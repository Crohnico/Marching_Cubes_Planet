using System.Collections.Generic;
using MarchingCubesPlanet.Coordinates;
using MarchingCubesPlanet.Lab;
using MarchingCubesPlanet.MarchingCubes;
using MarchingCubesPlanet.TrianglePools;
using UnityEngine;

namespace MarchingCubesPlanet.Preview
{
    [DisallowMultipleComponent]
    public sealed class PlanetRecipePayloadPreviewGenerationFlow : MonoBehaviour
    {
        private const int DefaultTemporaryTriangleCapacity = 1000000;
        private const string LogPrefix = "[Planet10.Generate] ";
        private const float MinimumRuntimeLodRefreshIntervalSeconds = 0.2f;

        [Header("Runtime Chain")]
        [SerializeField] private PlanetGpuShapeLab shapeLab;
        [SerializeField] private PlanetMarchingCubesLab marchingCubesLab;
        [SerializeField] private PlanetMarchingCubesPaintLab paintLab;

        [Header("Budget")]
        [SerializeField] private int minimumTemporaryTriangleCapacity = DefaultTemporaryTriangleCapacity;

        [Header("Runtime LOD")]
        [SerializeField] private bool runtimeLodUpdatesEnabled = true;
        [SerializeField] private float runtimeLodRefreshIntervalSeconds = MinimumRuntimeLodRefreshIntervalSeconds;
        [SerializeField] private PlanetChunkLodActivationProfile runtimeLodActivationProfile;

        [Header("State")]
        [SerializeField] private string lastDiagnostic;
        [SerializeField] private int lastDesiredLod0ChunkCount;
        [SerializeField] private int lastDesiredLod1ChunkCount;
        [SerializeField] private int lastDesiredLod2ChunkCount;
        [SerializeField] private int lastRuntimeLodChangedChunkCount;
        [SerializeField] private int lastRuntimeLodUpdateCount;
        [SerializeField] private int lastRuntimeLodViewVersion = -1;

        private readonly List<PlanetCachedChunkMesh> cachedChunks = new List<PlanetCachedChunkMesh>();
        private readonly List<PlanetChunkLodRuntimeEntry> runtimeChunkLods = new List<PlanetChunkLodRuntimeEntry>();
        private readonly Dictionary<Vector3Int, int> runtimeChunkIndexByChunkCoord = new Dictionary<Vector3Int, int>();
        private readonly List<int> runtimeLodAffectedChunkIndexes = new List<int>();
        private readonly HashSet<int> runtimeLodAffectedChunkIndexSet = new HashSet<int>();

        private PlanetRecipe runtimeLod1Recipe = PlanetRecipe.Default();
        private PlanetPlacement runtimePlacement = PlanetPlacement.Default();
        private MeshFilter runtimeTargetMeshFilter;
        private MeshRenderer runtimeTargetMeshRenderer;
        private PlanetChunkMeshCache runtimeChunkCache;
        private bool hasRuntimeChunkLods;
        private bool runtimeChunkCacheReady;
        private float lastRuntimeLodRefreshTime;
        private int runtimeTemporaryTriangleCapacity = DefaultTemporaryTriangleCapacity;
        private int runtimeCandidateChunkCount;
        private int activeRuntimeExtractionLod = -1;
        private int activeRuntimeExtractionChunkSize = -1;
        private bool hasRuntimeLodRingState;
        private Vector3 lastRuntimeLodCenterChunkCoords;
        private float lastRuntimeLod0RadiusChunks = -1f;
        private float lastRuntimeLod1RadiusChunks = -1f;

        public string LastDiagnostic => lastDiagnostic;
        public int LastDesiredLod0ChunkCount => lastDesiredLod0ChunkCount;
        public int LastDesiredLod1ChunkCount => lastDesiredLod1ChunkCount;
        public int LastDesiredLod2ChunkCount => lastDesiredLod2ChunkCount;
        public int RuntimeChunkLodCount => runtimeChunkLods.Count;
        public int LastRuntimeLodChangedChunkCount => lastRuntimeLodChangedChunkCount;
        public int LastRuntimeLodUpdateCount => lastRuntimeLodUpdateCount;
        public int LastRuntimeLodViewVersion => lastRuntimeLodViewVersion;

        private void Update()
        {
            if (!runtimeLodUpdatesEnabled || !hasRuntimeChunkLods)
            {
                return;
            }

            float now = Time.unscaledTime;
            bool intervalElapsed = now - lastRuntimeLodRefreshTime >= Mathf.Max(
                MinimumRuntimeLodRefreshIntervalSeconds,
                runtimeLodRefreshIntervalSeconds);
            if (intervalElapsed)
            {
                RefreshRuntimeChunkLods(false);
            }
        }

        public bool GenerateFromPreview(PlanetRecipePayloadPreview preview, Vector3 priorityOriginWorld)
        {
            if (preview == null)
            {
                lastDiagnostic = "Generate blocked: PlanetRecipePayloadPreview is missing.";
                return false;
            }

            PlanetRecipe sourceRecipe = preview.Recipe;
            if (!sourceRecipe.IsValid(out string recipeMessage))
            {
                lastDiagnostic = "Generate blocked: preview PlanetRecipe is invalid. " + recipeMessage;
                return false;
            }

            preview.EnsureRenderTargets(out MeshFilter targetMeshFilter, out MeshRenderer targetMeshRenderer);
            PlanetPlacement placement = BuildPreviewPlacement(preview);
            int requestedTriangleBudget = Mathf.Max(1, preview.RequestedTrianglePayload);
            return Generate(
                in sourceRecipe,
                placement,
                targetMeshFilter,
                targetMeshRenderer,
                requestedTriangleBudget,
                priorityOriginWorld);
        }

