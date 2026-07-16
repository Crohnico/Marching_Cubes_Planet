using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MarchingCubesPlanet.Coordinates;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Serialization;

namespace MarchingCubesPlanet.MarchingCubes
{
    [DisallowMultipleComponent]
    public sealed class PlanetDirector : MonoBehaviour
    {
        private const string DefaultOceanMaterialResourceName = "PlanetOcean";
        private const int OceanLongitudeSegments = 96;
        private const int OceanLatitudeSegments = 48;
        private const float ThreeOverFourPi = 0.23873242f;
        private const float HalfSqrtThree = 0.8660254f;
        private static readonly ProfilerMarker TopologySearchMarker =
            new ProfilerMarker("Planet.Topology.SearchRebuild");

        [SerializeField] private PlanetRecipe recipe = PlanetRecipe.Default();
        [SerializeField] private PlanetPlacement placement = PlanetPlacement.Default();
        [SerializeField] private Transform player;
        [SerializeField] private float activationRange = 10000f;
        [SerializeField] private float baseGridPrewarmRange = 15000f;
        [SerializeField] private PlanetChunkLod shellLod = PlanetChunkLod.LOD2;
        [SerializeField] private PlanetChunkLod baseLod = PlanetChunkLod.LOD0;
        [SerializeField] private bool standaloneLoadShellOnStart;
        [SerializeField] private Material oceanMaterial;
        [SerializeField] private bool useBaseOctree = true;
        [SerializeField] private int baseOctreeLod0RadiusChunks = 3;
        [SerializeField] private int baseOctreeLod1RadiusChunks = 8;
        [SerializeField] private int baseOctreeMaxChunks = 256;
        [SerializeField] private int baseOctreeOutputVertexCapacity = 1500000;
        [SerializeField] private int baseRebuildDistanceChunks = 2;
        [SerializeField] private float baseTransvoxelWidthCells = 0.5f;

        [Header("Chunk path cost debug")]
        [InspectorName("Draw Cost Gizmos")]
        [SerializeField] private bool drawChunkPathCostGizmos = true;
        [Tooltip("Allows one asynchronous topology request in flight. Zero pauses the catalogue.")]
        [InspectorName("Async Catalogue Enabled")]
        [SerializeField] [Range(0, 1)] private int topologyDebugChunksPerFrame = 1;
        [Tooltip("Use the current Base chunk limit as the maximum number of coloured chunks.")]
        [InspectorName("Use Base Chunk Limit")]
        [SerializeField] private bool topologyDebugUseBaseBufferLimit = true;
        [InspectorName("Max Coloured Chunks")]
        [SerializeField] [Min(1)] private int topologyDebugMaxChunks = 256;
        [Tooltip("Radius, in chunks, whose directly reachable air is admitted before path-cost candidates.")]
        [InspectorName("Straight Radius (Chunks)")]
        [SerializeField] [Range(0, 12)] private int topologyDebugStraightRadiusChunks = 2;
        [Tooltip("Maximum component nodes explored per requested chunk. Raise it only when the graph is visibly cut short.")]
        [InspectorName("Search Expansion Multiplier")]
        [SerializeField] [Range(1, 32)] private int topologyDebugSearchExpansionMultiplier = 8;
        [Tooltip("Rebuild an incomplete selection only after this many additional chunks have been catalogued.")]
        [InspectorName("Catalogue Batch / Rebuild")]
        [SerializeField] [Range(1, 128)] private int topologyDebugCatalogBatchSize = 16;
        [Tooltip("Cost of entering a chunk made entirely of air.")]
        [InspectorName("Air Chunk Cost")]
        [SerializeField] [Min(0f)] private float topologyDebugAirTraversalCost = 0.1f;
        [Tooltip("Cost of entering a chunk containing both solid and traversable air.")]
        [InspectorName("Mixed Chunk Cost")]
        [SerializeField] [Min(0f)] private float topologyDebugMixedTraversalCost = 1f;
        [Tooltip("Uniform six-neighbour step cost. It does not penalise a world or radial Y axis.")]
        [InspectorName("Step Cost")]
        [SerializeField] [Min(0f)] private float topologyDebugStepHeuristicCost;
        [Tooltip("Density values at or below this threshold are considered traversable air by the debug catalogue.")]
        [InspectorName("Air Density Threshold")]
        [SerializeField] private float topologyDebugAirDensityThreshold;
        [FormerlySerializedAs("topologyDebugRedCost")]
        [Tooltip("Chunks above this accumulated path cost are rejected. The limit is also the red end of the gizmo gradient.")]
        [InspectorName("Max Path Cost")]
        [SerializeField] [Min(0.0001f)] private float topologyDebugMaxPathCost = 10f;
        [InspectorName("Low Cost Colour")]
        [SerializeField] private Color topologyDebugLowCostColor = new Color(0.1f, 1f, 0.15f, 0.7f);
        [InspectorName("High Cost Colour")]
        [SerializeField] private Color topologyDebugHighCostColor = new Color(1f, 0.1f, 0.05f, 0.7f);

        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private PlanetGpuMarchingCubesSurface gpuSurface;
        private GameObject oceanSphere;
        private MeshFilter oceanFilter;
        private MeshRenderer oceanRenderer;
        private Mesh oceanMesh;
        private PlanetGrid grid;
        private readonly List<PlanetGridCoordinates> baseChunkCoordinates = new List<PlanetGridCoordinates>(256);
        private readonly HashSet<PlanetGridCoordinates> baseChunkCoordinateSet = new HashSet<PlanetGridCoordinates>();
        private readonly Dictionary<PlanetGridCoordinates, PlanetChunkLod> activeBaseChunkLods = new Dictionary<PlanetGridCoordinates, PlanetChunkLod>(256);
        private readonly PlanetTransvoxelFaceDescriptor[] transitionFaceScratch = new PlanetTransvoxelFaceDescriptor[6];
        private readonly PlanetChunkTopologyDebugCatalog topologyDebugCatalog = new PlanetChunkTopologyDebugCatalog();
        private readonly PlanetChunkPathDebugSearch topologyDebugSearch = new PlanetChunkPathDebugSearch();
        private readonly HashSet<PlanetGridCoordinates> topologyDebugSelectedChunks =
            new HashSet<PlanetGridCoordinates>();
        private int chunkLoadIndex = -1;
        private int loadVersion;
        private bool isAlive;
        private bool isSetUp;
        private bool isBaseLoading;
        private Vector3 baseFocusGrid;
        private Vector3 loadedBaseFocusGrid;
        private bool hasLoadedBaseFocus;
        private int baseTransitionFaceCount;
        private PlanetGridCoordinates topologyDebugPlayerChunk;
        private bool hasTopologyDebugPlayerChunk;
        private int topologyDebugSettingsHash;
        private int topologyDebugCatalogCountAtLastSearch;

