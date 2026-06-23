using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using MarchingCubesPlanet.VoxelEngine.Data;
using MarchingCubesPlanet.VoxelEngine.MarchingCubes;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    [DisallowMultipleComponent]
    public sealed partial class PlanetManager : MonoBehaviour
    {
        private const int DefaultStartupFarChunkBuildsPerFrame = 8;
        private const int DefaultSegmentLodChunksBuiltPerFrame = 16;

        [SerializeField] private PlayerChunkTracker playerChunkTracker;
        [SerializeField] private VoxelSphereGenerator sphereGenerator;
        [SerializeField] private Transform fallbackAnchor;
        public string PlanetID;
        [Header("Planet")]
        [Min(0.01f)] public float Radius = 14f;
        public int Seed = 12345;
        [Min(0f)] public float ActionAreaRadiusPadding = 56f;
        [FormerlySerializedAs("AtmosphereRadiusPadding"), Min(0f)] public float AtmosphereRadius = 28f;
        [Header("Debug")]
        [FormerlySerializedAs("drawSegmentGizmos")] public bool DrawGizmosSegments = true;
        public bool DrawActionAreaGizmo = true;
        public bool DrawAtmosphereGizmo = true;
        [SerializeField, Range(0f, 90f)] private float farHemisphereRefreshAngle = 3f;
        [SerializeField, HideInInspector] private bool planetShapeInitialized;
        [SerializeField, HideInInspector] private bool atmosphereInitialized;

        private readonly PlanetChunkBehaviour chunkBehaviour = new PlanetChunkBehaviour();
        private readonly PlanetCombinedMeshBehaviour combinedMeshBehaviour = new PlanetCombinedMeshBehaviour();
        private readonly PlanetSegmentSeamBehaviour segmentSeamBehaviour = new PlanetSegmentSeamBehaviour();
        private bool hasPlanetActionRadiusState;
        private bool isInsidePlanetActionRadius = true;
        private bool hasAtmosphereState;
        private bool isPlayerInsideAtmosphere;
        private Coroutine startupCoroutine;
        private bool startupInProgress;
        private bool startupDone;
        private int segmentLodChunksBuiltPerFrame = DefaultSegmentLodChunksBuiltPerFrame;
        private string activeSystemId;
        private PlanetData planetData = new PlanetData();
        private MarchingCubesCaseTable caseTable;
        private VoxelEngineConfig config;

        public static event Action<PlanetManager, bool> PlayerAtmosphereStateChanged;

        public PlanetData PlanetData => planetData;
        public string ResolvedPlanetID => ResolvePlanetId();
        public bool StartupDone => startupDone;
        public bool IsNearCombinedRenderingActive => IsNearCombinedRenderingReady();
        public bool IsVisibleMeshRenderingActive => IsVisibleCombinedMeshRenderingActive();
        public int NearSegmentCount => config != null
            ? config.NearCombinedMeshBucketCount
            : PlanetRenderConstants.MaxCombinedMeshBucketCount;
        private int MaxDeferredSegmentLodChunksBuiltPerFrame => segmentLodChunksBuiltPerFrame > 0
            ? segmentLodChunksBuiltPerFrame
            : config != null
            ? config.MaxDeferredSegmentLodChunksBuiltPerFrame
            : VoxelEngineConfig.MinDeferredSegmentLodChunksBuiltPerFrame;

        private void OnEnable()
        {
            InitializePlanetShapeFromSphereGeneratorIfNeeded();
            InitializeAtmosphereFromRadiusIfNeeded();
            ApplyPlanetShapeToSphereGenerator();
            EnsureConfig();
            EnsureCaseTable();
            hasPlanetActionRadiusState = false;
            hasAtmosphereState = false;
            EnsureCombinedRenderer();
        }

        private void Reset()
        {
            TryGetComponent(out sphereGenerator);
            fallbackAnchor = transform;
            InitializePlanetShapeFromSphereGeneratorIfNeeded();
            InitializeAtmosphereFromRadiusIfNeeded();
            ApplyPlanetShapeToSphereGenerator();
        }

        private void OnValidate()
        {
            InitializePlanetShapeFromSphereGeneratorIfNeeded();
            InitializeAtmosphereFromRadiusIfNeeded();
            Radius = Mathf.Max(0.01f, Radius);
            ActionAreaRadiusPadding = Mathf.Max(0f, ActionAreaRadiusPadding);
            AtmosphereRadius = Mathf.Max(0f, AtmosphereRadius);
            farHemisphereRefreshAngle = Mathf.Clamp(farHemisphereRefreshAngle, 0f, 90f);
            ApplyPlanetShapeToSphereGenerator();
        }

        private void Update()
        {
            RefreshAtmosphereState();

            if (startupInProgress)
            {
                return;
            }

            RefreshPlanetActionRadiusState();
            EnsureCombinedRenderer();
            TickChunkBehaviour();
            UpdateNearSegmentVisibility();
            TickCombinedMeshBehaviour();
        }

        public void Configure(
            VoxelEngineConfig nextConfig,
            PlayerChunkTracker nextPlayerChunkTracker,
            VoxelSphereGenerator nextSphereGenerator,
            Transform nextFallbackAnchor)
        {
            config = nextConfig;
            playerChunkTracker = nextPlayerChunkTracker;
            sphereGenerator = nextSphereGenerator;
            fallbackAnchor = nextFallbackAnchor;
            InitializePlanetShapeFromSphereGeneratorIfNeeded();
            InitializeAtmosphereFromRadiusIfNeeded();
            ApplyPlanetShapeToSphereGenerator();
        }

        private void OnDisable()
        {
            if (startupCoroutine != null)
            {
                StopCoroutine(startupCoroutine);
                startupCoroutine = null;
            }

            startupInProgress = false;
            startupDone = false;
            ClearChunks();
            DestroyCombinedMesh();
            if (caseTable.IsCreated)
            {
                caseTable.Dispose();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (DrawGizmosSegments)
            {
                DrawSegmentGizmos();
            }

            if (DrawActionAreaGizmo)
            {
                DrawActionAreaGizmos();
            }

            if (DrawAtmosphereGizmo)
            {
                DrawAtmosphereGizmos();
            }
        }

        private void DrawSegmentGizmos()
        {
            EnsureConfig();

            int segmentCount = Mathf.Clamp(
                config != null ? config.NearCombinedMeshBucketCount : PlanetRenderConstants.MaxCombinedMeshBucketCount,
                1,
                PlanetRenderConstants.MaxCombinedMeshBucketCount);
            if (!TryGetCombinedMeshBucketGrid(segmentCount, out int3 grid))
            {
                return;
            }

            float radius = sphereGenerator != null
                ? Mathf.Max(0.01f, sphereGenerator.MaximumTerrainRadius)
                : 1f;
            Vector3 center = GetSpherePositionInManagerLocal();
            Vector3 size = new Vector3(
                (radius * 2f) / grid.x,
                (radius * 2f) / grid.y,
                (radius * 2f) / grid.z);
            int focusSegment = GetSegmentIndexForWorldPosition(GetCurrentDetailFocusVector3(), segmentCount);

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
            Gizmos.DrawWireCube(center, Vector3.one * radius * 2f);

            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                Vector3 segmentCenter = GetSegmentLocalCenter(segmentIndex, segmentCount);
                Gizmos.color = segmentIndex == focusSegment
                    ? new Color(0f, 1f, 0.25f, 0.95f)
                    : new Color(0.1f, 0.65f, 1f, 0.45f);
                Gizmos.DrawWireCube(segmentCenter, size);

                Gizmos.color = segmentIndex == focusSegment
                    ? new Color(0f, 1f, 0.25f, 1f)
                    : new Color(1f, 0.9f, 0.1f, 0.85f);
                Gizmos.DrawSphere(segmentCenter, radius * 0.0125f);
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        private void DrawActionAreaGizmos()
        {
            EnsureConfig();
            if (!ShouldUsePlanetActionRadius())
            {
                return;
            }

            float actionRadius = Radius + ActionAreaRadiusPadding;
            if (float.IsNaN(actionRadius) || float.IsInfinity(actionRadius) || actionRadius <= 0f)
            {
                return;
            }

            Color previousColor = Gizmos.color;
            Gizmos.color = isInsidePlanetActionRadius
                ? new Color(0.25f, 1f, 0.25f, 0.45f)
                : new Color(1f, 0.35f, 0.15f, 0.45f);
            Gizmos.DrawWireSphere(GetSpherePosition(), actionRadius);
            Gizmos.color = previousColor;
        }

        private void DrawAtmosphereGizmos()
        {
            float atmosphereRadius = AtmosphereRadius;
            if (float.IsNaN(atmosphereRadius) || float.IsInfinity(atmosphereRadius) || atmosphereRadius <= 0f)
            {
                return;
            }

            Color previousColor = Gizmos.color;
            Gizmos.color = isPlayerInsideAtmosphere
                ? new Color(0.45f, 0.8f, 1f, 0.55f)
                : new Color(0.45f, 0.8f, 1f, 0.25f);
            Gizmos.DrawWireSphere(GetSpherePosition(), atmosphereRadius);
            Gizmos.color = previousColor;
        }

        [ContextMenu("Rebuild Active Chunks")]
        public void RebuildAroundCurrentAnchor()
        {
            EnsureConfig();
            EnsureCaseTable();
            RefreshPlanetActionRadiusState();
            int3 centerChunk = GetCurrentCenterChunk();
            RefreshDeclaredChunks();

            RebuildDesiredChunkSet();
            BuildDesiredChunksSynchronously(centerChunk);
            UpdateCombinedMesh(true);
            RefreshPlanetDataSnapshot();
        }

        [ContextMenu("Generate")]
        public void Generate()
        {
            if (Application.isPlaying)
            {
                StartStagedStartup(true, DefaultStartupFarChunkBuildsPerFrame);
                return;
            }

            ClearCombinedMesh();
            MarkAllChunksDirty();
            RebuildAroundCurrentAnchor();
        }

        public async Task Initialize(string stellarID)
        {
            activeSystemId = stellarID;
            if (startupDone)
            {
                return;
            }

            if (startupInProgress)
            {
                while (startupInProgress)
                {
                    await Task.Yield();
                }

                return;
            }

            startupInProgress = true;
            try
            {
                await InitializePlanetData();
                await InitializeFarMesh();
                await InitializePlanet();
                startupDone = true;
            }
            finally
            {
                startupInProgress = false;
            }
        }

        private async Task InitializePlanetData()
        {
            planetData = await GetSetPlanetData();
        }

        private async Task<PlanetData> GetSetPlanetData()
        {
            string url = GetPlanetDataUrl(activeSystemId);
            byte[] binary = FileManager.GetFile(url);
            if (binary != null)
            {
                PlanetData loadedPlanetData = PlanetData.FromBinary(binary);
                if (HasChunkMeshData(loadedPlanetData))
                {
                    return loadedPlanetData;
                }
            }

            PlanetData initializedPlanetData = await PlanetInitializer.InitializePlanet(this);
            FileManager.SaveFile(url, initializedPlanetData.ToBinary());
            return initializedPlanetData;
        }

        private async Task InitializeFarMesh()
        {
            EnsureVisibleCombinedMeshBucket();
            EnsureFarCombinedMeshBucket();

            string farMeshUrl = GetFarMeshUrl();
            byte[] binary = FileManager.GetFile(farMeshUrl);
            if (binary != null)
            {
                Mesh farMesh = MeshBinarySerializer.FromBinary(binary, "VoxelCombinedMesh_Far");
                DeliverFarCombinedMesh(farMesh);
                RebuildVisibleFarMesh();
                farCombinedMeshDirty = false;
                MarkCombinedRendererVisibilityDirty();
                ApplyCombinedRendererVisibility();
                return;
            }

            Mesh craftedFarMesh = await MeshCrafter.CraftFarMesh(planetData);
            DeliverFarCombinedMesh(craftedFarMesh);
            RebuildVisibleFarMesh();
            farCombinedMeshDirty = false;
            MarkCombinedRendererVisibilityDirty();
            ApplyCombinedRendererVisibility();
            FileManager.SaveFile(farMeshUrl, MeshBinarySerializer.ToBinary(craftedFarMesh));
            FileManager.SaveFile(GetPlanetDataUrl(activeSystemId), planetData.ToBinary());
        }

        private Task InitializePlanet()
        {
            ClearChunks();
            chunkBehaviour.ApplyPlanetDataToDeclaredChunks(planetData);
            chunkBehaviour.HydrateFromPlanetData(planetData, gameObject);

            MarkChunkVisibilityDirty();
            combinedMeshesDirty = true;
            combinedMeshLayoutDirty = true;
            farCombinedMeshDirty = false;
            return Task.CompletedTask;
        }

        private static bool HasChunkMeshData(PlanetData data)
        {
            if (data == null || data.chunks.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < data.chunks.Count; i++)
            {
                PlanetMeshData mesh = data.chunks[i].mesh;
                if (mesh != null && mesh.vertices.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        internal PlanetInitializationSettings CreateInitializationSettings()
        {
            EnsureConfig();
            ApplyPlanetShapeToSphereGenerator();

            int lodCount = Mathf.Clamp(config != null ? config.LodCount : PlanetRenderConstants.SegmentLodCount, 1, PlanetRenderConstants.SegmentLodCount);
            int activeLod = Mathf.Clamp(PlanetRenderConstants.ActiveSegmentLodIndex, 0, lodCount - 1);
            int requestedCellSize = config.GetCellSizeAtLod(activeLod);
            int segmentCount = Mathf.Clamp(
                config != null ? config.NearCombinedMeshBucketCount : PlanetRenderConstants.MaxCombinedMeshBucketCount,
                1,
                PlanetRenderConstants.MaxCombinedMeshBucketCount);
            TryGetCombinedMeshBucketGrid(segmentCount, out int3 segmentGrid);

            return new PlanetInitializationSettings
            {
                stellarID = ResolveSystemId(activeSystemId),
                planetID = ResolvePlanetId(),
                worldPosition = VoxelRuntimeMath.ToFloat3(GetSpherePosition()),
                radius = Radius,
                seed = Seed,
                chunkSize = config.ChunkSize,
                cellSize = ChunkBuilder.NormalizeCellSizeForChunk(requestedCellSize, config.ChunkSize),
                detailFocusKey = GetCurrentDetailFocusKey(),
                segmentCount = segmentCount,
                segmentGrid = segmentGrid,
                worldToPlanetLocal = transform.worldToLocalMatrix,
                sphereLocalPosition = VoxelRuntimeMath.ToFloat3(GetSpherePositionInManagerLocal()),
                maximumTerrainRadius = sphereGenerator != null ? Mathf.Max(0.01f, sphereGenerator.MaximumTerrainRadius) : Radius,
                scalarField = GetScalarFieldSettings()
            };
        }

        internal void CollectDeclaredChunksForInitialization(HashSet<int3> target)
        {
            EnsureConfig();
            ApplyPlanetShapeToSphereGenerator();
            target.Clear();
            if (sphereGenerator != null)
            {
                sphereGenerator.CollectOccupiedChunks(target, config.ChunkSize);
            }
        }

        [ContextMenu("Clear Generated Chunks")]
        public void ClearGeneratedChunks()
        {
            ClearChunks();
        }

        private void StartStagedStartup(bool clearExisting, int startupFarChunkBuildsPerFrame)
        {
            if (startupCoroutine != null)
            {
                StopCoroutine(startupCoroutine);
            }

            segmentLodChunksBuiltPerFrame = DefaultSegmentLodChunksBuiltPerFrame;
            startupCoroutine = StartCoroutine(RunStagedStartup(clearExisting, startupFarChunkBuildsPerFrame, true));
        }

        private IEnumerator RunStagedStartup(
            bool clearExisting,
            int startupFarChunkBuildsPerFrame,
            bool refreshDeclaredChunks)
        {
            startupInProgress = true;
            startupDone = false;

            EnsureConfig();
            EnsureCaseTable();
            EnsureCombinedRenderer();

            if (clearExisting)
            {
                ClearCombinedMesh();
                MarkAllChunksDirty();
            }

            RefreshPlanetActionRadiusState();
            int3 centerChunk = GetCurrentCenterChunk();
            bool loadedStartupFar = false;

            if (refreshDeclaredChunks)
            {
                RefreshDeclaredChunks();
                RefreshPlanetDataSnapshot();
            }
            else
            {
                chunkBehaviour.ApplyPlanetDataToDeclaredChunks(planetData);
                loadedStartupFar = TryLoadStartupFarMeshFromDisk();
            }

            yield return null;

            if (refreshDeclaredChunks || !TryRebuildDesiredChunkSetFromPlanetData())
            {
                RebuildDesiredChunkSet();
            }

            if (refreshDeclaredChunks)
            {
                RefreshPlanetDataBuildStateFromDesiredStates();
            }

            yield return BuildDesiredChunksBudgeted(
                centerChunk,
                Mathf.Max(1, startupFarChunkBuildsPerFrame));

            RebuildFarCombinedMesh(refreshDeclaredChunks || !loadedStartupFar);
            MarkCombinedRendererVisibilityDirty();
            ApplyCombinedRendererVisibility();

            startupInProgress = false;
            startupDone = true;
            startupCoroutine = null;
        }

        public string GetPlanetDataUrl(string systemId)
        {
            return FileManager.CombineUrl("StellarSystems", ResolveSystemId(systemId), "Planets", ResolvePlanetId(), "PlanetData");
        }

        public string GetPlanetStorageUrl()
        {
            return FileManager.CombineUrl("StellarSystems", ResolveSystemId(activeSystemId), "Planets", ResolvePlanetId());
        }

        [ContextMenu("Clear Planet Data")]
        public void ClearPlanetData()
        {
            string planetStorageUrl = GetPlanetStorageUrl();
            bool deleted = FileManager.DeleteDirectory(planetStorageUrl);
            UnityEngine.Debug.Log(deleted
                ? $"Cleared planet data: {planetStorageUrl}"
                : $"No planet data found: {planetStorageUrl}", this);
        }

        private string GetFarMeshUrl()
        {
            return FileManager.CombineUrl(
                "StellarSystems",
                ResolveSystemId(activeSystemId),
                "Planets",
                ResolvePlanetId(),
                "Far",
                "far.meshbin");
        }

        private string GetSegmentLodMeshUrl(int segmentId, int lodIndex)
        {
            return FileManager.CombineUrl(
                "StellarSystems",
                ResolveSystemId(activeSystemId),
                "Planets",
                ResolvePlanetId(),
                "Segments",
                segmentId.ToString(),
                $"LOD_{lodIndex}.meshbin");
        }

        private string GetSegmentSeamMeshUrl(SegmentSeamKey key)
        {
            return FileManager.CombineUrl(
                "StellarSystems",
                ResolveSystemId(activeSystemId),
                "Planets",
                ResolvePlanetId(),
                "Segments",
                "Seams",
                $"v{SegmentLodSeamBuilder.CacheVersion}",
                $"{key.segmentA}_{key.segmentB}_A{key.axis}",
                $"LOD_{key.lodA}_{key.lodB}.meshbin");
        }

        private bool TryLoadExistingPlanetDataOrThrow(string systemId)
        {
            string url = GetPlanetDataUrl(systemId);
            Stopwatch stopwatch = Stopwatch.StartNew();
            byte[] binary = FileManager.GetFile(url);
            if (binary == null)
            {
                return false;
            }

            try
            {
                planetData = PlanetData.FromBinary(binary);
                LogStartup($"PlanetData read from {url}. bytes={binary.Length}, declared={planetData.declaredChunks.Count}, chunks={planetData.chunks.Count}, elapsed={stopwatch.ElapsedMilliseconds}ms.");
            }
            catch (System.Exception exception)
            {
                string message = $"PlanetData could not be loaded for {ResolvePlanetId()} at {url}. {exception.Message}";
                UnityEngine.Debug.LogError(message, this);
                throw new System.InvalidOperationException(message, exception);
            }

            if (planetData == null || (planetData.declaredChunks.Count == 0 && planetData.chunks.Count == 0))
            {
                FailPlanetDataLoad($"PlanetData is empty or not functional for {ResolvePlanetId()} at {url}.");
            }

            return true;
        }

        private void ValidateLoadedPlanetDataBuiltFarOrThrow(string systemId)
        {
            if (IsFarCombinedMeshCached() && GetVisibleCombinedMeshVertexCount() > 0)
            {
                return;
            }

            string message =
                $"PlanetData loaded for {ResolvePlanetId()} at {GetPlanetDataUrl(systemId)}, " +
                "but it did not build Far mesh data. " +
                $"declaredChunks={declaredChunks.Count}, planetDataDeclared={planetData.declaredChunks.Count}, " +
                $"planetDataChunks={planetData.chunks.Count}, activeChunks={activeChunks.Count}, " +
                $"farVertices={GetFarCombinedMeshVertexCount()}, visibleVertices={GetVisibleCombinedMeshVertexCount()}, " +
                $"visibleRendering={IsVisibleCombinedMeshRenderingActive()}.";
            FailPlanetDataLoad(message);
        }

        private void FailPlanetDataLoad(string message)
        {
            UnityEngine.Debug.LogError(message, this);
            throw new System.InvalidOperationException(message);
        }

        private void SavePlanetData(string systemId)
        {
            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                byte[] binary = planetData.ToBinary();
                FileManager.SaveFile(GetPlanetDataUrl(systemId), binary);
                LogStartup($"PlanetData saved. bytes={binary.Length}, declared={planetData.declaredChunks.Count}, chunks={planetData.chunks.Count}, elapsed={stopwatch.ElapsedMilliseconds}ms.");
            }
            catch (System.Exception exception)
            {
                UnityEngine.Debug.LogWarning($"Could not save PlanetData for {ResolvePlanetId()}. {exception.Message}", this);
            }
        }

        private void LogStartup(string message)
        {
            UnityEngine.Debug.Log($"[PlanetStartup:{ResolvePlanetId()}] {message}", this);
        }

        private string ResolvePlanetId()
        {
            return string.IsNullOrWhiteSpace(PlanetID) ? gameObject.name : PlanetID.Trim();
        }

        private static string ResolveSystemId(string systemId)
        {
            return string.IsNullOrWhiteSpace(systemId) ? "DefaultSystem" : systemId.Trim();
        }

        private IEnumerator BuildDesiredChunksBudgeted(int3 centerChunk, int maxChunksPerFrame)
        {
            return chunkBehaviour.BuildDesiredChunksBudgeted(
                centerChunk,
                maxChunksPerFrame,
                config.ChunkSize,
                GetScalarFieldSettings(),
                caseTable,
                gameObject,
                MarkCombinedMeshDirty,
                MarkChunkVisibilityDirty);
        }

        public void DeclareChunk(int3 chunkCoord)
        {
            chunkBehaviour.DeclareChunk(chunkCoord);
        }

        public void ReleaseChunk(int3 chunkCoord)
        {
            chunkBehaviour.ReleaseChunk(chunkCoord);
        }

        public void ClearDeclaredChunks()
        {
            chunkBehaviour.ClearDeclaredChunks();
            RefreshPlanetDataSnapshot();
        }

        public void DeclareChunkBounds(int3 minInclusive, int3 maxInclusive)
        {
            chunkBehaviour.DeclareChunkBounds(minInclusive, maxInclusive);
        }

        public bool IsChunkDeclared(int3 chunkCoord)
        {
            return chunkBehaviour.IsChunkDeclared(chunkCoord);
        }

        public GameObject GetChunkObjectOrNull(int3 chunkCoord)
        {
            return chunkBehaviour.GetChunkObjectOrNull(chunkCoord);
        }

        public Transform GetNearSegmentTransformOrNull(int segmentIndex)
        {
            if (segmentIndex < 0)
            {
                return null;
            }

            EnsureNearCombinedMeshBucketCount(
                config != null ? config.NearCombinedMeshBucketCount : PlanetRenderConstants.MaxCombinedMeshBucketCount);
            if (segmentIndex >= nearCombinedMeshBuckets.Count || nearCombinedMeshBuckets[segmentIndex].owner == null)
            {
                return null;
            }

            CombinedMeshBucket bucket = nearCombinedMeshBuckets[segmentIndex];
            EnsureSegmentLodCache(bucket);
            return bucket.lodCache != null ? bucket.lodCache.PivotTransform : bucket.owner.transform;
        }

        public Transform GetVisibleMeshTransformOrNull()
        {
            EnsureVisibleCombinedMeshBucket();
            return visibleCombinedMeshBucket != null && visibleCombinedMeshBucket.owner != null
                ? visibleCombinedMeshBucket.owner.transform
                : null;
        }

        public int GetNearSegmentIndexForWorldPosition(Vector3 worldPosition)
        {
            return GetSegmentIndexForWorldPosition(
                worldPosition,
                Mathf.Clamp(
                    config != null ? config.NearCombinedMeshBucketCount : PlanetRenderConstants.MaxCombinedMeshBucketCount,
                    1,
                    PlanetRenderConstants.MaxCombinedMeshBucketCount));
        }

        private void RebuildDesiredChunkSet()
        {
            chunkBehaviour.RebuildDesiredChunkSet(
                GetCurrentDetailFocusKey(),
                ResolveCellSizeForChunk,
                MarkCombinedMeshDirty,
                MarkChunkVisibilityDirty);
        }

        private bool TryRebuildDesiredChunkSetFromPlanetData()
        {
            return chunkBehaviour.TryRebuildDesiredChunkSetFromPlanetData(
                planetData,
                MarkCombinedMeshDirty,
                MarkChunkVisibilityDirty);
        }

        private void BuildDesiredChunksSynchronously(int3 centerChunk)
        {
            chunkBehaviour.BuildDesiredChunksSynchronously(
                centerChunk,
                config.ChunkSize,
                GetScalarFieldSettings(),
                caseTable,
                gameObject,
                MarkCombinedMeshDirty,
                MarkChunkVisibilityDirty);
        }

        private void TickChunkBehaviour()
        {
            chunkBehaviour.Tick(
                useNearCombinedMeshes || ShouldThrottlePlanetUpdatesOutsideActionRadius(),
                ShouldCullRenderedChunks(),
                playerChunkTracker != null ? playerChunkTracker.ChunkCullingCamera : null,
                MarkCombinedMeshDirty,
                MarkChunkVisibilityChanged);
        }

        private void TickCombinedMeshBehaviour()
        {
            combinedMeshBehaviour.Tick(
                ProcessDeferredSegmentLodBuilds,
                RefreshFarHemisphereIfNeeded,
                UpdateCombinedMeshForView);
        }

        private void UpdateNearSegmentVisibility()
        {
            if (!useNearCombinedMeshes || !nearCombinedMeshesBuiltOnce)
            {
                return;
            }

            int3 currentFocusKey = GetCurrentDetailFocusKey();
            bool shouldCull = ShouldCullRenderedChunks();
            if (!combinedRendererVisibilityDirty
                && hasLastNearVisibilityFocusKey
                && currentFocusKey.Equals(lastNearVisibilityFocusKey)
                && shouldCull == lastNearVisibilityCullRenderedChunks)
            {
                return;
            }

            lastNearVisibilityFocusKey = currentFocusKey;
            hasLastNearVisibilityFocusKey = true;
            lastNearVisibilityCullRenderedChunks = shouldCull;
            MarkCombinedRendererVisibilityDirty();
            ApplyCombinedRendererVisibility();
        }

        private bool UseChunkCulling()
        {
            return playerChunkTracker != null
                && playerChunkTracker.EnableChunkCulling;
        }

        private bool ShouldUsePlanetActionRadius()
        {
            return (config == null || config.UsePlanetActionRadius) && sphereGenerator != null && config != null;
        }

        private bool RefreshAtmosphereState()
        {
            bool nextInside = IsCurrentFocusInsideAtmosphere();
            if (!hasAtmosphereState)
            {
                isPlayerInsideAtmosphere = nextInside;
                hasAtmosphereState = true;
                if (nextInside)
                {
                    PlayerAtmosphereStateChanged?.Invoke(this, true);
                    return true;
                }

                return false;
            }

            if (hasAtmosphereState && nextInside == isPlayerInsideAtmosphere)
            {
                return false;
            }

            isPlayerInsideAtmosphere = nextInside;
            hasAtmosphereState = true;
            PlayerAtmosphereStateChanged?.Invoke(this, nextInside);
            return true;
        }

        private bool IsCurrentFocusInsideAtmosphere()
        {
            float atmosphereRadius = Mathf.Max(0f, AtmosphereRadius);
            if (atmosphereRadius <= 0f)
            {
                return false;
            }

            Vector3 focus = GetCurrentDetailFocusVector3();
            return (focus - GetSpherePosition()).sqrMagnitude <= atmosphereRadius * atmosphereRadius;
        }

        private bool RefreshPlanetActionRadiusState()
        {
            bool nextInside = IsCurrentFocusInsidePlanetActionRadius();
            if (hasPlanetActionRadiusState && nextInside == isInsidePlanetActionRadius)
            {
                return false;
            }

            isInsidePlanetActionRadius = nextInside;
            hasPlanetActionRadiusState = true;
            combinedMeshLayoutDirty = true;
            MarkCombinedRendererVisibilityDirty();
            if (nextInside)
            {
                MarkAllNearCombinedMeshesDirty();
            }
            else
            {
                MarkChunkVisibilityDirty();
                combinedMeshesDirty = true;
                farCombinedMeshDirty = true;
            }

            return true;
        }

        private bool IsCurrentFocusInsidePlanetActionRadius()
        {
            if (!ShouldUsePlanetActionRadius())
            {
                return true;
            }

            return IsCurrentFocusInsideSegmentLodActivationRadius();
        }

        private bool IsCurrentFocusInsideSegmentLodActivationRadius()
        {
            if (sphereGenerator == null)
            {
                return true;
            }

            Vector3 focus = GetCurrentDetailFocusVector3();
            float effectiveRadius = Mathf.Max(0f, Radius + ActionAreaRadiusPadding);
            return (focus - GetSpherePosition()).sqrMagnitude <= effectiveRadius * effectiveRadius;
        }

        private bool ShouldThrottlePlanetUpdatesOutsideActionRadius()
        {
            return ShouldUsePlanetActionRadius()
                && hasPlanetActionRadiusState
                && !isInsidePlanetActionRadius;
        }

        private bool ShouldCullRenderedChunks()
        {
            return (config == null || config.UseChunkCullingForRendering) && UseChunkCulling();
        }

        private int CountVisibleChunks()
        {
            return chunkBehaviour.CountVisibleChunks(ShouldCullRenderedChunks());
        }

        private void MarkChunkVisibilityDirty()
        {
            chunkBehaviour.MarkVisibilityDirty();
            MarkCombinedRendererVisibilityDirty();
        }

        private void MarkChunkVisibilityChanged()
        {
            combinedMeshesDirty = true;
            MarkCombinedRendererVisibilityDirty();
        }

        private Vector3 GetCurrentDetailFocusVector3()
        {
            float3 focus = GetCurrentDetailFocus();
            return new Vector3(focus.x, focus.y, focus.z);
        }

        private void ClearChunks()
        {
            chunkBehaviour.ClearChunks();
            MarkChunkVisibilityDirty();
            ClearCombinedMesh();
        }

        private void RefreshPlanetDataSnapshot()
        {
            planetData.declaredChunks = new HashSet<int3>(declaredChunks);
            planetData.chunks.Clear();
            foreach (int3 chunkCoord in declaredChunks)
            {
                planetData.chunks.Add(new PlanetChunkBuildData
                {
                    coord = chunkCoord,
                    segmentId = GetSegmentIndexForChunkCoord(
                        chunkCoord,
                        Mathf.Clamp(
                            config != null ? config.NearCombinedMeshBucketCount : PlanetRenderConstants.MaxCombinedMeshBucketCount,
                            1,
                            PlanetRenderConstants.MaxCombinedMeshBucketCount))
                });
            }
        }

        private void RefreshPlanetDataBuildStateFromDesiredStates()
        {
            int segmentCount = Mathf.Clamp(
                config != null ? config.NearCombinedMeshBucketCount : PlanetRenderConstants.MaxCombinedMeshBucketCount,
                1,
                PlanetRenderConstants.MaxCombinedMeshBucketCount);
            planetData.declaredChunks = new HashSet<int3>(declaredChunks);
            planetData.chunks.Clear();
            foreach (KeyValuePair<int3, DesiredChunkState> pair in desiredChunkStates)
            {
                planetData.chunks.Add(new PlanetChunkBuildData
                {
                    coord = pair.Key,
                    segmentId = GetSegmentIndexForChunkCoord(pair.Key, segmentCount),
                    cellSize = pair.Value.cellSize,
                    detailFocusKey = pair.Value.detailFocusKey
                });
            }
        }

        private void RefreshDeclaredChunks()
        {
            ApplyPlanetShapeToSphereGenerator();
            if (sphereGenerator != null)
            {
                sphereGenerator.DeclareOccupiedChunks(this, config.ChunkSize);
            }
        }

        private void MarkAllChunksDirty()
        {
            chunkBehaviour.MarkAllChunksDirty();
        }

        private ScalarFieldSettings GetScalarFieldSettings()
        {
            ApplyPlanetShapeToSphereGenerator();
            return sphereGenerator != null
                ? sphereGenerator.BuildScalarFieldSettings()
                : config.ScalarFieldSettings;
        }

        private void InitializePlanetShapeFromSphereGeneratorIfNeeded()
        {
            if (planetShapeInitialized)
            {
                return;
            }

            if (sphereGenerator == null)
            {
                TryGetComponent(out sphereGenerator);
            }

            if (sphereGenerator != null)
            {
                Radius = Mathf.Max(0.01f, sphereGenerator.Radius);
                Seed = sphereGenerator.Seed;
            }

            ActionAreaRadiusPadding = Mathf.Max(0f, Radius * 4f);
            planetShapeInitialized = true;
        }

        private void InitializeAtmosphereFromRadiusIfNeeded()
        {
            if (atmosphereInitialized)
            {
                return;
            }

            AtmosphereRadius = Mathf.Max(0f, Radius * 2f);
            atmosphereInitialized = true;
        }

        private void ApplyPlanetShapeToSphereGenerator()
        {
            if (sphereGenerator == null)
            {
                return;
            }

            sphereGenerator.ConfigurePlanetShape(Radius, Seed);
        }

        private Vector3 GetSpherePosition()
        {
            return sphereGenerator != null ? sphereGenerator.transform.position : Vector3.zero;
        }

        private Vector3 GetSpherePositionInManagerLocal()
        {
            return sphereGenerator != null ? transform.InverseTransformPoint(sphereGenerator.transform.position) : Vector3.zero;
        }

        private static void DestroyUnityObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
                return;
            }

            DestroyImmediate(target);
        }

        private int3 GetCurrentCenterChunk()
        {
            Vector3 position = playerChunkTracker != null
                ? playerChunkTracker.TrackedPosition
                : (fallbackAnchor != null ? fallbackAnchor.position : transform.position);
            return VoxelChunkUtility.GetChunkCoords(new float3(position.x, position.y, position.z), config.ChunkSize);
        }

        private float3 GetCurrentDetailFocus()
        {
            if (playerChunkTracker != null)
            {
                Vector3 trackedPosition = playerChunkTracker.TrackedPosition;
                return new float3(trackedPosition.x, trackedPosition.y, trackedPosition.z);
            }

            Transform anchor = fallbackAnchor != null ? fallbackAnchor : transform;
            Vector3 position = anchor.position;
            return new float3(position.x, position.y, position.z);
        }

        private int3 GetCurrentDetailFocusKey()
        {
            int quantization = math.max(1, math.min(config.ChunkSize.x, math.min(config.ChunkSize.y, config.ChunkSize.z)));
            return (int3)math.floor(GetCurrentDetailFocus() / quantization);
        }

        private void EnsureConfig()
        {
            if (config != null)
            {
                return;
            }

            config = Resources.Load<VoxelEngineConfig>(VoxelEngineConfig.ResourceName);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<VoxelEngineConfig>();
            }
        }

        private void EnsureCaseTable()
        {
            if (!caseTable.IsCreated)
            {
                caseTable = MarchingCubesCaseTableBuilder.Build(Allocator.Persistent);
            }
        }

    }
}