        public bool Generate(
            in PlanetRecipe sourceRecipe,
            PlanetPlacement placement,
            MeshFilter targetMeshFilter,
            MeshRenderer targetMeshRenderer,
            int requestedTriangleBudget,
            Vector3 priorityOriginWorld)
        {
            Debug.Log(LogPrefix + "START requestedTriangleBudget=" + requestedTriangleBudget +
                      " priorityOriginWorld=" + priorityOriginWorld +
                      " sourceGridRadius=" + sourceRecipe.GridRadius +
                      " sourceWorldScale=" + sourceRecipe.WorldScale +
                      " sourceWorldRadius=" + sourceRecipe.WorldRadius);

            ResolveReferences();
            if (shapeLab == null || marchingCubesLab == null || paintLab == null)
            {
                lastDiagnostic = "Generate blocked: runtime chain is incomplete. Required modules: 06 Shape, 07 Marching Cubes, 08 Paint, 09 Environment.";
                Debug.LogError(LogPrefix + lastDiagnostic +
                               " shapeLab=" + shapeLab +
                               " marchingCubesLab=" + marchingCubesLab +
                               " paintLab=" + paintLab);
                return false;
            }

            Release();
            ApplyChunkLodSummary(default);
            cachedChunks.Clear();
            ClearRuntimeChunkLods();

            int safeTriangleBudget = Mathf.Max(1, requestedTriangleBudget);
            int temporaryTriangleCapacity = Mathf.Max(safeTriangleBudget, Mathf.Max(1, minimumTemporaryTriangleCapacity));
            PlanetTrianglePoolRegistry.SetEnvironmentTriangleBudget(safeTriangleBudget);
            PlanetTrianglePoolRegistry.SetFallbackPriorityOriginWorld(priorityOriginWorld);
            Debug.Log(LogPrefix + "Budget applied environmentTriangles=" + safeTriangleBudget +
                      " temporaryTriangleCapacity=" + temporaryTriangleCapacity);

            PlanetChunkLod fallbackLod = PlanetChunkLodUtility.InitialFallbackLod;
            int fallbackLodIndex = (int)fallbackLod;
            PlanetChunkLodActivationConfig runtimeLodConfig = ResolveRuntimeLodActivationConfig();
            PlanetRecipe fallbackRecipe = PlanetChunkLodUtility.BuildRecipeForLod(in sourceRecipe, fallbackLod);
            Debug.Log(LogPrefix + "Fallback LOD prepared lod=" + fallbackLodIndex +
                      " fallbackGridRadius=" + fallbackRecipe.GridRadius +
                      " fallbackWorldScale=" + fallbackRecipe.WorldScale +
                      " fallbackWorldRadius=" + fallbackRecipe.WorldRadius +
                      " fallbackChunkSize=" + PlanetChunkLodUtility.GetChunkSizeForLod(fallbackLod, in runtimeLodConfig));
            shapeLab.SetRecipe(in fallbackRecipe);
            paintLab.SetPlacement(placement);
            paintLab.UsePlanetSurfaceAtlas(temporaryTriangleCapacity);

            runtimePlacement = placement;
            runtimeTargetMeshFilter = targetMeshFilter;
            runtimeTargetMeshRenderer = targetMeshRenderer;
            runtimeTemporaryTriangleCapacity = temporaryTriangleCapacity;
            runtimeChunkCache = PlanetChunkMeshCache.CreateDefault();
            runtimeChunkCacheReady = runtimeChunkCache.Prepare(in sourceRecipe, in runtimeLodConfig);
            Debug.Log(LogPrefix + "Cache prepare ready=" + runtimeChunkCacheReady +
                      " diagnostic=" + (runtimeChunkCache != null ? runtimeChunkCache.LastDiagnostic : "cache=null"));

            if (!InitializeRuntimeExtractionForLod(fallbackLod, in fallbackRecipe))
            {
                Debug.LogError(LogPrefix + "Initial LOD extraction setup failed. diagnostic=" + lastDiagnostic);
                return false;
            }
            Debug.Log(LogPrefix + "07 initialized candidateChunks=" + marchingCubesLab.LastCandidateChunkCount +
                      " chunkSize=" + marchingCubesLab.Settings.chunkRange.chunkSize +
                      " temporaryTriangleCapacity=" + marchingCubesLab.Settings.temporaryOutputTriangleCapacity);

            CollectRuntimeChunkLodsFromCandidateChunks(in fallbackRecipe, in sourceRecipe, in placement);
            Debug.Log(LogPrefix + "Runtime chunk table collected count=" + runtimeChunkLods.Count +
                      " mcCandidateChunks=" + marchingCubesLab.LastCandidateChunkCount);
            BeginRuntimeChunkLods(in sourceRecipe, true);
            Debug.Log(LogPrefix + "LOD2 pass finished changed=" + lastRuntimeLodChangedChunkCount +
                      " runtimeChunks=" + runtimeChunkLods.Count +
                      " paintHasLiveMesh=" + paintLab.HasLiveMesh +
                      " paintedChunks=" + paintLab.LastPaintedChunkCount +
                      " paintedTris=" + paintLab.LastPaintedTriangleCount +
                      " waterTris=" + paintLab.LastWaterTriangleCount);

            if (!paintLab.HasLiveMesh)
            {
                lastDiagnostic = "Generate finished without visible 09 Environment triangles after initial LOD changes. " +
                                 FormatLabDiagnostic(paintLab.LastDiagnostic);
                Debug.LogError(LogPrefix + lastDiagnostic +
                               " runtimeChunks=" + runtimeChunkLods.Count +
                               " changed=" + lastRuntimeLodChangedChunkCount +
                               " mcCandidateChunks=" + marchingCubesLab.LastCandidateChunkCount +
                               " mcLastExtractedChunk=" + marchingCubesLab.LastExtractedCandidateChunkIndex +
                               " mcTrisAttempted=" + marchingCubesLab.LastTriangleCountAttempted +
                               " mcTrisWritten=" + marchingCubesLab.LastTriangleCountWritten +
                               " mcOverflow=" + marchingCubesLab.LastOverflow +
                               " paintLastAction=" + paintLab.LastAction);
                return false;
            }

            lastDiagnostic = "Generated fixed LOD" + fallbackLodIndex + " shell. All chunks used desiredLOD=LOD" + fallbackLodIndex +
                             " loadedOrGenerated=" + lastRuntimeLodChangedChunkCount +
                             " chunks=" + runtimeChunkLods.Count +
                             " cache=" + (runtimeChunkCacheReady ? "ready" : "skipped") + ".";
            if (!runtimeChunkCacheReady)
            {
                lastDiagnostic += " Cache skipped: " + runtimeChunkCache.LastDiagnostic;
            }

            Debug.Log(LogPrefix + "SUCCESS " + lastDiagnostic);
            return true;
        }