        public PlanetGrid Grid => grid;
        public bool IsAlive => isAlive;
        public bool IsSetUp => isSetUp;
        public Transform Player => player;

        public PlanetRecipe Recipe
        {
            get => recipe;
            set
            {
                recipe = value;
                grid = null;
                ResetTopologyDebug();
                UpdateOceanSphere();
            }
        }

        public PlanetPlacement Placement => placement;

        private IEnumerator Start()
        {
            SyncPlacementFromTransform();
            gpuSurface = GetGpuSurface();

            if (player == null && Camera.main != null)
            {
                player = Camera.main.transform;
            }

            if (!standaloneLoadShellOnStart)
            {
                yield break;
            }

            yield return new WaitForSeconds(0.1f);
        
            LoadShell(shellLod, () => isSetUp = false);
        }

        private void Update()
        {
            SyncPlacementFromTransform();
            gpuSurface.SetPlacement(placement);

            if (player == null)
            {
                return;
            }

            float playerDistance = Vector3.Distance(player.position, transform.position);
            if (grid == null && playerDistance <= Mathf.Max(activationRange, baseGridPrewarmRange))
            {
                GenerateGrid();
            }

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (drawChunkPathCostGizmos && playerDistance <= activationRange)
            {
                UpdateTopologyDebug();
            }
            #endif

            bool nextAlive = playerDistance <= activationRange;
            if (nextAlive != isAlive)
            {
                isAlive = nextAlive;
                if (isAlive && !isSetUp)
                {
                    LoadBaseFromShell();
                    return;
                }

                if (!isAlive)
                {
                    if (isBaseLoading)
                    {
                        bool wasSetUp = isSetUp;
                        CancelBaseLoad();
                        if (wasSetUp)
                        {
                            LoadShell(shellLod, () =>
                            {
                                ReleaseBase();
                                isSetUp = false;
                            }, true);
                        }
                        else
                        {
                            ReleaseBase();
                        }

                        return;
                    }

                    if (isSetUp)
                    {
                        LoadShell(shellLod, () =>
                        {
                            ReleaseBase();
                            isSetUp = false;
                        }, true);
                    }
                }

                return;
            }

            if (isAlive && isSetUp && !isBaseLoading && ShouldRebuildBaseForPlayerMovement())
            {
                LoadBase(baseLod, () =>
                {
                    isSetUp = true;
                    Debug.Log("BaseReloaded", this);
                }, true);
            }
        }

        public void LoadShell(PlanetChunkLod lod, Action onComplete = null, bool keepBaseVisibleUntilComplete = false)
        {
            loadVersion++;
            isBaseLoading = false;
            chunkLoadIndex = -1;

            ClearLegacyMesh();
            grid = GetGpuSurface().GenerateShell(
                recipe,
                placement,
                lod,
                meshRenderer,
                keepBaseVisibleUntilComplete);
            LogShellBudget();
            EnsureOceanSphere();
            onComplete?.Invoke();
        }

