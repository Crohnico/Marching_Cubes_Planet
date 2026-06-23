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
        private const int InteriorSubMesh = 0;
        private const int TransitionSubMesh = 1;
        private const int SurfaceSubMesh = 2;
        private const int MaxCombinedMeshBucketCount = VoxelEngineConfig.MaxCombinedMeshBucketCount;
        private const int SegmentLodCount = 3;
        private const int ActiveSegmentLodIndex = 2;
        private const int DefaultStartupFarChunkBuildsPerFrame = 8;
        private const int DefaultSegmentLodChunksBuiltPerFrame = 16;
        private static readonly ProfilerMarker RebuildDesiredMarker = new ProfilerMarker("VoxelEngine.RebuildDesiredChunks");
        private static readonly ProfilerMarker CombineMeshMarker = new ProfilerMarker("VoxelEngine.CombineMesh");
        private static readonly ProfilerMarker BuildCombineInstancesMarker = new ProfilerMarker("VoxelEngine.BuildCombineInstances");
        private static readonly ProfilerMarker CombineBucketMeshMarker = new ProfilerMarker("VoxelEngine.CombineBucketMesh");

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

        private readonly PlanetChunkRuntime chunkRuntime = new PlanetChunkRuntime();
        private readonly List<CombineInstance> combineInstances = new List<CombineInstance>();
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
        private bool lastShouldCullRenderedChunks;
        private bool hasPlanetActionRadiusState;
        private bool isInsidePlanetActionRadius = true;
        private bool hasAtmosphereState;
        private bool isPlayerInsideAtmosphere;
        private DeferredSegmentLodBuild activeDeferredSegmentLodBuild;
        private Coroutine startupCoroutine;
        private bool startupInProgress;
        private bool startupDone;
        private int segmentLodChunksBuiltPerFrame = DefaultSegmentLodChunksBuiltPerFrame;
        private string activeSystemId;
        private PlanetData planetData = new PlanetData();
        private MarchingCubesCaseTable caseTable;
        private VoxelEngineConfig config;

        private Dictionary<int3, VoxelChunkState> activeChunks => chunkRuntime.ActiveChunks;
        private Dictionary<int3, DesiredChunkState> desiredChunkStates => chunkRuntime.DesiredChunkStates;
        private HashSet<int3> declaredChunks => chunkRuntime.DeclaredChunks;
        private HashSet<int3> desiredChunks => chunkRuntime.DesiredChunks;
        private List<int3> scratchChunkCoords => chunkRuntime.ScratchChunkCoords;
        private List<VoxelCellBuildRequest> cellRequestBuffer => chunkRuntime.CellRequestBuffer;
        private Plane[] chunkCullingFrustumPlanes => chunkRuntime.CullingFrustumPlanes;
        private bool chunkVisibilityDirty
        {
            get => chunkRuntime.VisibilityDirty;
            set => chunkRuntime.VisibilityDirty = value;
        }

        public static event Action<PlanetManager, bool> PlayerAtmosphereStateChanged;

        public PlanetData PlanetData => planetData;
        public string ResolvedPlanetID => ResolvePlanetId();
        public bool StartupDone => startupDone;
        public int DeclaredChunkCount => chunkRuntime.DeclaredChunkCount;
        public int DesiredChunkCount => chunkRuntime.DesiredChunkCount;
        public int ActiveChunkCount => chunkRuntime.ActiveChunkCount;
        public int VisibleChunkCount => CountVisibleChunks();
        public int CombinedVertexCount => GetCombinedVertexCount();
        public int CombinedTriangleCount => GetCombinedTriangleCount();
        public bool IsRenderCullingActive => ShouldCullRenderedChunks();
        public bool IsFarBridgeActive => IsFarBridgeVisible();
        public bool IsNearCombinedRenderingActive => IsNearCombinedRenderingReady();
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
            chunkRuntime.ApplyPlanetDataToDeclaredChunks(planetData);
            chunkRuntime.HydrateFromPlanetData(planetData, gameObject, BuildChunkName);

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

            int lodCount = Mathf.Clamp(config != null ? config.LodCount : SegmentLodCount, 1, SegmentLodCount);
            int activeLod = Mathf.Clamp(ActiveSegmentLodIndex, 0, lodCount - 1);
            int requestedCellSize = config.GetCellSizeAtLod(activeLod);
            int segmentCount = Mathf.Clamp(NearCombinedMeshBucketCount, 1, MaxCombinedMeshBucketCount);
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
                chunkRuntime.ApplyPlanetDataToDeclaredChunks(planetData);
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

                CompleteChunkBuild(ChunkBuilder.StartChunkBuild(
                    chunkCoord,
                    desiredState,
                    config.ChunkSize,
                    GetScalarFieldSettings(),
                    caseTable,
                    cellRequestBuffer));
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
            chunkRuntime.DeclareChunk(chunkCoord);
        }

        public void ReleaseChunk(int3 chunkCoord)
        {
            chunkRuntime.ReleaseChunk(chunkCoord);
        }

        public void ClearDeclaredChunks()
        {
            chunkRuntime.ClearDeclaredChunks();
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

                CompleteChunkBuild(ChunkBuilder.StartChunkBuild(
                    chunkCoord,
                    desiredState,
                    config.ChunkSize,
                    GetScalarFieldSettings(),
                    caseTable,
                    cellRequestBuffer));
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
            bool visibilityChanged = chunkRuntime.UpdateVisibility(
                ShouldCullRenderedChunks(),
                playerChunkTracker.ChunkCullingCamera,
                MarkCombinedMeshDirty);

            if (visibilityChanged)
            {
                combinedMeshesDirty = true;
                MarkCombinedRendererVisibilityDirty();
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
            return UseChunkCullingForRendering && UseChunkCulling();
        }

        private int CountVisibleChunks()
        {
            return chunkRuntime.CountVisibleChunks(ShouldCullRenderedChunks());
        }

        private static int CompareChunkCoordsByPriority(int3 a, int3 b, int3 centerChunk)
        {
            int distanceComparison = GetChunkDistance(a - centerChunk).CompareTo(GetChunkDistance(b - centerChunk));
            if (distanceComparison != 0)
            {
                return distanceComparison;
            }

            return VoxelRuntimeMath.CompareInt3(a, b);
        }

        private void CompleteChunkBuild(ChunkBuild chunkBuild)
        {
            if (chunkRuntime.CompleteChunkBuild(chunkBuild, gameObject, BuildChunkName, out int3 changedChunkCoord))
            {
                MarkChunkVisibilityDirty();
                MarkCombinedMeshDirty(changedChunkCoord);
            }
        }

        private static int GetChunkDistance(int3 chunkOffset)
        {
            return math.max(math.abs(chunkOffset.x), math.max(math.abs(chunkOffset.y), math.abs(chunkOffset.z)));
        }

        private void MarkChunkVisibilityDirty()
        {
            chunkRuntime.MarkVisibilityDirty();
            MarkCombinedRendererVisibilityDirty();
        }

        private Vector3 GetCurrentDetailFocusVector3()
        {
            float3 focus = GetCurrentDetailFocus();
            return new Vector3(focus.x, focus.y, focus.z);
        }

        private void RemoveUndesiredChunks()
        {
            chunkRuntime.RemoveUndesiredChunks(MarkCombinedMeshDirty);
            MarkCombinedRendererVisibilityDirty();
        }

        private void ClearChunks()
        {
            chunkRuntime.ClearChunks();
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
            chunkRuntime.MarkAllChunksDirty();
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

        private static string BuildChunkName(int3 chunkCoord, int cellSize)
        {
            return $"VoxelChunk_{chunkCoord.x}_{chunkCoord.y}_{chunkCoord.z}_S{cellSize}";
        }

    }
}