        public void Release()
        {
            ResolveReferences();

            if (paintLab != null)
            {
                paintLab.ReleaseModule();
            }

            PlanetTrianglePoolRegistry.ReleaseAllSlots();

            if (marchingCubesLab != null)
            {
                marchingCubesLab.ReleaseModule();
            }

            if (shapeLab != null)
            {
                shapeLab.ReleaseModule();
            }

            ApplyChunkLodSummary(default);
            cachedChunks.Clear();
            ClearRuntimeChunkLods();
            runtimeChunkCache = null;
            runtimeChunkCacheReady = false;
            runtimeTargetMeshFilter = null;
            runtimeTargetMeshRenderer = null;
            runtimeCandidateChunkCount = 0;
            activeRuntimeExtractionLod = -1;
            activeRuntimeExtractionChunkSize = -1;
        }

        private void ApplyChunkLodSummary(PlanetChunkLodSummary summary)
        {
            lastDesiredLod0ChunkCount = summary.lod0Count;
            lastDesiredLod1ChunkCount = summary.lod1Count;
            lastDesiredLod2ChunkCount = summary.lod2Count;
        }

        private void BeginRuntimeChunkLods(in PlanetRecipe lod1Recipe, bool forceInitialFallbackDesired)
        {
            runtimeLod1Recipe = lod1Recipe;
            hasRuntimeChunkLods = runtimeChunkLods.Count > 0;
            lastRuntimeLodViewVersion = -1;
            lastRuntimeLodRefreshTime = 0f;
            lastRuntimeLodChangedChunkCount = 0;
            lastRuntimeLodUpdateCount = 0;
            hasRuntimeLodRingState = false;
            Debug.Log(LogPrefix + "BeginRuntimeChunkLods forceInitialFallbackDesired=" + forceInitialFallbackDesired +
                      " chunkCount=" + runtimeChunkLods.Count +
                      " hasRuntimeChunkLods=" + hasRuntimeChunkLods);
            if (forceInitialFallbackDesired)
            {
                ApplyChunkLodSummary(ForceRuntimeDesiredLod(PlanetChunkLodUtility.InitialFallbackLod));
                lastRuntimeLodChangedChunkCount = ProcessRuntimeChunkLodChanges();
                lastRuntimeLodUpdateCount++;
                lastRuntimeLodViewVersion = PlanetTrianglePoolRegistry.PlayerViewVersion;
                lastRuntimeLodRefreshTime = Time.unscaledTime;
                hasRuntimeLodRingState = false;
                hasRuntimeChunkLods = runtimeChunkLods.Count > 0;
                Debug.Log(LogPrefix + "Initial fallback processing done changed=" + lastRuntimeLodChangedChunkCount +
                          " runtimeUpdates=" + lastRuntimeLodUpdateCount +
                          " initializedChunks=" + CountInitializedRuntimeChunks() +
                          "/" + runtimeChunkLods.Count +
                          " hasRuntimeChunkLods=" + hasRuntimeChunkLods);
                return;
            }

            RefreshRuntimeChunkLods(true);
        }