        public void LoadShellFromCacheOrGenerate(
            string shellCachePath,
            PlanetChunkLod lod,
            Action onComplete = null,
            bool keepBaseVisibleUntilComplete = false)
        {
            SyncPlacementFromTransform();
            loadVersion++;
            isBaseLoading = false;
            chunkLoadIndex = -1;
            ClearLegacyMesh();

            PlanetGpuMarchingCubesSurface surface = GetGpuSurface();
            if (surface.TryLoadShellCache(shellCachePath, recipe, placement, lod, meshRenderer, out PlanetGrid cachedGrid))
            {
                grid = cachedGrid;
                Debug.Log("<color=#37D67A>[Shell Cache] Loaded from disk: " + Path.GetFileName(shellCachePath) + "</color>", this);
                LogShellBudget();
                EnsureOceanSphere();
                onComplete?.Invoke();
                return;
            }

            Debug.Log("<color=#FFD400>[Shell Cache] Missing. Generating shell: " + Path.GetFileName(shellCachePath) + "</color>", this);
            grid = surface.GenerateShell(
                recipe,
                placement,
                lod,
                meshRenderer,
                keepBaseVisibleUntilComplete);
            LogShellBudget();
            EnsureOceanSphere();
            bool saved = surface.TrySaveShellCache(shellCachePath, recipe, lod);
            if (saved)
            {
                Debug.Log("<color=#FFD400>[Shell Cache] Generated and saved to disk: " + Path.GetFileName(shellCachePath) + "</color>", this);
            }
            else
            {
                Debug.LogWarning("[Shell Cache] Generated but could not save to disk: " + shellCachePath, this);
            }

            onComplete?.Invoke();
        }

        private void LogShellBudget()
        {
            PlanetGpuMarchingCubesSurface surface = GetGpuSurface();
            Debug.Log(
                "[Shell GPU] chunks=" + (grid != null ? grid.InformationCount : 0) +
                " vertices=" + surface.ShellVertexCount +
                " capacity=" + surface.ShellVertexCapacity +
                " bytes=" + surface.ShellVertexBytes,
                this);
        }

        public void SetAutoLoadShellOnStart(bool enabled)
        {
            standaloneLoadShellOnStart = enabled;
        }

        public void SetPlanetSeed(int seed)
        {
            if (recipe.Seed == seed)
            {
                return;
            }

            recipe.Seed = seed;
            grid = null;
            ResetTopologyDebug();
            UpdateOceanSphere();
        }

        public void LoadBase(PlanetChunkLod lod, Action onComplete = null, bool keepCurrentBaseVisibleUntilComplete = false)
        {
            if (grid == null)
            {
                GenerateGrid();
            }

            grid.CopyInformationCells(baseChunkCoordinates);
            PrepareBaseOctree();
            PrepareBaseTransitionState(lod);
            int sequence = ++loadVersion;
            chunkLoadIndex = -1;
            baseTransitionFaceCount = 0;
            isBaseLoading = true;
            GetGpuSurface().BeginChunkSequence(true, keepCurrentBaseVisibleUntilComplete);
            QueueChunkLoad(lod, sequence, onComplete);
        }

        private void LoadBaseFromShell()
        {
            LoadBase(shellLod, () =>
            {
                ReleaseShell();
                isSetUp = true;
                Debug.Log("BaseLoaded", this);

                if (baseLod == shellLod || !isAlive)
                {
                    return;
                }

                LoadBase(baseLod, () =>
                {
                    isSetUp = true;
                    Debug.Log("BaseRefined", this);
                }, true);
            });
        }

        public async void LoadChunk(PlanetChunkLod lod, PlanetGridCoordinates? chunkID = null, Action onComplete = null)
        {

            if (!chunkID.HasValue)
            {
                if (grid.InformationCount <= 0)
                {
                    onComplete?.Invoke();
                    return;
                }

                chunkID = grid.GetInfoCell(UnityEngine.Random.Range(0, grid.InformationCount));
            }

            ClearLegacyMesh();
            GetGpuSurface().BeginChunkSequence();
            GetGpuSurface().GenerateChunk(
                recipe,
                placement,
                lod,
                chunkID.Value,
                meshRenderer);
            await Task.Yield();
            onComplete?.Invoke();
        }

        public void ReleaseShell()
        {
            GetGpuSurface().ReleaseShell();
        }

        public void ReleaseBase()
        {
            GetGpuSurface().ReleaseChunkAggregate();
            isBaseLoading = false;
            chunkLoadIndex = -1;
            baseChunkCoordinates.Clear();
            activeBaseChunkLods.Clear();
            hasLoadedBaseFocus = false;
        }

        public PlanetChunkLod CalculateLODFromPlayerPosition()
        {
            return PlanetChunkLod.LOD2;
        }

