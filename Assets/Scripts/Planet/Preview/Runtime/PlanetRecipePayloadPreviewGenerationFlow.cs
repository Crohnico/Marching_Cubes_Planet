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

        [Header("Runtime Chain")]
        [SerializeField] private PlanetGpuShapeLab shapeLab;
        [SerializeField] private PlanetMarchingCubesLab marchingCubesLab;
        [SerializeField] private PlanetMarchingCubesPaintLab paintLab;

        [Header("Budget")]
        [SerializeField] private int minimumTemporaryTriangleCapacity = DefaultTemporaryTriangleCapacity;

        [Header("Runtime LOD")]
        [SerializeField] private bool runtimeLodUpdatesEnabled = true;
        [SerializeField] private float runtimeLodRefreshIntervalSeconds = 0.1f;
        [SerializeField] private PlanetChunkLodActivationProfile runtimeLodActivationProfile;

        [Header("State")]
        [SerializeField] private string lastDiagnostic;
        [SerializeField] private int lastDesiredLod0ChunkCount;
        [SerializeField] private int lastDesiredLod1ChunkCount;
        [SerializeField] private int lastDesiredLod2ChunkCount;
        [SerializeField] private float lastBestChunkLodScore;
        [SerializeField] private float lastAverageChunkLodScore;
        [SerializeField] private int lastRuntimeLodChangedChunkCount;
        [SerializeField] private int lastRuntimeLodUpdateCount;
        [SerializeField] private int lastRuntimeLodViewVersion = -1;

        private readonly List<PlanetCachedChunkMesh> cachedChunks = new List<PlanetCachedChunkMesh>();
        private readonly List<PlanetChunkLodRuntimeEntry> runtimeChunkLods = new List<PlanetChunkLodRuntimeEntry>();

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

        public string LastDiagnostic => lastDiagnostic;
        public int LastDesiredLod0ChunkCount => lastDesiredLod0ChunkCount;
        public int LastDesiredLod1ChunkCount => lastDesiredLod1ChunkCount;
        public int LastDesiredLod2ChunkCount => lastDesiredLod2ChunkCount;
        public float LastBestChunkLodScore => lastBestChunkLodScore;
        public float LastAverageChunkLodScore => lastAverageChunkLodScore;
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

            int viewVersion = PlanetTrianglePoolRegistry.PlayerViewVersion;
            float now = Time.unscaledTime;
            bool versionChanged = viewVersion != lastRuntimeLodViewVersion;
            bool intervalElapsed = now - lastRuntimeLodRefreshTime >= Mathf.Max(0.01f, runtimeLodRefreshIntervalSeconds);
            if (versionChanged || intervalElapsed)
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
            PlanetRecipe fallbackRecipe = PlanetChunkLodUtility.BuildRecipeForLod(in sourceRecipe, fallbackLod);
            Debug.Log(LogPrefix + "Fallback LOD prepared lod=" + fallbackLodIndex +
                      " fallbackGridRadius=" + fallbackRecipe.GridRadius +
                      " fallbackWorldScale=" + fallbackRecipe.WorldScale +
                      " fallbackWorldRadius=" + fallbackRecipe.WorldRadius +
                      " fallbackChunkSize=" + PlanetChunkLodUtility.GetChunkSizeForLod(fallbackLod));
            shapeLab.SetRecipe(in fallbackRecipe);
            paintLab.SetPlacement(placement);
            paintLab.UsePlanetSurfaceAtlas(temporaryTriangleCapacity);

            runtimePlacement = placement;
            runtimeTargetMeshFilter = targetMeshFilter;
            runtimeTargetMeshRenderer = targetMeshRenderer;
            runtimeTemporaryTriangleCapacity = temporaryTriangleCapacity;
            runtimeChunkCache = PlanetChunkMeshCache.CreateDefault();
            runtimeChunkCacheReady = runtimeChunkCache.Prepare(in sourceRecipe);
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

            CollectRuntimeChunkLodsFromCandidateChunks(in fallbackRecipe, in placement);
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
        }

        private void ApplyChunkLodSummary(PlanetChunkLodSummary summary)
        {
            lastDesiredLod0ChunkCount = summary.lod0Count;
            lastDesiredLod1ChunkCount = summary.lod1Count;
            lastDesiredLod2ChunkCount = summary.lod2Count;
            lastBestChunkLodScore = summary.bestScore;
            lastAverageChunkLodScore = summary.averageScore;
        }

        private void BeginRuntimeChunkLods(in PlanetRecipe lod1Recipe, bool forceInitialFallbackDesired)
        {
            runtimeLod1Recipe = lod1Recipe;
            hasRuntimeChunkLods = runtimeChunkLods.Count > 0;
            lastRuntimeLodViewVersion = -1;
            lastRuntimeLodRefreshTime = 0f;
            lastRuntimeLodChangedChunkCount = 0;
            lastRuntimeLodUpdateCount = 0;
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

            PlanetChunkLodSummary summary = PlanetChunkLodRuntimePlanner.EvaluateEntries(
                runtimeChunkLods,
                in runtimeLod1Recipe,
                PlanetTrianglePoolRegistry.PlayerPositionWorld,
                PlanetTrianglePoolRegistry.PlayerForwardWorld,
                ResolveRuntimeLodActivationConfig(),
                out int changedLodCount);
            int viewVersion = PlanetTrianglePoolRegistry.PlayerViewVersion;
            ApplyChunkLodSummary(summary);
            int appliedChangeCount = ProcessRuntimeChunkLodChanges();
            lastRuntimeLodChangedChunkCount = Mathf.Max(changedLodCount, appliedChangeCount);
            lastRuntimeLodUpdateCount++;
            lastRuntimeLodViewVersion = viewVersion;
            lastRuntimeLodRefreshTime = Time.unscaledTime;

        }

        private PlanetChunkLodSummary ForceRuntimeDesiredLod(PlanetChunkLod desiredLod)
        {
            PlanetChunkLodSummary summary = default;
            for (int i = 0; i < runtimeChunkLods.Count; i++)
            {
                PlanetChunkLodRuntimeEntry entry = runtimeChunkLods[i];
                entry.ForceDesiredLod(desiredLod);
                runtimeChunkLods[i] = entry;
                summary.Record(new PlanetChunkLodScore(
                    entry.ChunkId,
                    desiredLod,
                    entry.Score,
                    entry.ProximityScore,
                    entry.ViewScore,
                    entry.DistanceChunks,
                    entry.ViewRayDistanceChunks,
                    entry.CenterWorld));
            }

            summary.Finish();
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
            string meshId = PlanetMarchingCubesMeshPainter.BuildChunkMeshId(entry.ChunkId);

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

            if (entry.ChunkId < 0 || entry.ChunkId >= runtimeCandidateChunkCount)
            {
                lastDiagnostic = "Runtime LOD change skipped: chunkId outside 07 candidate range. chunkId=" +
                                 entry.ChunkId + " candidates=" + runtimeCandidateChunkCount + ".";
                Debug.LogError(LogPrefix + lastDiagnostic);
                return false;
            }

            marchingCubesLab.ExtractCandidateChunkSurface(entry.ChunkId);
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
            if (activeRuntimeExtractionLod == lodIndex &&
                shapeLab.IsShapeGpuInitialized &&
                marchingCubesLab.HasLiveResources)
            {
                return true;
            }

            Debug.Log(LogPrefix + "Initialize extraction resources lod=" + lodIndex +
                      " gridRadius=" + lodRecipe.GridRadius +
                      " worldScale=" + lodRecipe.WorldScale +
                      " chunkSize=" + PlanetChunkLodUtility.GetChunkSizeForLod(lod));
            shapeLab.SetRecipe(in lodRecipe);
            shapeLab.InitShapeGpu();
            if (!shapeLab.IsShapeGpuInitialized)
            {
                lastDiagnostic = "Generate blocked: 06 Shape GPU did not initialize. " +
                                 FormatLabDiagnostic(shapeLab.LastDiagnostic);
                activeRuntimeExtractionLod = -1;
                Debug.LogError(LogPrefix + lastDiagnostic);
                return false;
            }

            marchingCubesLab.EnsureTemporaryOutputTriangleCapacity(runtimeTemporaryTriangleCapacity);
            marchingCubesLab.SetRuntimeChunkSize(PlanetChunkLodUtility.GetChunkSizeForLod(lod));
            marchingCubesLab.InitMarchingCubesGpu();
            if (!marchingCubesLab.HasLiveResources)
            {
                lastDiagnostic = "Generate blocked: 07 Marching Cubes did not initialize. " +
                                 FormatLabDiagnostic(marchingCubesLab.LastDiagnostic);
                activeRuntimeExtractionLod = -1;
                Debug.LogError(LogPrefix + lastDiagnostic);
                return false;
            }

            activeRuntimeExtractionLod = lodIndex;
            Debug.Log(LogPrefix + "Extraction resources ready lod=" + lodIndex +
                      " candidateChunks=" + marchingCubesLab.LastCandidateChunkCount +
                      " chunkSize=" + marchingCubesLab.Settings.chunkRange.chunkSize);
            return true;
        }

        private void CollectRuntimeChunkLodsFromCandidateChunks(
            in PlanetRecipe renderRecipe,
            in PlanetPlacement placement)
        {
            runtimeChunkLods.Clear();
            int candidateCount = Mathf.Max(0, (int)marchingCubesLab.LastCandidateChunkCount);
            runtimeCandidateChunkCount = candidateCount;
            float activeChunkSize = Mathf.Max(1, marchingCubesLab.Settings.chunkRange.chunkSize);
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
                runtimeChunkLods.Add(new PlanetChunkLodRuntimeEntry(chunkId, centerWorld));
            }
            Debug.Log(LogPrefix + "Collect runtime chunks end count=" + runtimeChunkLods.Count);
        }

        private void ClearRuntimeChunkLods()
        {
            runtimeChunkLods.Clear();
            hasRuntimeChunkLods = false;
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