        private void RefreshRuntimeChunkLods(bool force)
        {
            if (!force && (!runtimeLodUpdatesEnabled || !hasRuntimeChunkLods))
            {
                return;
            }

            PlanetChunkLodActivationConfig activationConfig = ResolveRuntimeLodActivationConfig();
            activationConfig.EnsureValid();
            int viewVersion = PlanetTrianglePoolRegistry.PlayerViewVersion;
            Vector3 centerChunkCoords = CalculateRuntimeLodCenterChunkCoords(
                PlanetTrianglePoolRegistry.PlayerPositionWorld,
                in activationConfig);
            float lod0RadiusChunks = CalculateRuntimeLodRadiusChunks(
                activationConfig.enableLod0 ? activationConfig.lod0MaxDistanceWorld : -1f,
                in activationConfig);
            float lod1RadiusChunks = CalculateRuntimeLodRadiusChunks(
                activationConfig.lod1MaxDistanceWorld,
                in activationConfig);
            int changedLodCount;
            int appliedChangeCount;
            bool updateRingState;
            if (force || !hasRuntimeLodRingState)
            {
                PlanetChunkLodSummary summary = PlanetChunkLodRuntimePlanner.EvaluateEntries(
                    runtimeChunkLods,
                    in runtimeLod1Recipe,
                    PlanetTrianglePoolRegistry.PlayerPositionWorld,
                    activationConfig,
                    out changedLodCount);
                ApplyChunkLodSummary(summary);
                appliedChangeCount = ProcessRuntimeChunkLodChanges();
                updateRingState = true;
            }
            else
            {
                CollectRuntimeLodRingDiffIndexes(
                    lastRuntimeLodCenterChunkCoords,
                    centerChunkCoords,
                    lastRuntimeLod0RadiusChunks,
                    lod0RadiusChunks,
                    lastRuntimeLod1RadiusChunks,
                    lod1RadiusChunks);
                if (runtimeLodAffectedChunkIndexes.Count > 0)
                {
                    changedLodCount = EvaluateRuntimeChunkLodIndexes(
                        runtimeLodAffectedChunkIndexes,
                        in activationConfig);
                    appliedChangeCount = ProcessRuntimeChunkLodChanges(runtimeLodAffectedChunkIndexes);
                    updateRingState = true;
                }
                else
                {
                    changedLodCount = 0;
                    appliedChangeCount = 0;
                    updateRingState = false;
                }
            }

            lastRuntimeLodChangedChunkCount = Mathf.Max(changedLodCount, appliedChangeCount);
            lastRuntimeLodUpdateCount++;
            lastRuntimeLodViewVersion = viewVersion;
            lastRuntimeLodRefreshTime = Time.unscaledTime;
            if (updateRingState)
            {
                hasRuntimeLodRingState = true;
                lastRuntimeLodCenterChunkCoords = centerChunkCoords;
                lastRuntimeLod0RadiusChunks = lod0RadiusChunks;
                lastRuntimeLod1RadiusChunks = lod1RadiusChunks;
            }

        }

        private PlanetChunkLodSummary ForceRuntimeDesiredLod(PlanetChunkLod desiredLod)
        {
            PlanetChunkLodSummary summary = default;
            for (int i = 0; i < runtimeChunkLods.Count; i++)
            {
                PlanetChunkLodRuntimeEntry entry = runtimeChunkLods[i];
                entry.ForceDesiredLod(desiredLod);
                runtimeChunkLods[i] = entry;
                summary.Record(desiredLod);
            }

            return summary;
        }

        private int ProcessRuntimeChunkLodChanges()
        {
            if (!hasRuntimeChunkLods)
            {
                return 0;
            }

            int changedCount = 0;
            for (int i = 0; i < runtimeChunkLods.Count; i++)
            {
                PlanetChunkLodRuntimeEntry entry = runtimeChunkLods[i];
                if (!entry.NeedsLodChange)
                {
                    continue;
                }

                if (!ApplyRuntimeChunkLodChange(entry))
                {
                    continue;
                }

                entry.MarkInitialized(entry.DesiredLod);
                runtimeChunkLods[i] = entry;
                changedCount++;
            }

            return changedCount;
        }

        private int ProcessRuntimeChunkLodChanges(IList<int> indexes)
        {
            if (!hasRuntimeChunkLods || indexes == null || indexes.Count <= 0)
            {
                return 0;
            }

            int changedCount = 0;
            for (int i = 0; i < indexes.Count; i++)
            {
                int entryIndex = indexes[i];
                if (entryIndex < 0 || entryIndex >= runtimeChunkLods.Count)
                {
                    continue;
                }

                PlanetChunkLodRuntimeEntry entry = runtimeChunkLods[entryIndex];
                if (!entry.NeedsLodChange)
                {
                    continue;
                }

                if (!ApplyRuntimeChunkLodChange(entry))
                {
                    continue;
                }

                entry.MarkInitialized(entry.DesiredLod);
                runtimeChunkLods[entryIndex] = entry;
                changedCount++;
            }

            return changedCount;
        }