        private async void QueueChunkLoad(PlanetChunkLod lod, int sequence, Action onComplete)
        {
            if (sequence != loadVersion || !isBaseLoading || grid == null)
            {
                return;
            }

            chunkLoadIndex++;
            if (chunkLoadIndex >= baseChunkCoordinates.Count)
            {
                isBaseLoading = false;
                loadedBaseFocusGrid = baseFocusGrid;
                hasLoadedBaseFocus = true;
                GetGpuSurface().CompleteChunkSequence();
                Debug.Log("Base Transvoxel faces: " + baseTransitionFaceCount, this);
                onComplete?.Invoke();
                return;
            }

            PlanetGridCoordinates chunkID = baseChunkCoordinates[chunkLoadIndex];
            PlanetChunkLod chunkLod = GetActiveBaseChunkLod(lod, chunkID);
            int transitionFaceCount = BuildTransitionFaces(chunkID, chunkLod, chunkLoadIndex);
            baseTransitionFaceCount += transitionFaceCount;
            ClearLegacyMesh();
            GetGpuSurface().GenerateChunkIntoBase(
                recipe,
                placement,
                chunkLod,
                chunkID,
                meshRenderer,
                useBaseOctree ? baseOctreeOutputVertexCapacity : PlanetMarchingCubesSettings.DefaultOutputVertexCapacity,
                transitionFaceScratch,
                transitionFaceCount);
            await Task.Yield();
            QueueChunkLoad(lod, sequence, onComplete);
        }

        private void CancelBaseLoad()
        {
            loadVersion++;
            isBaseLoading = false;
            chunkLoadIndex = -1;
            baseChunkCoordinates.Clear();
            activeBaseChunkLods.Clear();
            baseTransitionFaceCount = 0;
            hasLoadedBaseFocus = false;
        }

        private void GenerateGrid()
        {
            grid = PlanetGenerator.GenerateGrid(recipe);
            chunkLoadIndex = -1;
        }

        private void PrepareBaseOctree()
        {
            baseFocusGrid = CalculateBaseFocusGrid();
            AddCaveVolumeCandidates();

            if (!useBaseOctree)
            {
                return;
            }

            baseChunkCoordinates.Sort(CompareBaseChunkDistance);

            int maxChunks = Mathf.Max(0, baseOctreeMaxChunks);
            if (maxChunks > 0 && baseChunkCoordinates.Count > maxChunks)
            {
                baseChunkCoordinates.RemoveRange(maxChunks, baseChunkCoordinates.Count - maxChunks);
            }
        }

        private void AddCaveVolumeCandidates()
        {
            PlanetCaveSettings caves = recipe.CaveSystem;
            if (!caves.Enabled || caves.Porosity <= 0)
            {
                return;
            }

            baseChunkCoordinateSet.Clear();
            for (int i = 0; i < baseChunkCoordinates.Count; i++)
            {
                baseChunkCoordinateSet.Add(baseChunkCoordinates[i]);
            }

            int maxChunks = baseOctreeMaxChunks > 0 ? baseOctreeMaxChunks : 256;
            int budgetRadius = Mathf.Max(1, Mathf.CeilToInt(Mathf.Pow(maxChunks * ThreeOverFourPi, 1f / 3f)));
            int radiusChunks = Mathf.Max(1, Mathf.Min(Mathf.Max(1, baseOctreeLod1RadiusChunks), budgetRadius));
            float chunkSize = PlanetMarchingCubesChunkRange.CanonicalChunkSize;
            PlanetGridCoordinates center = new PlanetGridCoordinates(
                Mathf.FloorToInt(baseFocusGrid.x / chunkSize),
                Mathf.FloorToInt(baseFocusGrid.y / chunkSize),
                Mathf.FloorToInt(baseFocusGrid.z / chunkSize));
            float outerRadius = recipe.GridRadius *
                                (1f + Mathf.Max(0f, recipe.MaxLandElevation) * Mathf.Max(0f, recipe.MaxHeightModifier) +
                                 Mathf.Max(0f, recipe.MountainBiomeHeight) + Mathf.Max(0f, recipe.SurfaceNoiseAmplitude));
            float chunkHalfDiagonal = chunkSize * HalfSqrtThree;
            float maximumCenterDistance = outerRadius + chunkHalfDiagonal;
            float maximumCenterDistanceSquared = maximumCenterDistance * maximumCenterDistance;
            int radiusSquared = radiusChunks * radiusChunks;

            for (int z = -radiusChunks; z <= radiusChunks; z++)
            {
                for (int y = -radiusChunks; y <= radiusChunks; y++)
                {
                    for (int x = -radiusChunks; x <= radiusChunks; x++)
                    {
                        if (x * x + y * y + z * z > radiusSquared)
                        {
                            continue;
                        }

                        PlanetGridCoordinates candidate = new PlanetGridCoordinates(
                            center.x + x,
                            center.y + y,
                            center.z + z);
                        if (baseChunkCoordinateSet.Contains(candidate) ||
                            CalculateBaseChunkCenterGrid(candidate).sqrMagnitude > maximumCenterDistanceSquared)
                        {
                            continue;
                        }

                        baseChunkCoordinateSet.Add(candidate);
                        baseChunkCoordinates.Add(candidate);
                    }
                }
            }
        }

