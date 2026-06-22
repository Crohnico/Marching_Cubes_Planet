using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using MarchingCubesPlanet.VoxelEngine.Data;
using MarchingCubesPlanet.VoxelEngine.Jobs;
using MarchingCubesPlanet.VoxelEngine.MarchingCubes;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    [DisallowMultipleComponent]
    public sealed partial class PlanetManager : MonoBehaviour
    {
        private const int MaxVerticesPerCell = 36;
        private const int InteriorSubMesh = 0;
        private const int TransitionSubMesh = 1;
        private const int SurfaceSubMesh = 2;
        private const int LayerSubMeshCount = 3;
        private const int MaxCombinedMeshBucketCount = VoxelEngineConfig.MaxCombinedMeshBucketCount;
        private const int SegmentLodCount = 3;
        private const int ActiveSegmentLodIndex = 2;
        private const int DefaultStartupFarChunkBuildsPerFrame = 8;
        private const int DefaultSegmentLodChunksBuiltPerFrame = 16;
        private static readonly ProfilerMarker RebuildDesiredMarker = new ProfilerMarker("VoxelEngine.RebuildDesiredChunks");
        private static readonly ProfilerMarker BuildRequestsMarker = new ProfilerMarker("VoxelEngine.BuildCellRequests");
        private static readonly ProfilerMarker StartChunkBuildMarker = new ProfilerMarker("VoxelEngine.StartChunkBuild");
        private static readonly ProfilerMarker CompleteChunkBuildMarker = new ProfilerMarker("VoxelEngine.CompleteChunkBuild");
        private static readonly ProfilerMarker UploadMeshMarker = new ProfilerMarker("VoxelEngine.UploadMesh");
        private static readonly ProfilerMarker CombineMeshMarker = new ProfilerMarker("VoxelEngine.CombineMesh");
        private static readonly ProfilerMarker UpdateVisibilityMarker = new ProfilerMarker("VoxelEngine.UpdateChunkVisibility");
        private static readonly ProfilerMarker BuildCombineInstancesMarker = new ProfilerMarker("VoxelEngine.BuildCombineInstances");
        private static readonly ProfilerMarker CombineBucketMeshMarker = new ProfilerMarker("VoxelEngine.CombineBucketMesh");

        [SerializeField] private PlayerChunkTracker playerChunkTracker;
        [SerializeField] private VoxelSphereGenerator sphereGenerator;
        [SerializeField] private Transform fallbackAnchor;
        public string PlanetID;
        [Header("Debug")]
        [SerializeField] private bool drawSegmentGizmos = true;
        [SerializeField, Range(0f, 90f)] private float farHemisphereRefreshAngle = 3f;

        private readonly Dictionary<int3, VoxelChunkState> activeChunks = new Dictionary<int3, VoxelChunkState>();
        private readonly Dictionary<int3, int> declaredChunkRefCounts = new Dictionary<int3, int>();
        private readonly Dictionary<int3, DesiredChunkState> desiredChunkStates = new Dictionary<int3, DesiredChunkState>();
        private readonly HashSet<int3> declaredChunks = new HashSet<int3>();
        private readonly HashSet<int3> desiredChunks = new HashSet<int3>();
        private readonly List<int3> scratchChunkCoords = new List<int3>();
        private readonly List<CombineInstance> combineInstances = new List<CombineInstance>();
        private readonly List<Vector3> meshUploadVertices = new List<Vector3>(65536);
        private readonly List<Vector3> meshUploadNormals = new List<Vector3>(65536);
        private readonly List<Vector2> meshUploadUvs = new List<Vector2>(65536);
        private readonly List<int> meshUploadInteriorIndices = new List<int>(65536);
        private readonly List<int> meshUploadTransitionIndices = new List<int>(65536);
        private readonly List<int> meshUploadSurfaceIndices = new List<int>(65536);
        private readonly List<VoxelCellBuildRequest> cellRequestBuffer = new List<VoxelCellBuildRequest>(32768);
        private readonly List<Mesh> scratchSegmentLodChunkMeshes = new List<Mesh>();
        private readonly List<int3> deferredSegmentLodChunkCoords = new List<int3>();
        private readonly List<int3> deferredSegmentLodBuiltChunkCoords = new List<int3>();
        private readonly List<Mesh> deferredSegmentLodChunkMeshes = new List<Mesh>();
        private readonly List<DeferredSegmentLodKey> deferredSegmentLodBuildQueue = new List<DeferredSegmentLodKey>();
        private readonly HashSet<DeferredSegmentLodKey> queuedDeferredSegmentLodBuilds = new HashSet<DeferredSegmentLodKey>();
        private readonly List<CombinedMeshBucket> nearCombinedMeshBuckets = new List<CombinedMeshBucket>();
        private bool combinedMeshesDirty = true;
        private bool combinedMeshLayoutDirty = true;
        private bool combinedRendererVisibilityDirty = true;
        private bool farCombinedMeshDirty = true;
        private bool useNearCombinedMeshes;
        private bool nearCombinedMeshesBuiltOnce;
        private bool combinedMeshRenderModeInitialized;
        private int3 lastNearVisibilityFocusKey;
        private bool hasLastNearVisibilityFocusKey;
        private bool lastNearVisibilityCullRenderedChunks;
        private int activeCombinedMeshBucketCount;
        private CombinedMeshBucket visibleCombinedMeshBucket;
        private CombinedMeshBucket farCombinedMeshBucket;
        private GameObject nearMeshesRoot;
        private Vector3 lastFarHemisphereDirection;
        private bool hasLastFarHemisphereDirection;
        private readonly Plane[] chunkCullingFrustumPlanes = new Plane[6];
        private bool chunkVisibilityDirty = true;
        private bool lastShouldCullRenderedChunks;
        private bool hasPlanetActionRadiusState;
        private bool isInsidePlanetActionRadius = true;
        private DeferredSegmentLodBuild activeDeferredSegmentLodBuild;
        private Coroutine startupCoroutine;
        private bool startupInProgress;
        private bool startupDone;
        private int segmentLodChunksBuiltPerFrame = DefaultSegmentLodChunksBuiltPerFrame;
        private string activeSystemId;
        private PlanetData planetData = new PlanetData();
        private MarchingCubesCaseTable caseTable;
        private VoxelEngineConfig config;

        public PlanetData PlanetData => planetData;
        public bool StartupDone => startupDone;
        public int DeclaredChunkCount => declaredChunks.Count;
        public int DesiredChunkCount => desiredChunkStates.Count;
        public int ActiveChunkCount => activeChunks.Count;
        public int VisibleChunkCount => CountVisibleChunks();
        public int CombinedVertexCount => GetCombinedVertexCount();
        public int CombinedTriangleCount => GetCombinedTriangleCount();
        public bool IsRenderCullingActive => ShouldCullRenderedChunks();
        public bool IsFarBridgeActive => IsFarBridgeVisible();
        public bool IsNearCombinedRenderingActive => useNearCombinedMeshes && nearCombinedMeshesBuiltOnce;
        public bool IsVisibleMeshRenderingActive => IsVisibleCombinedMeshRenderingActive();
        public int NearSegmentCount => NearCombinedMeshBucketCount;
        public Vector3 DetailFocusPosition => GetCurrentDetailFocusVector3();
        private Material TerrainMaterial => sphereGenerator != null ? sphereGenerator.TerrainMaterial : null;
        private bool UseChunkCullingForRendering => config == null || config.UseChunkCullingForRendering;
        private bool UsePlanetActionRadius => config == null || config.UsePlanetActionRadius;
        private int NearCombinedMeshBucketCount => config != null ? config.NearCombinedMeshBucketCount : MaxCombinedMeshBucketCount;
        private bool UseRadialLayerCulling => config == null || config.UseRadialLayerCulling;
        private int NeverLayerCullChunkDistance => config != null ? config.NeverLayerCullChunkDistance : 3;
        private int MaxDeferredSegmentLodChunksBuiltPerFrame => segmentLodChunksBuiltPerFrame > 0
            ? segmentLodChunksBuiltPerFrame
            : config != null
            ? config.MaxDeferredSegmentLodChunksBuiltPerFrame
            : VoxelEngineConfig.MinDeferredSegmentLodChunksBuiltPerFrame;

        private void OnEnable()
        {
            EnsureConfig();
            EnsureCaseTable();
            hasPlanetActionRadiusState = false;
            EnsureCombinedRenderer();
        }

        private void Reset()
        {
            TryGetComponent(out sphereGenerator);
            fallbackAnchor = transform;
        }

        private void OnValidate()
        {
            farHemisphereRefreshAngle = Mathf.Clamp(farHemisphereRefreshAngle, 0f, 90f);
        }

        private void Update()
        {
            if (startupInProgress)
            {
                return;
            }

            RefreshPlanetActionRadiusState();
            EnsureCombinedRenderer();
            UpdateChunkVisibilityIfNeeded();
            UpdateNearSegmentVisibility();
            ProcessDeferredSegmentLodBuilds();
            RefreshFarHemisphereIfNeeded();
            UpdateCombinedMeshForView();
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
            if (!drawSegmentGizmos)
            {
                return;
            }

            int segmentCount = Mathf.Clamp(NearCombinedMeshBucketCount, 1, MaxCombinedMeshBucketCount);
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

        public IEnumerator Initialize()
        {
            yield return Initialize(string.Empty, DefaultStartupFarChunkBuildsPerFrame, DefaultSegmentLodChunksBuiltPerFrame);
        }

        public IEnumerator Initialize(string systemId, int startupFarChunkBuildsPerFrame)
        {
            yield return Initialize(systemId, startupFarChunkBuildsPerFrame, DefaultSegmentLodChunksBuiltPerFrame);
        }

        public IEnumerator Initialize(string systemId, int startupFarChunkBuildsPerFrame, int segmentLodChunksBuiltPerFrame)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            activeSystemId = systemId;
            this.segmentLodChunksBuiltPerFrame = Mathf.Max(1, segmentLodChunksBuiltPerFrame);
            if (startupDone)
            {
                yield break;
            }

            if (startupInProgress)
            {
                while (startupInProgress)
                {
                    yield return null;
                }

                yield break;
            }

            LogStartup($"Initialize begin. system={ResolveSystemId(systemId)}, planet={ResolvePlanetId()}, farBudget={startupFarChunkBuildsPerFrame}, lodBudget={this.segmentLodChunksBuiltPerFrame}.");
            bool loadedPlanetData = TryLoadExistingPlanetDataOrThrow(systemId);
            LogStartup($"PlanetData {(loadedPlanetData ? "loaded" : "missing; will generate")} at {stopwatch.ElapsedMilliseconds}ms.");
            if (loadedPlanetData)
            {
                yield return RunStagedStartup(false, startupFarChunkBuildsPerFrame, false);
                ValidateLoadedPlanetDataBuiltFarOrThrow(systemId);
                LogStartup($"Initialize complete from disk in {stopwatch.ElapsedMilliseconds}ms.");
                yield break;
            }

            yield return RunStagedStartup(false, startupFarChunkBuildsPerFrame, true);
            SavePlanetData(systemId);
            LogStartup($"Initialize complete after generation in {stopwatch.ElapsedMilliseconds}ms.");
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
            Stopwatch stopwatch = Stopwatch.StartNew();
            startupInProgress = true;
            startupDone = false;
            LogStartup($"Staged startup begin. refreshDeclaredChunks={refreshDeclaredChunks}.");
            EnsureConfig();
            EnsureCaseTable();
            EnsureCombinedRenderer();
            LogStartup($"Core runtime ensured at {stopwatch.ElapsedMilliseconds}ms.");

            if (clearExisting)
            {
                ClearCombinedMesh();
                MarkAllChunksDirty();
            }

            RefreshPlanetActionRadiusState();
            int3 centerChunk = GetCurrentCenterChunk();

            if (refreshDeclaredChunks)
            {
                RefreshDeclaredChunks();
                RefreshPlanetDataSnapshot();
                LogStartup($"Declared chunks refreshed. declared={declaredChunks.Count}, planetDataChunks={planetData.chunks.Count}, elapsed={stopwatch.ElapsedMilliseconds}ms.");
            }
            else
            {
                ApplyPlanetDataToDeclaredChunks();
                LogStartup($"Declared chunks loaded from PlanetData. declared={declaredChunks.Count}, planetDataChunks={planetData.chunks.Count}, elapsed={stopwatch.ElapsedMilliseconds}ms.");
                if (TryLoadStartupFarMeshFromDisk())
                {
                    LogStartup($"Startup Far loaded from disk; skipped desired chunk build. activeChunks={activeChunks.Count}, elapsed={stopwatch.ElapsedMilliseconds}ms.");
                    startupInProgress = false;
                    startupDone = true;
                    startupCoroutine = null;
                    yield break;
                }
            }

            yield return null;

            if (refreshDeclaredChunks || !TryRebuildDesiredChunkSetFromPlanetData())
            {
                RebuildDesiredChunkSet();
                LogStartup($"Desired chunk set rebuilt from runtime. desired={desiredChunkStates.Count}, elapsed={stopwatch.ElapsedMilliseconds}ms.");
            }
            else
            {
                LogStartup($"Desired chunk set rebuilt from PlanetData. desired={desiredChunkStates.Count}, elapsed={stopwatch.ElapsedMilliseconds}ms.");
            }

            if (refreshDeclaredChunks)
            {
                RefreshPlanetDataBuildStateFromDesiredStates();
                LogStartup($"PlanetData build state populated from Far desired state. chunks={planetData.chunks.Count}, elapsed={stopwatch.ElapsedMilliseconds}ms.");
            }

            int chunksBeforeBuild = activeChunks.Count;
            yield return BuildDesiredChunksBudgeted(
                centerChunk,
                Mathf.Max(1, startupFarChunkBuildsPerFrame));
            LogStartup($"Desired chunks built. activeBefore={chunksBeforeBuild}, activeAfter={activeChunks.Count}, elapsed={stopwatch.ElapsedMilliseconds}ms.");

            RebuildFarCombinedMesh();
            LogStartup($"Far mesh ready. farVertices={GetFarCombinedMeshVertexCount()}, visibleVertices={GetVisibleCombinedMeshVertexCount()}, elapsed={stopwatch.ElapsedMilliseconds}ms.");
            MarkCombinedRendererVisibilityDirty();
            ApplyCombinedRendererVisibility();
            LogStartup($"Renderer visibility applied. visibleRendering={IsVisibleCombinedMeshRenderingActive()}, elapsed={stopwatch.ElapsedMilliseconds}ms.");

            startupInProgress = false;
            startupDone = true;
            startupCoroutine = null;
        }

        public string GetPlanetDataUrl(string systemId)
        {
            return FileManager.CombineUrl("StellarSystems", ResolveSystemId(systemId), "Planets", ResolvePlanetId(), "PlanetData");
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

        private void ApplyPlanetDataToDeclaredChunks()
        {
            declaredChunkRefCounts.Clear();
            declaredChunks.Clear();

            foreach (int3 chunkCoord in planetData.declaredChunks)
            {
                declaredChunks.Add(chunkCoord);
                declaredChunkRefCounts[chunkCoord] = 1;
            }

            if (declaredChunks.Count != 0)
            {
                return;
            }

            for (int i = 0; i < planetData.chunks.Count; i++)
            {
                int3 chunkCoord = planetData.chunks[i].coord;
                declaredChunks.Add(chunkCoord);
                declaredChunkRefCounts[chunkCoord] = 1;
            }
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
            scratchChunkCoords.Clear();
            foreach (KeyValuePair<int3, DesiredChunkState> pair in desiredChunkStates)
            {
                if (!IsChunkReady(pair.Key, pair.Value))
                {
                    scratchChunkCoords.Add(pair.Key);
                }
            }

            scratchChunkCoords.Sort((a, b) => CompareChunkCoordsByPriority(a, b, centerChunk));

            int chunksBuiltThisFrame = 0;
            for (int i = 0; i < scratchChunkCoords.Count; i++)
            {
                int3 chunkCoord = scratchChunkCoords[i];
                if (!desiredChunkStates.TryGetValue(chunkCoord, out DesiredChunkState desiredState)
                    || IsChunkReady(chunkCoord, desiredState))
                {
                    continue;
                }

                CompleteChunkBuild(StartChunkBuild(chunkCoord, desiredState));
                chunksBuiltThisFrame++;
                if (chunksBuiltThisFrame >= maxChunksPerFrame)
                {
                    chunksBuiltThisFrame = 0;
                    yield return null;
                }
            }
        }

        public void DeclareChunk(int3 chunkCoord)
        {
            if (declaredChunkRefCounts.TryGetValue(chunkCoord, out int refCount))
            {
                declaredChunkRefCounts[chunkCoord] = refCount + 1;
                return;
            }

            declaredChunkRefCounts.Add(chunkCoord, 1);
            declaredChunks.Add(chunkCoord);
        }

        public void ReleaseChunk(int3 chunkCoord)
        {
            if (!declaredChunkRefCounts.TryGetValue(chunkCoord, out int refCount))
            {
                return;
            }

            if (refCount > 1)
            {
                declaredChunkRefCounts[chunkCoord] = refCount - 1;
                return;
            }

            declaredChunkRefCounts.Remove(chunkCoord);
            declaredChunks.Remove(chunkCoord);
        }

        public void ClearDeclaredChunks()
        {
            declaredChunkRefCounts.Clear();
            declaredChunks.Clear();
            RefreshPlanetDataSnapshot();
        }

        public void DeclareChunkBounds(int3 minInclusive, int3 maxInclusive)
        {
            for (int x = minInclusive.x; x <= maxInclusive.x; x++)
            {
                for (int y = minInclusive.y; y <= maxInclusive.y; y++)
                {
                    for (int z = minInclusive.z; z <= maxInclusive.z; z++)
                    {
                        DeclareChunk(new int3(x, y, z));
                    }
                }
            }
        }

        public bool IsChunkDeclared(int3 chunkCoord)
        {
            return declaredChunks.Contains(chunkCoord);
        }

        public GameObject GetChunkObjectOrNull(int3 chunkCoord)
        {
            return activeChunks.TryGetValue(chunkCoord, out VoxelChunkState state)
                ? state.owner
                : null;
        }

        public Transform GetNearSegmentTransformOrNull(int segmentIndex)
        {
            if (segmentIndex < 0)
            {
                return null;
            }

            EnsureNearCombinedMeshBucketCount(NearCombinedMeshBucketCount);
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
            return GetSegmentIndexForWorldPosition(worldPosition, Mathf.Clamp(NearCombinedMeshBucketCount, 1, MaxCombinedMeshBucketCount));
        }

        private void RebuildDesiredChunkSet()
        {
            using (RebuildDesiredMarker.Auto())
            {
                desiredChunks.Clear();
                desiredChunkStates.Clear();
                int3 detailFocusKey = GetCurrentDetailFocusKey();

                foreach (int3 chunkCoord in declaredChunks)
                {
                    int cellSize = ResolveCellSizeForChunk(chunkCoord);
                    desiredChunks.Add(chunkCoord);
                    desiredChunkStates[chunkCoord] = new DesiredChunkState(cellSize, detailFocusKey);
                }

                MarkChunkVisibilityDirty();
                RemoveUndesiredChunks();
            }
        }

        private bool TryRebuildDesiredChunkSetFromPlanetData()
        {
            if (planetData == null || planetData.chunks.Count == 0)
            {
                return false;
            }

            desiredChunks.Clear();
            desiredChunkStates.Clear();
            for (int i = 0; i < planetData.chunks.Count; i++)
            {
                PlanetChunkBuildData chunk = planetData.chunks[i];
                if (chunk.cellSize <= 0)
                {
                    desiredChunks.Clear();
                    desiredChunkStates.Clear();
                    return false;
                }

                desiredChunks.Add(chunk.coord);
                desiredChunkStates[chunk.coord] = new DesiredChunkState(
                    chunk.cellSize,
                    chunk.detailFocusKey);
            }

            MarkChunkVisibilityDirty();
            RemoveUndesiredChunks();
            return true;
        }

        private void BuildDesiredChunksSynchronously(int3 centerChunk)
        {
            scratchChunkCoords.Clear();
            foreach (KeyValuePair<int3, DesiredChunkState> pair in desiredChunkStates)
            {
                if (!IsChunkReady(pair.Key, pair.Value))
                {
                    scratchChunkCoords.Add(pair.Key);
                }
            }

            scratchChunkCoords.Sort((a, b) => CompareChunkCoordsByPriority(a, b, centerChunk));

            for (int i = 0; i < scratchChunkCoords.Count; i++)
            {
                int3 chunkCoord = scratchChunkCoords[i];
                if (!desiredChunkStates.TryGetValue(chunkCoord, out DesiredChunkState desiredState)
                    || IsChunkReady(chunkCoord, desiredState))
                {
                    continue;
                }

                CompleteChunkBuild(StartChunkBuild(chunkCoord, desiredState));
            }
        }

        private bool IsChunkReady(int3 chunkCoord, DesiredChunkState desiredState)
        {
            return activeChunks.TryGetValue(chunkCoord, out VoxelChunkState state)
                && state.generated
                && !state.dirty
                && state.cellSize == desiredState.cellSize
                && state.detailFocusKey.Equals(desiredState.detailFocusKey);
        }

        private void UpdateChunkVisibilityIfNeeded()
        {
            if (useNearCombinedMeshes || ShouldThrottlePlanetUpdatesOutsideActionRadius())
            {
                chunkVisibilityDirty = false;
                lastShouldCullRenderedChunks = ShouldCullRenderedChunks();
                return;
            }

            bool shouldCull = ShouldCullRenderedChunks();
            if (!shouldCull && !lastShouldCullRenderedChunks)
            {
                chunkVisibilityDirty = false;
                return;
            }

            if (shouldCull != lastShouldCullRenderedChunks)
            {
                lastShouldCullRenderedChunks = shouldCull;
                MarkChunkVisibilityDirty();
            }

            if (!chunkVisibilityDirty)
            {
                return;
            }

            UpdateChunkVisibility();
            chunkVisibilityDirty = false;
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

        private void UpdateChunkVisibility()
        {
            using (UpdateVisibilityMarker.Auto())
            {
                bool visibilityChanged = false;
                bool shouldCull = ShouldCullRenderedChunks();
                if (shouldCull)
                {
                    GeometryUtility.CalculateFrustumPlanes(playerChunkTracker.ChunkCullingCamera, chunkCullingFrustumPlanes);
                }

                foreach (VoxelChunkState state in activeChunks.Values)
                {
                    bool inRange = true;
                    bool inFrustum = true;
                    int lastPlaneIndex = state.frustumLastPlaneIndex;
                    if (shouldCull)
                    {
                        inRange = IsChunkInCameraRange(state.chunkBounds, playerChunkTracker.ChunkCullingCamera);
                        inFrustum = TestAabbAgainstFrustumCoherent(
                            state.chunkBounds,
                            chunkCullingFrustumPlanes,
                            state.frustumLastPlaneIndex,
                            out lastPlaneIndex);
                    }

                    if (state.visible.inRange != inRange
                        || state.visible.inFrustum != inFrustum
                        || state.frustumLastPlaneIndex != lastPlaneIndex)
                    {
                        state.visible = new ChunkVisibility
                        {
                            inRange = inRange,
                            inFrustum = inFrustum
                        };
                        state.frustumLastPlaneIndex = lastPlaneIndex;
                        visibilityChanged = true;
                        MarkCombinedMeshDirty(state.chunkCoord);
                    }
                }

                if (visibilityChanged)
                {
                    combinedMeshesDirty = true;
                    MarkCombinedRendererVisibilityDirty();
                }
            }
        }

        private bool UseChunkCulling()
        {
            return playerChunkTracker != null
                && playerChunkTracker.EnableChunkCulling;
        }

        private bool ShouldUsePlanetActionRadius()
        {
            return UsePlanetActionRadius && sphereGenerator != null && config != null;
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
            float effectiveRadius = Mathf.Max(0f, sphereGenerator.Radius + GetMaximumConfiguredLodDistance());
            return (focus - GetSpherePosition()).sqrMagnitude <= effectiveRadius * effectiveRadius;
        }

        private float GetMaximumConfiguredLodDistance()
        {
            return config.GetMaxWorldDistanceForLod(GetSegmentLodCount() - 1);
        }

        private bool ShouldThrottlePlanetUpdatesOutsideActionRadius()
        {
            return ShouldUsePlanetActionRadius()
                && hasPlanetActionRadiusState
                && !isInsidePlanetActionRadius;
        }

        private bool ShouldCullRenderedChunks()
        {
            return UseChunkCullingForRendering && UseChunkCulling();
        }

        private static bool IsChunkInCameraRange(Bounds bounds, Camera camera)
        {
            Vector3 point = camera.transform.position;
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            float dx = Mathf.Max(Mathf.Abs(point.x - center.x) - extents.x, 0f);
            float dy = Mathf.Max(Mathf.Abs(point.y - center.y) - extents.y, 0f);
            float dz = Mathf.Max(Mathf.Abs(point.z - center.z) - extents.z, 0f);
            float farClip = camera.farClipPlane;
            return dx * dx + dy * dy + dz * dz <= farClip * farClip;
        }

        private static bool TestAabbAgainstFrustumCoherent(
            Bounds bounds,
            Plane[] frustumPlanes,
            int startPlaneIndex,
            out int lastPlaneIndex)
        {
            int planeCount = frustumPlanes.Length;
            int safeStart = planeCount == 0 ? 0 : Mathf.Clamp(startPlaneIndex, 0, planeCount - 1);
            for (int offset = 0; offset < planeCount; offset++)
            {
                int planeIndex = (safeStart + offset) % planeCount;
                if (IsAabbOutsidePlane(bounds, frustumPlanes[planeIndex]))
                {
                    lastPlaneIndex = planeIndex;
                    return false;
                }
            }

            lastPlaneIndex = safeStart;
            return true;
        }

        private static bool IsAabbOutsidePlane(Bounds bounds, Plane plane)
        {
            Vector3 positive = bounds.center;
            Vector3 extents = bounds.extents;
            Vector3 normal = plane.normal;
            positive.x += normal.x >= 0f ? extents.x : -extents.x;
            positive.y += normal.y >= 0f ? extents.y : -extents.y;
            positive.z += normal.z >= 0f ? extents.z : -extents.z;
            return plane.GetDistanceToPoint(positive) < 0f;
        }

        private int CountVisibleChunks()
        {
            if (!ShouldCullRenderedChunks())
            {
                return activeChunks.Count;
            }

            int count = 0;
            foreach (VoxelChunkState state in activeChunks.Values)
            {
                if (state.visible.IsVisible)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CompareChunkCoordsByPriority(int3 a, int3 b, int3 centerChunk)
        {
            int distanceComparison = GetChunkDistance(a - centerChunk).CompareTo(GetChunkDistance(b - centerChunk));
            if (distanceComparison != 0)
            {
                return distanceComparison;
            }

            return CompareInt3(a, b);
        }

        private static int CompareInt3(int3 a, int3 b)
        {
            int xComparison = a.x.CompareTo(b.x);
            if (xComparison != 0)
            {
                return xComparison;
            }

            int yComparison = a.y.CompareTo(b.y);
            if (yComparison != 0)
            {
                return yComparison;
            }

            return a.z.CompareTo(b.z);
        }

        private ChunkBuild StartChunkBuild(int3 chunkCoord, DesiredChunkState desiredState)
        {
            using (StartChunkBuildMarker.Auto())
            {
                int3 chunkSize = config.ChunkSize;
                int3 chunkOrigin = VoxelChunkUtility.GetChunkOrigin(chunkCoord, chunkSize);
                using (BuildRequestsMarker.Auto())
                {
                    BuildCellRequests(
                        cellRequestBuffer,
                        chunkOrigin,
                        chunkSize,
                        desiredState.cellSize);
                }

                int cellCount = cellRequestBuffer.Count;

                NativeArray<VoxelCellBuildRequest> requests = new NativeArray<VoxelCellBuildRequest>(cellCount, Allocator.Persistent);
                NativeArray<VoxelCell> cells = new NativeArray<VoxelCell>(cellCount, Allocator.Persistent);
                NativeList<float3> vertices = new NativeList<float3>(cellCount * MaxVerticesPerCell, Allocator.Persistent);
                NativeList<float3> normals = new NativeList<float3>(cellCount * MaxVerticesPerCell, Allocator.Persistent);
                NativeList<float2> uvs = new NativeList<float2>(cellCount * MaxVerticesPerCell, Allocator.Persistent);
                NativeList<int> interiorIndices = new NativeList<int>(cellCount * MaxVerticesPerCell, Allocator.Persistent);
                NativeList<int> transitionIndices = new NativeList<int>(cellCount * MaxVerticesPerCell, Allocator.Persistent);
                NativeList<int> surfaceIndices = new NativeList<int>(cellCount * MaxVerticesPerCell, Allocator.Persistent);
                ScalarFieldSettings scalarField = GetScalarFieldSettings();

                for (int i = 0; i < cellRequestBuffer.Count; i++)
                {
                    requests[i] = cellRequestBuffer[i];
                }

                EvaluateVoxelCellsJob evaluateJob = new EvaluateVoxelCellsJob
                {
                    scalarField = scalarField,
                    requests = requests,
                    cells = cells
                };

                JobHandle evaluateHandle = evaluateJob.Schedule(cellCount, 64);

                GenerateChunkMeshJob meshJob = new GenerateChunkMeshJob
                {
                    cells = cells,
                    cornerIndexAFromEdge = caseTable.cornerIndexAFromEdge,
                    cornerIndexBFromEdge = caseTable.cornerIndexBFromEdge,
                    triangulation = caseTable.triangulation,
                    chunkOrigin = chunkOrigin,
                    chunkSize = chunkSize,
                    scalarField = scalarField,
                    vertices = vertices,
                    normals = normals,
                    uvs = uvs,
                    interiorIndices = interiorIndices,
                    transitionIndices = transitionIndices,
                    surfaceIndices = surfaceIndices
                };

                return new ChunkBuild
                {
                    chunkCoord = chunkCoord,
                    cellSize = desiredState.cellSize,
                    detailFocusKey = desiredState.detailFocusKey,
                    chunkOrigin = chunkOrigin,
                    chunkSize = chunkSize,
                    requests = requests,
                    cells = cells,
                    vertices = vertices,
                    normals = normals,
                    uvs = uvs,
                    interiorIndices = interiorIndices,
                    transitionIndices = transitionIndices,
                    surfaceIndices = surfaceIndices,
                    jobHandle = meshJob.Schedule(evaluateHandle)
                };
            }
        }

        private void CompleteChunkBuild(ChunkBuild chunkBuild)
        {
            using (CompleteChunkBuildMarker.Auto())
            {
                chunkBuild.jobHandle.Complete();

                try
                {
                    DesiredChunkState completedState = new DesiredChunkState(
                        chunkBuild.cellSize,
                        chunkBuild.detailFocusKey);
                    bool isStillDesired = desiredChunkStates.TryGetValue(chunkBuild.chunkCoord, out DesiredChunkState desiredState)
                        && desiredState.Equals(completedState);
                    if (!isStillDesired)
                    {
                        return;
                    }

                    VoxelChunkAltIndices altIndices;
                    Mesh mesh = BuildMesh(
                        BuildChunkName(chunkBuild.chunkCoord, chunkBuild.cellSize),
                        chunkBuild.vertices,
                        chunkBuild.normals,
                        chunkBuild.uvs,
                        chunkBuild.interiorIndices,
                        chunkBuild.transitionIndices,
                        chunkBuild.surfaceIndices,
                        out altIndices);

                    VoxelChunkState nextState = new VoxelChunkState
                    {
                        chunkCoord = chunkBuild.chunkCoord,
                        cellSize = chunkBuild.cellSize,
                        detailFocusKey = chunkBuild.detailFocusKey,
                        owner = gameObject,
                        mesh = mesh,
                        altIndices = altIndices,
                        chunkOrigin = chunkBuild.chunkOrigin,
                        chunkBounds = BuildChunkBounds(chunkBuild.chunkOrigin, chunkBuild.chunkSize),
                        visible = ChunkVisibility.Visible,
                        generated = true,
                        dirty = false
                    };

                    if (activeChunks.TryGetValue(chunkBuild.chunkCoord, out VoxelChunkState oldState))
                    {
                        DestroyChunk(oldState);
                        activeChunks[chunkBuild.chunkCoord] = nextState;
                        MarkChunkVisibilityDirty();
                        MarkCombinedMeshDirty(chunkBuild.chunkCoord);
                        return;
                    }

                    activeChunks.Add(chunkBuild.chunkCoord, nextState);
                    MarkChunkVisibilityDirty();
                    MarkCombinedMeshDirty(chunkBuild.chunkCoord);
                }
                finally
                {
                    DisposeChunkBuild(chunkBuild);
                }
            }
        }

        private static int GetChunkDistance(int3 chunkOffset)
        {
            return math.max(math.abs(chunkOffset.x), math.max(math.abs(chunkOffset.y), math.abs(chunkOffset.z)));
        }

        private static void BuildCellRequests(
            List<VoxelCellBuildRequest> requests,
            int3 chunkOrigin,
            int3 chunkSize,
            int maximumCellSize,
            bool clearRequests = true)
        {
            int normalizedMaximumCellSize = math.max(1, maximumCellSize);
            normalizedMaximumCellSize = NormalizeCellSizeForChunk(normalizedMaximumCellSize, chunkSize);
            if (clearRequests)
            {
                requests.Clear();
            }

            AddCells(
                requests,
                chunkOrigin,
                chunkSize,
                normalizedMaximumCellSize);
        }

        private static int NormalizeCellSizeForChunk(int requestedSize, int3 chunkSize)
        {
            int requested = math.max(1, math.min(requestedSize, math.min(chunkSize.x, math.min(chunkSize.y, chunkSize.z))));
            for (int size = requested; size >= 1; size--)
            {
                if (chunkSize.x % size == 0
                    && chunkSize.y % size == 0
                    && chunkSize.z % size == 0)
                {
                    return size;
                }
            }

            return 1;
        }

        private static void AddCells(
            List<VoxelCellBuildRequest> requests,
            int3 origin,
            int3 size,
            int cellSize)
        {
            for (int x = 0; x < size.x; x += cellSize)
            {
                for (int y = 0; y < size.y; y += cellSize)
                {
                    for (int z = 0; z < size.z; z += cellSize)
                    {
                        int3 refinedOrigin = origin + new int3(x, y, z);
                        requests.Add(new VoxelCellBuildRequest
                        {
                            origin = refinedOrigin,
                            size = cellSize
                        });
                    }
                }
            }
        }

        private Mesh BuildMesh(
            string meshName,
            NativeList<float3> vertices,
            NativeList<float3> normals,
            NativeList<float2> uvs,
            NativeList<int> interiorIndices,
            NativeList<int> transitionIndices,
            NativeList<int> surfaceIndices,
            out VoxelChunkAltIndices altIndices)
        {
            using (UploadMeshMarker.Auto())
            {
                altIndices = new VoxelChunkAltIndices(
                    interiorIndices.Length,
                    transitionIndices.Length,
                    surfaceIndices.Length);
                Mesh mesh = new Mesh
                {
                    name = meshName,
                    indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
                };

                if (vertices.Length == 0 || altIndices.TotalIndexCount == 0)
                {
                    mesh.bounds = new Bounds(Vector3.zero, Vector3.zero);
                    return mesh;
                }

                CopyToVector3List(vertices, meshUploadVertices);
                CopyToVector3List(normals, meshUploadNormals);

                Bounds bounds = CalculateBounds(meshUploadVertices);
                CopyToVector2List(uvs, meshUploadUvs);
                CopyToIntList(interiorIndices, meshUploadInteriorIndices);
                CopyToIntList(transitionIndices, meshUploadTransitionIndices);
                CopyToIntList(surfaceIndices, meshUploadSurfaceIndices);

                mesh.SetVertices(meshUploadVertices);
                mesh.SetNormals(meshUploadNormals);
                mesh.SetUVs(0, meshUploadUvs);
                mesh.subMeshCount = LayerSubMeshCount;
                mesh.SetTriangles(meshUploadInteriorIndices, InteriorSubMesh, false);
                mesh.SetTriangles(meshUploadTransitionIndices, TransitionSubMesh, false);
                mesh.SetTriangles(meshUploadSurfaceIndices, SurfaceSubMesh, false);
                mesh.bounds = bounds;
                return mesh;
            }
        }

        private static Bounds CalculateBounds(List<Vector3> vertices)
        {
            Vector3 min = vertices[0];
            Vector3 max = vertices[0];
            for (int i = 1; i < vertices.Count; i++)
            {
                Vector3 vertex = vertices[i];
                min = Vector3.Min(min, vertex);
                max = Vector3.Max(max, vertex);
            }

            return new Bounds((min + max) * 0.5f, max - min);
        }

        private static void CopyToVector3List(NativeList<float3> source, List<Vector3> destination)
        {
            EnsureListCapacity(destination, source.Length);
            destination.Clear();
            for (int i = 0; i < source.Length; i++)
            {
                float3 value = source[i];
                destination.Add(new Vector3(value.x, value.y, value.z));
            }
        }

        private static void CopyToVector2List(NativeList<float2> source, List<Vector2> destination)
        {
            EnsureListCapacity(destination, source.Length);
            destination.Clear();
            for (int i = 0; i < source.Length; i++)
            {
                float2 value = source[i];
                destination.Add(new Vector2(value.x, value.y));
            }
        }

        private static void CopyToIntList(NativeList<int> source, List<int> destination)
        {
            EnsureListCapacity(destination, source.Length);
            destination.Clear();
            for (int i = 0; i < source.Length; i++)
            {
                destination.Add(source[i]);
            }
        }

        private static void EnsureListCapacity<T>(List<T> list, int requiredCapacity)
        {
            if (list.Capacity < requiredCapacity)
            {
                list.Capacity = requiredCapacity;
            }
        }

        private void MarkChunkVisibilityDirty()
        {
            chunkVisibilityDirty = true;
            MarkCombinedRendererVisibilityDirty();
        }

        private static Bounds BuildChunkBounds(int3 chunkOrigin, int3 chunkSize)
        {
            Vector3 size = ToVector3(chunkSize);
            return new Bounds(ToVector3(chunkOrigin) + size * 0.5f, size);
        }

        private static Vector3 ToVector3(int3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private Vector3 GetCurrentDetailFocusVector3()
        {
            float3 focus = GetCurrentDetailFocus();
            return new Vector3(focus.x, focus.y, focus.z);
        }

        private void RemoveUndesiredChunks()
        {
            scratchChunkCoords.Clear();
            foreach (KeyValuePair<int3, VoxelChunkState> pair in activeChunks)
            {
                if (!desiredChunks.Contains(pair.Key))
                {
                    scratchChunkCoords.Add(pair.Key);
                }
            }

            for (int i = 0; i < scratchChunkCoords.Count; i++)
            {
                int3 chunkCoord = scratchChunkCoords[i];
                DestroyChunk(activeChunks[chunkCoord]);
                activeChunks.Remove(chunkCoord);
                MarkChunkVisibilityDirty();
                MarkCombinedMeshDirty(chunkCoord);
            }
        }

        private void ClearChunks()
        {
            foreach (VoxelChunkState state in activeChunks.Values)
            {
                DestroyChunk(state);
            }

            activeChunks.Clear();
            desiredChunks.Clear();
            desiredChunkStates.Clear();
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
                        Mathf.Clamp(NearCombinedMeshBucketCount, 1, MaxCombinedMeshBucketCount))
                });
            }
        }

        private void RefreshPlanetDataBuildStateFromDesiredStates()
        {
            int segmentCount = Mathf.Clamp(NearCombinedMeshBucketCount, 1, MaxCombinedMeshBucketCount);
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

        private static void DisposeChunkBuild(ChunkBuild chunkBuild)
        {
            if (chunkBuild.requests.IsCreated)
            {
                chunkBuild.requests.Dispose();
            }

            if (chunkBuild.cells.IsCreated)
            {
                chunkBuild.cells.Dispose();
            }

            if (chunkBuild.vertices.IsCreated)
            {
                chunkBuild.vertices.Dispose();
            }

            if (chunkBuild.normals.IsCreated)
            {
                chunkBuild.normals.Dispose();
            }

            if (chunkBuild.uvs.IsCreated)
            {
                chunkBuild.uvs.Dispose();
            }

            if (chunkBuild.interiorIndices.IsCreated)
            {
                chunkBuild.interiorIndices.Dispose();
            }

            if (chunkBuild.transitionIndices.IsCreated)
            {
                chunkBuild.transitionIndices.Dispose();
            }

            if (chunkBuild.surfaceIndices.IsCreated)
            {
                chunkBuild.surfaceIndices.Dispose();
            }
        }

        private static void DisposeIfCreated<T>(NativeArray<T> array)
            where T : struct
        {
            if (array.IsCreated)
            {
                array.Dispose();
            }
        }

        private static void DisposeIfCreated<T>(NativeList<T> list)
            where T : unmanaged
        {
            if (list.IsCreated)
            {
                list.Dispose();
            }
        }

        private void RefreshDeclaredChunks()
        {
            if (sphereGenerator != null)
            {
                sphereGenerator.DeclareOccupiedChunks(this, config.ChunkSize);
            }
        }

        private void MarkAllChunksDirty()
        {
            foreach (VoxelChunkState state in activeChunks.Values)
            {
                state.dirty = true;
            }
        }

        private ScalarFieldSettings GetScalarFieldSettings()
        {
            return sphereGenerator != null
                ? sphereGenerator.BuildScalarFieldSettings()
                : config.ScalarFieldSettings;
        }

        private Vector3 GetSpherePosition()
        {
            return sphereGenerator != null ? sphereGenerator.transform.position : Vector3.zero;
        }

        private Vector3 GetSpherePositionInManagerLocal()
        {
            return sphereGenerator != null ? transform.InverseTransformPoint(sphereGenerator.transform.position) : Vector3.zero;
        }

        private static void DestroyChunk(VoxelChunkState state)
        {
            if (state.mesh != null)
            {
                DestroyUnityObject(state.mesh);
            }
        }

        private static void DestroyUnityObject(Object target)
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

        private static string BuildChunkName(int3 chunkCoord, int cellSize)
        {
            return $"VoxelChunk_{chunkCoord.x}_{chunkCoord.y}_{chunkCoord.z}_S{cellSize}";
        }

        private sealed class VoxelChunkState
        {
            public int3 chunkCoord;
            public int cellSize;
            public int3 detailFocusKey;
            public GameObject owner;
            public Mesh mesh;
            public VoxelChunkAltIndices altIndices;
            public int3 chunkOrigin;
            public Bounds chunkBounds;
            public ChunkVisibility visible;
            public int frustumLastPlaneIndex;
            public bool generated;
            public bool dirty;
        }

        private sealed class CombinedMeshBucket
        {
            public GameObject owner;
            public MeshFilter meshFilter;
            public MeshRenderer meshRenderer;
            public Mesh mesh;
            public VoxelSegmentLodMeshCache lodCache;
            public Mesh[] lodMeshes;
            public bool[] lodDirty;
            public bool[] lodCached;
            public int activeLodIndex;
            public CombineInstance[] combineInstanceBuffer;
            public bool dirty;
        }

        private struct DeferredSegmentLodBuild
        {
            public bool active;
            public DeferredSegmentLodKey key;
            public int nextChunkIndex;
            public int cellSize;
        }

        private readonly struct DeferredSegmentLodKey : System.IEquatable<DeferredSegmentLodKey>
        {
            public readonly int bucketIndex;
            public readonly int lodIndex;

            public DeferredSegmentLodKey(int bucketIndex, int lodIndex)
            {
                this.bucketIndex = bucketIndex;
                this.lodIndex = lodIndex;
            }

            public bool Equals(DeferredSegmentLodKey other)
            {
                return bucketIndex == other.bucketIndex && lodIndex == other.lodIndex;
            }

            public override bool Equals(object obj)
            {
                return obj is DeferredSegmentLodKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (bucketIndex * 397) ^ lodIndex;
                }
            }
        }

        private struct DesiredChunkState
        {
            public int cellSize;
            public int3 detailFocusKey;

            public DesiredChunkState(int cellSize, int3 detailFocusKey)
            {
                this.cellSize = cellSize;
                this.detailFocusKey = detailFocusKey;
            }

            public bool Equals(DesiredChunkState other)
            {
                return cellSize == other.cellSize
                    && detailFocusKey.Equals(other.detailFocusKey);
            }
        }

        private sealed class ChunkBuild
        {
            public int3 chunkCoord;
            public int cellSize;
            public int3 detailFocusKey;
            public int3 chunkOrigin;
            public int3 chunkSize;
            public NativeArray<VoxelCellBuildRequest> requests;
            public NativeArray<VoxelCell> cells;
            public NativeList<float3> vertices;
            public NativeList<float3> normals;
            public NativeList<float2> uvs;
            public NativeList<int> interiorIndices;
            public NativeList<int> transitionIndices;
            public NativeList<int> surfaceIndices;
            public JobHandle jobHandle;
        }

        private struct ChunkVisibility
        {
            public bool inRange;
            public bool inFrustum;

            public static ChunkVisibility Visible => new ChunkVisibility
            {
                inRange = true,
                inFrustum = true
            };

            public bool IsVisible => inRange && inFrustum;
        }

        private struct VoxelChunkAltIndices
        {
            public int interiorIndexCount;
            public int transitionIndexCount;
            public int surfaceIndexCount;

            public VoxelChunkAltIndices(int interiorIndexCount, int transitionIndexCount, int surfaceIndexCount)
            {
                this.interiorIndexCount = interiorIndexCount;
                this.transitionIndexCount = transitionIndexCount;
                this.surfaceIndexCount = surfaceIndexCount;
            }

            public int TotalIndexCount => interiorIndexCount + transitionIndexCount + surfaceIndexCount;

            public bool HasSubMesh(int subMeshIndex)
            {
                switch (subMeshIndex)
                {
                    case InteriorSubMesh:
                        return interiorIndexCount > 0;
                    case TransitionSubMesh:
                        return transitionIndexCount > 0;
                    case SurfaceSubMesh:
                        return surfaceIndexCount > 0;
                    default:
                        return false;
                }
            }
        }

        private static class VoxelChunkLayerMask
        {
            public const int None = 0;
            public const int Interior = 1 << 0;
            public const int Transition = 1 << 1;
            public const int Surface = 1 << 2;
            public const int All = Interior | Transition | Surface;
        }

    }
}