        private int EvaluateRuntimeChunkLodIndexes(
            IList<int> indexes,
            in PlanetChunkLodActivationConfig activationConfig)
        {
            if (indexes == null || indexes.Count <= 0)
            {
                return 0;
            }

            PlanetChunkLodResolutionContext context = new PlanetChunkLodResolutionContext(
                PlanetTrianglePoolRegistry.PlayerPositionWorld,
                PlanetChunkLodUtility.CalculateBaseChunkWorldSize(in runtimeLod1Recipe, in activationConfig),
                activationConfig);
            int changedLodCount = 0;
            for (int i = 0; i < indexes.Count; i++)
            {
                int entryIndex = indexes[i];
                if (entryIndex < 0 || entryIndex >= runtimeChunkLods.Count)
                {
                    continue;
                }

                PlanetChunkLodRuntimeEntry entry = runtimeChunkLods[entryIndex];
                PlanetChunkLod previousDesiredLod = entry.DesiredLod;
                float distanceWorld = (entry.CenterWorld - context.PlayerPositionWorld).magnitude;
                PlanetChunkLod desiredLod = PlanetChunkLodResolver.ResolveDesiredLod(
                    distanceWorld,
                    in context,
                    entry.CurrentLod);
                if (entry.ApplyDesiredLod(desiredLod))
                {
                    AdjustRuntimeDesiredLodCount(previousDesiredLod, desiredLod);
                    changedLodCount++;
                }

                runtimeChunkLods[entryIndex] = entry;
            }

            return changedLodCount;
        }

        private void AdjustRuntimeDesiredLodCount(PlanetChunkLod previousLod, PlanetChunkLod nextLod)
        {
            if (previousLod == nextLod)
            {
                return;
            }

            DecrementRuntimeDesiredLodCount(previousLod);
            switch (nextLod)
            {
                case PlanetChunkLod.LOD0:
                    lastDesiredLod0ChunkCount++;
                    break;
                case PlanetChunkLod.LOD1:
                    lastDesiredLod1ChunkCount++;
                    break;
                default:
                    lastDesiredLod2ChunkCount++;
                    break;
            }
        }

        private void DecrementRuntimeDesiredLodCount(PlanetChunkLod lod)
        {
            switch (lod)
            {
                case PlanetChunkLod.LOD0:
                    lastDesiredLod0ChunkCount = Mathf.Max(0, lastDesiredLod0ChunkCount - 1);
                    break;
                case PlanetChunkLod.LOD1:
                    lastDesiredLod1ChunkCount = Mathf.Max(0, lastDesiredLod1ChunkCount - 1);
                    break;
                default:
                    lastDesiredLod2ChunkCount = Mathf.Max(0, lastDesiredLod2ChunkCount - 1);
                    break;
            }
        }

        private Vector3 CalculateRuntimeLodCenterChunkCoords(
            Vector3 playerPositionWorld,
            in PlanetChunkLodActivationConfig activationConfig)
        {
            Vector3 playerGrid = PlanetCoordinateConverter.WorldToGrid(playerPositionWorld, in runtimeLod1Recipe, in runtimePlacement);
            int chunkSize = activationConfig.CanonicalChunkSize;
            return playerGrid / chunkSize;
        }

        private float CalculateRuntimeLodRadiusChunks(
            float distanceWorld,
            in PlanetChunkLodActivationConfig activationConfig)
        {
            if (distanceWorld <= 0f)
            {
                return -1f;
            }

            float baseChunkWorldSize = PlanetChunkLodUtility.CalculateBaseChunkWorldSize(
                in runtimeLod1Recipe,
                in activationConfig);
            return Mathf.Max(0f, distanceWorld / baseChunkWorldSize);
        }

        private void CollectRuntimeLodRingDiffIndexes(
            Vector3 previousCenter,
            Vector3 nextCenter,
            float previousLod0Radius,
            float nextLod0Radius,
            float previousLod1Radius,
            float nextLod1Radius)
        {
            runtimeLodAffectedChunkIndexes.Clear();
            runtimeLodAffectedChunkIndexSet.Clear();
            CollectRuntimeLodRangeDiffIndexes(previousCenter, nextCenter, previousLod1Radius, nextLod1Radius);
            CollectRuntimeLodRangeDiffIndexes(previousCenter, nextCenter, previousLod0Radius, nextLod0Radius);
        }

        private void CollectRuntimeLodRangeDiffIndexes(
            Vector3 previousCenter,
            Vector3 nextCenter,
            float previousRadius,
            float nextRadius)
        {
            if ((previousCenter - nextCenter).sqrMagnitude <= 0.000001f &&
                Mathf.Approximately(previousRadius, nextRadius))
            {
                return;
            }

            if (previousRadius >= 0f)
            {
                CollectRuntimeLodRangeOnlyInFirst(previousCenter, previousRadius, nextCenter, nextRadius);
            }

            if (nextRadius >= 0f)
            {
                CollectRuntimeLodRangeOnlyInFirst(nextCenter, nextRadius, previousCenter, previousRadius);
            }
        }