        private void PrepareBaseTransitionState(PlanetChunkLod requestedLod)
        {
            activeBaseChunkLods.Clear();
            for (int i = 0; i < baseChunkCoordinates.Count; i++)
            {
                PlanetGridCoordinates coordinates = baseChunkCoordinates[i];
                activeBaseChunkLods[coordinates] = SelectBaseChunkLod(requestedLod, coordinates);
            }
        }

        private PlanetChunkLod GetActiveBaseChunkLod(PlanetChunkLod requestedLod, PlanetGridCoordinates coordinates)
        {
            return activeBaseChunkLods.TryGetValue(coordinates, out PlanetChunkLod lod)
                ? lod
                : SelectBaseChunkLod(requestedLod, coordinates);
        }

        private int BuildTransitionFaces(PlanetGridCoordinates coordinates, PlanetChunkLod chunkLod, int outputChunkIndex)
        {
            if (!useBaseOctree || chunkLod == PlanetChunkLod.LOD0)
            {
                return 0;
            }

            int count = 0;
            AddTransitionFaceIfNeeded(coordinates, chunkLod, outputChunkIndex, 0, -1, ref count);
            AddTransitionFaceIfNeeded(coordinates, chunkLod, outputChunkIndex, 0, 1, ref count);
            AddTransitionFaceIfNeeded(coordinates, chunkLod, outputChunkIndex, 1, -1, ref count);
            AddTransitionFaceIfNeeded(coordinates, chunkLod, outputChunkIndex, 1, 1, ref count);
            AddTransitionFaceIfNeeded(coordinates, chunkLod, outputChunkIndex, 2, -1, ref count);
            AddTransitionFaceIfNeeded(coordinates, chunkLod, outputChunkIndex, 2, 1, ref count);
            return count;
        }

        private void AddTransitionFaceIfNeeded(
            PlanetGridCoordinates coordinates,
            PlanetChunkLod chunkLod,
            int outputChunkIndex,
            int axis,
            int sign,
            ref int count)
        {
            PlanetGridCoordinates neighbor = OffsetCoordinates(coordinates, axis, sign);
            if (!activeBaseChunkLods.TryGetValue(neighbor, out PlanetChunkLod neighborLod))
            {
                return;
            }

            if ((int)neighborLod != (int)chunkLod - 1)
            {
                return;
            }

            PlanetMarchingCubesChunkOrigin coarseOrigin = coordinates.ToChunkOrigin(chunkLod);
            int chunkSize = PlanetChunkLodUtility.GetChunkSizeForLod(chunkLod);
            transitionFaceScratch[count++] = new PlanetTransvoxelFaceDescriptor(
                coarseOrigin,
                chunkSize,
                axis,
                sign,
                outputChunkIndex,
                Mathf.Clamp(baseTransvoxelWidthCells, 0.1f, 2f));
        }

        private static PlanetGridCoordinates OffsetCoordinates(PlanetGridCoordinates coordinates, int axis, int sign)
        {
            switch (axis)
            {
                case 0:
                    return new PlanetGridCoordinates(coordinates.x + sign, coordinates.y, coordinates.z);
                case 1:
                    return new PlanetGridCoordinates(coordinates.x, coordinates.y + sign, coordinates.z);
                default:
                    return new PlanetGridCoordinates(coordinates.x, coordinates.y, coordinates.z + sign);
            }
        }

        private bool ShouldRebuildBaseForPlayerMovement()
        {
            if (!useBaseOctree || !hasLoadedBaseFocus)
            {
                return false;
            }

            Vector3 currentFocusGrid = CalculateBaseFocusGrid();
            float rebuildDistanceGrid = Mathf.Max(1, baseRebuildDistanceChunks) *
                                        PlanetMarchingCubesChunkRange.CanonicalChunkSize;
            return (currentFocusGrid - loadedBaseFocusGrid).sqrMagnitude >= rebuildDistanceGrid * rebuildDistanceGrid;
        }

        private Vector3 CalculateBaseFocusGrid()
        {
            if (player == null)
            {
                return Vector3.zero;
            }

            Vector3 gridPosition = PlanetCoordinateConverter.WorldToGrid(player.position, in recipe, in placement);
            float gridRadius = Mathf.Max(1f, recipe.GridRadius);
            return gridPosition.sqrMagnitude > gridRadius * gridRadius
                ? gridPosition.normalized * gridRadius
                : gridPosition;
        }

        private int CompareBaseChunkDistance(PlanetGridCoordinates a, PlanetGridCoordinates b)
        {
            float distanceA = CalculateBaseChunkDistanceSquared(a);
            float distanceB = CalculateBaseChunkDistanceSquared(b);
            return distanceA.CompareTo(distanceB);
        }

        private float CalculateBaseChunkDistanceSquared(PlanetGridCoordinates coordinates)
        {
            Vector3 chunkCenter = CalculateBaseChunkCenterGrid(coordinates);
            return (chunkCenter - baseFocusGrid).sqrMagnitude;
        }