        private void CollectRuntimeLodRangeOnlyInFirst(
            Vector3 firstCenter,
            float firstRadius,
            Vector3 secondCenter,
            float secondRadius)
        {
            int minX = Mathf.FloorToInt(firstCenter.x - firstRadius);
            int minY = Mathf.FloorToInt(firstCenter.y - firstRadius);
            int minZ = Mathf.FloorToInt(firstCenter.z - firstRadius);
            int maxX = Mathf.FloorToInt(firstCenter.x + firstRadius);
            int maxY = Mathf.FloorToInt(firstCenter.y + firstRadius);
            int maxZ = Mathf.FloorToInt(firstCenter.z + firstRadius);
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        Vector3Int chunkCoord = new Vector3Int(x, y, z);
                        if (!IsRuntimeChunkCenterInsideLodSphere(chunkCoord, firstCenter, firstRadius) ||
                            IsRuntimeChunkCenterInsideLodSphere(chunkCoord, secondCenter, secondRadius))
                        {
                            continue;
                        }

                        AddRuntimeLodAffectedChunkIndex(chunkCoord);
                    }
                }
            }
        }

        private bool IsRuntimeChunkCenterInsideLodSphere(Vector3Int chunkCoord, Vector3 center, float radius)
        {
            if (radius < 0f)
            {
                return false;
            }

            Vector3 chunkCenter = new Vector3(
                chunkCoord.x + 0.5f,
                chunkCoord.y + 0.5f,
                chunkCoord.z + 0.5f);
            float dx = chunkCenter.x - center.x;
            float dy = chunkCenter.y - center.y;
            float dz = chunkCenter.z - center.z;
            return dx * dx + dy * dy + dz * dz <= radius * radius;
        }

        private void AddRuntimeLodAffectedChunkIndex(Vector3Int chunkCoord)
        {
            if (!runtimeChunkIndexByChunkCoord.TryGetValue(chunkCoord, out int entryIndex))
            {
                return;
            }

            if (!runtimeLodAffectedChunkIndexSet.Add(entryIndex))
            {
                return;
            }

            runtimeLodAffectedChunkIndexes.Add(entryIndex);
        }

        private PlanetChunkLodActivationConfig ResolveRuntimeLodActivationConfig()
        {
            if (runtimeLodActivationProfile == null)
            {
                runtimeLodActivationProfile = Resources.Load<PlanetChunkLodActivationProfile>(
                    PlanetChunkLodActivationProfile.DefaultResourcesPath);
            }

            return runtimeLodActivationProfile != null
                ? runtimeLodActivationProfile.ToConfig()
                : PlanetChunkLodActivationConfig.Default();
        }

        private int CountInitializedRuntimeChunks()
        {
            int initializedCount = 0;
            for (int i = 0; i < runtimeChunkLods.Count; i++)
            {
                if (runtimeChunkLods[i].IsInitialized)
                {
                    initializedCount++;
                }
            }

            return initializedCount;
        }

        private bool ApplyRuntimeChunkLodChange(PlanetChunkLodRuntimeEntry entry)
        {
            int lod = (int)entry.DesiredLod;
            PlanetRecipe lodRecipe = PlanetChunkLodUtility.BuildRecipeForLod(in runtimeLod1Recipe, entry.DesiredLod);
            string meshId = BuildStableChunkMeshId(entry.ChunkOrigin);

            if (runtimeChunkCacheReady &&
                runtimeChunkCache.TryLoadChunkMesh(
                    entry.ChunkId,
                    lod,
                    PlanetChunkCachePayloadMode.MeshOnly,
                    out PlanetCachedChunkMesh cachedChunk,
                    out PlanetChunkCacheLoadSummary _))
            {
                bool painted = paintLab.PaintNamedMesh(
                    meshId,
                    cachedChunk.SurfaceMesh,
                    cachedChunk.WaterMesh,
                    runtimeTargetMeshFilter,
                    runtimeTargetMeshRenderer,
                    runtimePlacement,
                    in lodRecipe,
                    entry.ChunkId);
                if (!painted)
                {
                    cachedChunk.ReleaseMeshes();
                }

                return painted;
            }

            return GenerateAndPaintRuntimeChunk(entry, in lodRecipe, lod, meshId);
        }

        private bool GenerateAndPaintRuntimeChunk(
            PlanetChunkLodRuntimeEntry entry,
            in PlanetRecipe lodRecipe,
            int lod,
            string meshId)
        {
            if (!InitializeRuntimeExtractionForLod(entry.DesiredLod, in lodRecipe))
            {
                Debug.LogError(LogPrefix + "Generate chunk setup failed chunkId=" + entry.ChunkId +
                               " lod=" + lod +
                               " diagnostic=" + lastDiagnostic);
                return false;
            }

            PlanetMarchingCubesChunkOrigin lodChunkOrigin = ConvertChunkOrigin(
                entry.ChunkOrigin,
                in runtimeLod1Recipe,
                in lodRecipe);
            if (!TryFindCandidateChunkIndex(lodChunkOrigin, out int candidateChunkIndex))
            {
                return false;
            }

            marchingCubesLab.ExtractCandidateChunkSurface(candidateChunkIndex);
            if (marchingCubesLab.LastOverflow)
            {
                lastDiagnostic = "Runtime LOD change blocked: 07 overflow for chunk " + entry.ChunkId +
                                 " LOD" + lod +
                                 ". Attempted tris=" + marchingCubesLab.LastTriangleCountAttempted +
                                 ", capacity tris=" + runtimeTemporaryTriangleCapacity + ".";
                Debug.LogError(LogPrefix + lastDiagnostic);
                return false;
            }

            if (marchingCubesLab.LastTriangleCountWritten == 0u)
            {
                Debug.LogWarning(LogPrefix + "Chunk extracted zero triangles chunkId=" + entry.ChunkId +
                                 " lod=" + lod +
                                 ". Publishing empty named mesh through existing path.");
                paintLab.PaintNamedMesh(
                    meshId,
                    null,
                    null,
                    runtimeTargetMeshFilter,
                    runtimeTargetMeshRenderer,
                    runtimePlacement,
                    in lodRecipe,
                    entry.ChunkId);
                return true;
            }

            bool paintedExtraction = paintLab.PaintLastExtractionAsNamedMesh(
                meshId,
                entry.ChunkId,
                runtimeTargetMeshFilter,
                runtimeTargetMeshRenderer,
                runtimePlacement,
                in lodRecipe,
                runtimeChunkCacheReady ? runtimeChunkCache : null,
                lod);
            return paintedExtraction;
        }

        private bool InitializeRuntimeExtractionForLod(PlanetChunkLod lod, in PlanetRecipe lodRecipe)
        {
            int lodIndex = (int)lod;
            PlanetChunkLodActivationConfig activationConfig = ResolveRuntimeLodActivationConfig();
            int chunkSize = PlanetChunkLodUtility.GetChunkSizeForLod(lod, in activationConfig);
            if (activeRuntimeExtractionLod == lodIndex &&
                activeRuntimeExtractionChunkSize == chunkSize &&
                shapeLab.IsShapeGpuInitialized &&
                marchingCubesLab.HasLiveResources)
            {
                return true;
            }

            Debug.Log(LogPrefix + "Initialize extraction resources lod=" + lodIndex +
                      " gridRadius=" + lodRecipe.GridRadius +
                      " worldScale=" + lodRecipe.WorldScale +
                      " chunkSize=" + chunkSize);
            shapeLab.SetRecipe(in lodRecipe);
            shapeLab.InitShapeGpu();
            if (!shapeLab.IsShapeGpuInitialized)
            {
                lastDiagnostic = "Generate blocked: 06 Shape GPU did not initialize. " +
                                 FormatLabDiagnostic(shapeLab.LastDiagnostic);
                activeRuntimeExtractionLod = -1;
                activeRuntimeExtractionChunkSize = -1;
                Debug.LogError(LogPrefix + lastDiagnostic);
                return false;
            }

            marchingCubesLab.EnsureTemporaryOutputTriangleCapacity(runtimeTemporaryTriangleCapacity);
            marchingCubesLab.SetRuntimeChunkSize(chunkSize);
            marchingCubesLab.InitMarchingCubesGpu();
            if (!marchingCubesLab.HasLiveResources)
            {
                lastDiagnostic = "Generate blocked: 07 Marching Cubes did not initialize. " +
                                 FormatLabDiagnostic(marchingCubesLab.LastDiagnostic);
                activeRuntimeExtractionLod = -1;
                activeRuntimeExtractionChunkSize = -1;
                Debug.LogError(LogPrefix + lastDiagnostic);
                return false;
            }

            activeRuntimeExtractionLod = lodIndex;
            activeRuntimeExtractionChunkSize = chunkSize;
            runtimeCandidateChunkCount = Mathf.Max(0, (int)marchingCubesLab.LastCandidateChunkCount);
            Debug.Log(LogPrefix + "Extraction resources ready lod=" + lodIndex +
                      " candidateChunks=" + marchingCubesLab.LastCandidateChunkCount +
                      " chunkSize=" + marchingCubesLab.Settings.chunkRange.chunkSize);
            return true;
        }

        private void CollectRuntimeChunkLodsFromCandidateChunks(
            in PlanetRecipe renderRecipe,
            in PlanetRecipe lod1Recipe,
            in PlanetPlacement placement)
        {
            runtimeChunkLods.Clear();
            runtimeChunkIndexByChunkCoord.Clear();
            int candidateCount = Mathf.Max(0, (int)marchingCubesLab.LastCandidateChunkCount);
            runtimeCandidateChunkCount = candidateCount;
            float activeChunkSize = Mathf.Max(1, marchingCubesLab.Settings.chunkRange.chunkSize);
            int baseChunkSize = Mathf.Max(1, PlanetChunkLodUtility.GetChunkSizeForLod(
                PlanetChunkLod.LOD1,
                ResolveRuntimeLodActivationConfig()));
            Debug.Log(LogPrefix + "Collect runtime chunks start candidateCount=" + candidateCount +
                      " activeChunkSize=" + activeChunkSize);
            for (int chunkId = 0; chunkId < candidateCount; chunkId++)
            {
                if (!marchingCubesLab.TryGetCandidateChunkOrigin(chunkId, out PlanetMarchingCubesChunkOrigin origin))
                {
                    Debug.LogWarning(LogPrefix + "Candidate chunk origin missing chunkId=" + chunkId);
                    continue;
                }

                Vector3 centerGrid = new Vector3(
                    origin.x + activeChunkSize * 0.5f,
                    origin.y + activeChunkSize * 0.5f,
                    origin.z + activeChunkSize * 0.5f);
                Vector3 centerWorld = PlanetCoordinateConverter.GridToWorld(centerGrid, in renderRecipe, in placement);
                PlanetMarchingCubesChunkOrigin baseOrigin = ConvertChunkOrigin(origin, in renderRecipe, in lod1Recipe);
                int stableChunkId = BuildStableChunkId(baseOrigin);
                int entryIndex = runtimeChunkLods.Count;
                runtimeChunkLods.Add(new PlanetChunkLodRuntimeEntry(stableChunkId, baseOrigin, centerWorld));
                runtimeChunkIndexByChunkCoord[BuildRuntimeChunkCoord(baseOrigin, baseChunkSize)] = entryIndex;
            }
            Debug.Log(LogPrefix + "Collect runtime chunks end count=" + runtimeChunkLods.Count);
        }

        private bool TryFindCandidateChunkIndex(PlanetMarchingCubesChunkOrigin origin, out int candidateChunkIndex)
        {
            int candidateCount = runtimeCandidateChunkCount;
            for (int i = 0; i < candidateCount; i++)
            {
                if (!marchingCubesLab.TryGetCandidateChunkOrigin(i, out PlanetMarchingCubesChunkOrigin candidateOrigin))
                {
                    continue;
                }

                if (candidateOrigin.x == origin.x &&
                    candidateOrigin.y == origin.y &&
                    candidateOrigin.z == origin.z)
                {
                    candidateChunkIndex = i;
                    return true;
                }
            }

            candidateChunkIndex = -1;
            return false;
        }

        private static PlanetMarchingCubesChunkOrigin ConvertChunkOrigin(
            PlanetMarchingCubesChunkOrigin origin,
            in PlanetRecipe fromRecipe,
            in PlanetRecipe toRecipe)
        {
            float scale = fromRecipe.WorldScale / Mathf.Max(0.0001f, toRecipe.WorldScale);
            return new PlanetMarchingCubesChunkOrigin(
                Mathf.RoundToInt(origin.x * scale),
                Mathf.RoundToInt(origin.y * scale),
                Mathf.RoundToInt(origin.z * scale));
        }

        private static int BuildStableChunkId(PlanetMarchingCubesChunkOrigin origin)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + origin.x;
                hash = hash * 31 + origin.y;
                hash = hash * 31 + origin.z;
                return hash & 0x7fffffff;
            }
        }

        private static string BuildStableChunkMeshId(PlanetMarchingCubesChunkOrigin origin)
        {
            return "chunk_" + origin.x + "_" + origin.y + "_" + origin.z;
        }

        private static Vector3Int BuildRuntimeChunkCoord(
            PlanetMarchingCubesChunkOrigin origin,
            int chunkSize)
        {
            chunkSize = Mathf.Max(1, chunkSize);
            return new Vector3Int(
                Mathf.FloorToInt(origin.x / (float)chunkSize),
                Mathf.FloorToInt(origin.y / (float)chunkSize),
                Mathf.FloorToInt(origin.z / (float)chunkSize));
        }

        private void ClearRuntimeChunkLods()
        {
            runtimeChunkLods.Clear();
            runtimeChunkIndexByChunkCoord.Clear();
            runtimeLodAffectedChunkIndexes.Clear();
            runtimeLodAffectedChunkIndexSet.Clear();
            hasRuntimeChunkLods = false;
            hasRuntimeLodRingState = false;
            lastRuntimeLodChangedChunkCount = 0;
            lastRuntimeLodUpdateCount = 0;
            lastRuntimeLodViewVersion = -1;
            lastRuntimeLodRefreshTime = 0f;
        }

        private static void ReleaseCachedChunkMeshes(List<PlanetCachedChunkMesh> chunks)
        {
            if (chunks == null)
            {
                return;
            }

            for (int i = 0; i < chunks.Count; i++)
            {
                chunks[i]?.ReleaseMeshes();
            }

            chunks.Clear();
        }

        private void ResolveReferences()
        {
            if (shapeLab == null)
            {
                shapeLab = FindFirstObjectByType<PlanetGpuShapeLab>();
            }

            if (marchingCubesLab == null)
            {
                marchingCubesLab = FindFirstObjectByType<PlanetMarchingCubesLab>();
            }

            if (paintLab == null)
            {
                paintLab = FindFirstObjectByType<PlanetMarchingCubesPaintLab>();
            }
        }

        private static PlanetPlacement BuildPreviewPlacement(PlanetRecipePayloadPreview preview)
        {
            PlanetPlacement placement = new PlanetPlacement(preview.TransformPlanetWorldCenter)
            {
                PlanetRotation = preview.TransformPlanetRotation
            };
            return placement;
        }

        private static long CalculateMarchingCubesTemporaryBufferBytes(int temporaryTriangleCapacity)
        {
            return (long)Mathf.Max(0, temporaryTriangleCapacity) * 3L * 32L;
        }

        private static string FormatBytes(long bytes)
        {
            double mib = bytes / (1024.0 * 1024.0);
            return bytes + " bytes / " + mib.ToString("0.00") + " MiB";
        }

        private static string FormatLabDiagnostic(PlanetLabDiagnostic diagnostic)
        {
            if (string.IsNullOrWhiteSpace(diagnostic.title))
            {
                return "No lab diagnostic was reported.";
            }

            return diagnostic.title +
                   " Cause: " + diagnostic.probableCause +
                   " Action: " + diagnostic.recommendedAction;
        }
    }
}