        private PlanetChunkLod SelectBaseChunkLod(PlanetChunkLod requestedLod, PlanetGridCoordinates coordinates)
        {
            if (!useBaseOctree)
            {
                return requestedLod;
            }

            float chunkDistance = Mathf.Sqrt(CalculateBaseChunkDistanceSquared(coordinates)) /
                                  Mathf.Max(1f, PlanetMarchingCubesChunkRange.CanonicalChunkSize);
            PlanetChunkLod octreeLod;
            if (chunkDistance <= Mathf.Max(0, baseOctreeLod0RadiusChunks))
            {
                octreeLod = PlanetChunkLod.LOD0;
            }
            else if (chunkDistance <= Mathf.Max(baseOctreeLod0RadiusChunks, baseOctreeLod1RadiusChunks))
            {
                octreeLod = PlanetChunkLod.LOD1;
            }
            else
            {
                octreeLod = PlanetChunkLod.LOD2;
            }

            return (PlanetChunkLod)Mathf.Max((int)requestedLod, (int)octreeLod);
        }

        private static Vector3 CalculateBaseChunkCenterGrid(PlanetGridCoordinates coordinates)
        {
            float chunkSize = PlanetMarchingCubesChunkRange.CanonicalChunkSize;
            return new Vector3(
                (coordinates.x + 0.5f) * chunkSize,
                (coordinates.y + 0.5f) * chunkSize,
                (coordinates.z + 0.5f) * chunkSize);
        }

        private void SyncPlacementFromTransform()
        {
            placement.PlanetWorldCenter = transform.position;
            placement.PlanetRotation = transform.rotation;
        }

        private MeshFilter GetMeshFilter()
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            return meshFilter;
        }

        private PlanetGpuMarchingCubesSurface GetGpuSurface()
        {
            if (gpuSurface == null) gpuSurface = GetComponent<PlanetGpuMarchingCubesSurface>();
            if (gpuSurface == null) gpuSurface = gameObject.AddComponent<PlanetGpuMarchingCubesSurface>();
            return gpuSurface;
        }

        private void EnsureOceanSphere()
        {
            if (oceanSphere == null)
            {
                oceanSphere = new GameObject("Ocean");
                oceanSphere.name = "Ocean";
                oceanSphere.layer = gameObject.layer;
                oceanSphere.transform.SetParent(transform, false);
                oceanFilter = oceanSphere.AddComponent<MeshFilter>();
                oceanRenderer = oceanSphere.AddComponent<MeshRenderer>();
            }

            if (oceanFilter == null)
            {
                oceanFilter = oceanSphere.GetComponent<MeshFilter>();
                if (oceanFilter == null)
                {
                    oceanFilter = oceanSphere.AddComponent<MeshFilter>();
                }
            }

            if (oceanMesh == null)
            {
                oceanMesh = CreateOceanSphereMesh(OceanLongitudeSegments, OceanLatitudeSegments);
            }

            oceanFilter.sharedMesh = oceanMesh;

            if (oceanRenderer == null)
            {
                oceanRenderer = oceanSphere.GetComponent<MeshRenderer>();
                if (oceanRenderer == null)
                {
                    oceanRenderer = oceanSphere.AddComponent<MeshRenderer>();
                }
            }

            if (oceanRenderer != null)
            {
                oceanRenderer.sharedMaterial = ResolveOceanMaterial();
            }

            UpdateOceanSphere();
        }

        private void UpdateOceanSphere()
        {
            if (oceanSphere == null)
            {
                return;
            }

            float oceanRadius = Mathf.Max(0.0001f, recipe.GridRadius * recipe.WorldScale);
            Transform oceanTransform = oceanSphere.transform;
            oceanTransform.localPosition = Vector3.zero;
            oceanTransform.localRotation = Quaternion.identity;
            oceanTransform.localScale = Vector3.one * (oceanRadius * 2f);
        }

        private Material ResolveOceanMaterial()
        {
            if (oceanMaterial != null)
            {
                return oceanMaterial;
            }

            oceanMaterial = Resources.Load<Material>(DefaultOceanMaterialResourceName);
            return oceanMaterial;
        }

        private static Mesh CreateOceanSphereMesh(int longitudeSegments, int latitudeSegments)
        {
            int lon = Mathf.Max(8, longitudeSegments);
            int lat = Mathf.Max(4, latitudeSegments);
            int ringCount = lat - 1;
            int vertexCount = 2 + ringCount * lon;
            int triangleIndexCount = lon * 6 + Mathf.Max(0, ringCount - 1) * lon * 6;
            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            int[] triangles = new int[triangleIndexCount];

            vertices[0] = Vector3.up * 0.5f;
            normals[0] = Vector3.up;
            uvs[0] = new Vector2(0.5f, 1f);

            for (int ring = 1; ring <= ringCount; ring++)
            {
                float v = ring / (float)lat;
                float theta = v * Mathf.PI;
                float y = Mathf.Cos(theta) * 0.5f;
                float ringRadius = Mathf.Sin(theta) * 0.5f;
                int ringStart = 1 + (ring - 1) * lon;
                for (int segment = 0; segment < lon; segment++)
                {
                    float u = segment / (float)lon;
                    float phi = u * Mathf.PI * 2f;
                    Vector3 normal = new Vector3(
                        Mathf.Cos(phi) * ringRadius,
                        y,
                        Mathf.Sin(phi) * ringRadius).normalized;
                    int index = ringStart + segment;
                    vertices[index] = normal * 0.5f;
                    normals[index] = normal;
                    uvs[index] = new Vector2(u, 1f - v);
                }
            }

            int bottomIndex = vertexCount - 1;
            vertices[bottomIndex] = Vector3.down * 0.5f;
            normals[bottomIndex] = Vector3.down;
            uvs[bottomIndex] = new Vector2(0.5f, 0f);

            int triangle = 0;
            int firstRingStart = 1;
            for (int segment = 0; segment < lon; segment++)
            {
                int next = (segment + 1) % lon;
                triangles[triangle++] = 0;
                triangles[triangle++] = firstRingStart + segment;
                triangles[triangle++] = firstRingStart + next;
            }

            for (int ring = 1; ring < ringCount; ring++)
            {
                int currentRingStart = 1 + (ring - 1) * lon;
                int nextRingStart = currentRingStart + lon;
                for (int segment = 0; segment < lon; segment++)
                {
                    int next = (segment + 1) % lon;
                    int a = currentRingStart + segment;
                    int b = currentRingStart + next;
                    int c = nextRingStart + segment;
                    int d = nextRingStart + next;
                    triangles[triangle++] = a;
                    triangles[triangle++] = c;
                    triangles[triangle++] = b;
                    triangles[triangle++] = b;
                    triangles[triangle++] = c;
                    triangles[triangle++] = d;
                }
            }

            int lastRingStart = 1 + (ringCount - 1) * lon;
            for (int segment = 0; segment < lon; segment++)
            {
                int next = (segment + 1) % lon;
                triangles[triangle++] = bottomIndex;
                triangles[triangle++] = lastRingStart + next;
                triangles[triangle++] = lastRingStart + segment;
            }

            EnsureOutwardTriangleWinding(vertices, triangles);

            Mesh mesh = new Mesh
            {
                name = "PlanetDirector_OceanSphere_Runtime"
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void EnsureOutwardTriangleWinding(Vector3[] vertices, int[] triangles)
        {
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int aIndex = triangles[i];
                int bIndex = triangles[i + 1];
                int cIndex = triangles[i + 2];
                Vector3 a = vertices[aIndex];
                Vector3 b = vertices[bIndex];
                Vector3 c = vertices[cIndex];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                Vector3 center = (a + b + c) / 3f;
                if (Vector3.Dot(normal, center) >= 0f)
                {
                    continue;
                }

                triangles[i + 1] = cIndex;
                triangles[i + 2] = bIndex;
            }
        }

        private void ClearLegacyMesh()
        {
            MeshFilter target = GetMeshFilter();
            if (target != null)
            {
                target.sharedMesh = null;
            }
        }

        private void UpdateTopologyDebug()
        {
            SyncPlacementFromTransform();
            Vector3 playerGrid = PlanetCoordinateConverter.WorldToGrid(
                player.position,
                in recipe,
                in placement);
            int chunkSize = PlanetChunkTopologyDebugData.ChunkSize;
            PlanetGridCoordinates playerChunk = new PlanetGridCoordinates(
                Mathf.FloorToInt(playerGrid.x / chunkSize),
                Mathf.FloorToInt(playerGrid.y / chunkSize),
                Mathf.FloorToInt(playerGrid.z / chunkSize));
            topologyDebugCatalog.EnsureRecipe(in recipe, topologyDebugAirDensityThreshold);
            bool playerChunkChanged = !hasTopologyDebugPlayerChunk ||
                                      !topologyDebugPlayerChunk.Equals(playerChunk);
            if (playerChunkChanged)
            {
                topologyDebugPlayerChunk = playerChunk;
                hasTopologyDebugPlayerChunk = true;
            }

            topologyDebugCatalog.Prioritize(playerChunk);

            bool catalogChanged = topologyDebugCatalog.Tick(
                in recipe,
                topologyDebugChunksPerFrame,
                topologyDebugAirDensityThreshold);
            int settingsHash = CalculateTopologyDebugSettingsHash();
            int maxChunks = topologyDebugUseBaseBufferLimit
                ? Mathf.Max(1, baseOctreeMaxChunks)
                : Mathf.Max(1, topologyDebugMaxChunks);
            bool settingsChanged = topologyDebugSettingsHash != settingsHash;
            bool selectionIncomplete = topologyDebugSearch.Results.Count < maxChunks;
            bool playerTopologyAvailable = topologyDebugCatalog.TryGet(playerChunk, out _);
            bool canPublishFirstSelection = topologyDebugSearch.Results.Count == 0 && playerTopologyAvailable;
            bool catalogueBatchReady = topologyDebugCatalog.Count - topologyDebugCatalogCountAtLastSearch >=
                                       Mathf.Max(1, topologyDebugCatalogBatchSize);
            bool incompleteSelectionNeedsRefresh = catalogChanged &&
                                                   selectionIncomplete &&
                                                   (canPublishFirstSelection || catalogueBatchReady);
            if (!playerChunkChanged && !settingsChanged && !incompleteSelectionNeedsRefresh)
            {
                return;
            }

            int maxExpandedComponents = (int)Mathf.Min(
                int.MaxValue,
                (long)maxChunks * Mathf.Max(1, topologyDebugSearchExpansionMultiplier));
            using (TopologySearchMarker.Auto())
            {
                topologyDebugSearch.Rebuild(
                    topologyDebugCatalog,
                    playerGrid,
                    maxChunks,
                    maxExpandedComponents,
                    topologyDebugStraightRadiusChunks,
                    topologyDebugAirTraversalCost,
                    topologyDebugMixedTraversalCost,
                    topologyDebugStepHeuristicCost,
                    topologyDebugMaxPathCost,
                    !playerChunkChanged && !settingsChanged);
            }
            topologyDebugSettingsHash = settingsHash;
            topologyDebugCatalogCountAtLastSearch = topologyDebugCatalog.Count;
        }

        private int CalculateTopologyDebugSettingsHash()
        {
            unchecked
            {
                int hash = topologyDebugUseBaseBufferLimit ? 1 : 0;
                hash = (hash * 397) ^ topologyDebugMaxChunks;
                hash = (hash * 397) ^ baseOctreeMaxChunks;
                hash = (hash * 397) ^ topologyDebugStraightRadiusChunks;
                hash = (hash * 397) ^ topologyDebugSearchExpansionMultiplier;
                hash = (hash * 397) ^ topologyDebugCatalogBatchSize;
                hash = (hash * 397) ^ topologyDebugAirTraversalCost.GetHashCode();
                hash = (hash * 397) ^ topologyDebugMixedTraversalCost.GetHashCode();
                hash = (hash * 397) ^ topologyDebugStepHeuristicCost.GetHashCode();
                hash = (hash * 397) ^ topologyDebugAirDensityThreshold.GetHashCode();
                hash = (hash * 397) ^ topologyDebugMaxPathCost.GetHashCode();
                return hash;
            }
        }

        private void ResetTopologyDebug()
        {
            topologyDebugCatalog.Reset();
            topologyDebugSearch.Rebuild(null, Vector3.zero, 0, 0, 0, 0f, 0f, 0f, 0f, false);
            topologyDebugSettingsHash = 0;
            topologyDebugCatalogCountAtLastSearch = 0;
            hasTopologyDebugPlayerChunk = false;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.1f, 0.7f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, activationRange));
            DrawTopologyDebugGizmos();
        }

        private void DrawTopologyDebugGizmos()
        {
            if (!drawChunkPathCostGizmos || !Application.isPlaying)
            {
                return;
            }

            IReadOnlyList<PlanetChunkPathDebugResult> results = topologyDebugSearch.Results;
            if (results.Count == 0)
            {
                return;
            }

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            Gizmos.matrix = Matrix4x4.TRS(
                transform.position,
                transform.rotation,
                Vector3.one * Mathf.Max(0.0001f, recipe.WorldScale));
            float chunkSize = PlanetChunkTopologyDebugData.ChunkSize;
            float maxPathCost = Mathf.Max(0.0001f, topologyDebugMaxPathCost);
            topologyDebugSelectedChunks.Clear();
            for (int index = 0; index < results.Count; index++)
            {
                topologyDebugSelectedChunks.Add(results[index].Coordinates);
            }

            for (int index = 0; index < results.Count; index++)
            {
                PlanetChunkPathDebugResult result = results[index];
                float normalizedCost = Mathf.Clamp01(result.Cost / maxPathCost);
                Color color = Color.Lerp(
                    topologyDebugLowCostColor,
                    topologyDebugHighCostColor,
                    normalizedCost);
                Vector3 center = PlanetChunkPathDebugSearch.CalculateChunkCenterGrid(result.Coordinates);
                Color fillColor = color;
                fillColor.a *= 0.25f;
                Gizmos.color = fillColor;
                Gizmos.DrawCube(center, Vector3.one * chunkSize);
                Gizmos.color = color;
                Gizmos.DrawWireCube(center, Vector3.one * chunkSize);
                if (result.HasParent && topologyDebugSelectedChunks.Contains(result.Parent))
                {
                    Vector3 parentCenter = PlanetChunkPathDebugSearch.CalculateChunkCenterGrid(result.Parent);
                    Gizmos.DrawLine(parentCenter, center);
                }
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        private void OnDestroy()
        {
            topologyDebugCatalog.Dispose();
            if (oceanMesh != null)
            {
                Destroy(oceanMesh);
                oceanMesh = null;
            }
        }
    }
}
