using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using MarchingCubesPlanet.Coordinates;
using UnityEngine;

namespace MarchingCubesPlanet.MarchingCubes
{
    [DisallowMultipleComponent]
    public sealed class PlanetDirector : MonoBehaviour
    {
        private const string DefaultOceanMaterialResourceName = "PlanetOcean";

        [SerializeField] private PlanetRecipe recipe = PlanetRecipe.Default();
        [SerializeField] private PlanetPlacement placement = PlanetPlacement.Default();
        [SerializeField] private Transform player;
        [SerializeField] private float activationRange = 10000f;
        [SerializeField] private PlanetChunkLod shellLod = PlanetChunkLod.LOD2;
        [SerializeField] private PlanetChunkLod baseLod = PlanetChunkLod.LOD0;
        [SerializeField] private Material oceanMaterial;
        [SerializeField] private bool useBaseOctree = true;
        [SerializeField] private int baseOctreeLod0RadiusChunks = 3;
        [SerializeField] private int baseOctreeLod1RadiusChunks = 8;
        [SerializeField] private int baseOctreeMaxChunks = 256;
        [SerializeField] private int baseOctreeOutputVertexCapacity = 1500000;

        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private PlanetGpuMarchingCubesSurface gpuSurface;
        private GameObject oceanSphere;
        private MeshRenderer oceanRenderer;
        private PlanetGrid grid;
        private readonly List<PlanetGridCoordinates> baseChunkCoordinates = new List<PlanetGridCoordinates>(256);
        private int chunkLoadIndex = -1;
        private int loadVersion;
        private bool isAlive;
        private bool isSetUp;
        private bool isBaseLoading;
        private Vector3 baseFocusGrid;

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
                UpdateOceanSphere();
            }
        }

        public PlanetPlacement Placement => placement;

        private IEnumerator Start()
        {
            SyncPlacementFromTransform();
            gpuSurface = GetGpuSurface();
            GenerateGrid();

            if (player == null && Camera.main != null)
            {
                player = Camera.main.transform;
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

            bool nextAlive = Vector3.Distance(player.position, transform.position) <= activationRange;
            if (nextAlive == isAlive)
            {
                return;
            }

            isAlive = nextAlive;
            if (isAlive && !isSetUp)
            {
                LoadBase(baseLod, () =>
                {
                    ReleaseShell();
                    isSetUp = true;
                    Debug.Log("BaseLoaded", this);
                });
                return;
            }

            if (!isAlive)
            {
                if (isBaseLoading)
                {
                    CancelBaseLoad();
                    ReleaseBase();
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
        }

        public void LoadShell(PlanetChunkLod lod, Action onComplete = null, bool keepBaseVisibleUntilComplete = false)
        {
            loadVersion++;
            isBaseLoading = false;
            chunkLoadIndex = -1;

            ClearLegacyMesh();
            GetGpuSurface().GenerateShell(
                recipe,
                placement,
                lod,
                meshRenderer,
                keepBaseVisibleUntilComplete);
            EnsureOceanSphere();
            onComplete?.Invoke();
        }

        public void LoadBase(PlanetChunkLod lod, Action onComplete = null)
        {
            if (grid == null)
            {
                GenerateGrid();
            }

            grid.CopyInformationCells(baseChunkCoordinates);
            PrepareBaseOctree();
            int sequence = ++loadVersion;
            chunkLoadIndex = -1;
            isBaseLoading = true;
            GetGpuSurface().BeginChunkSequence(true);
            QueueChunkLoad(lod, sequence, onComplete);
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
                onComplete?.Invoke();
                return;
            }

            PlanetGridCoordinates chunkID = baseChunkCoordinates[chunkLoadIndex];
            PlanetChunkLod chunkLod = SelectBaseChunkLod(lod, chunkID);
            ClearLegacyMesh();
            GetGpuSurface().GenerateChunkIntoBase(
                recipe,
                placement,
                chunkLod,
                chunkID,
                meshRenderer,
                useBaseOctree ? baseOctreeOutputVertexCapacity : PlanetMarchingCubesSettings.DefaultOutputVertexCapacity);
            await Task.Yield();
            QueueChunkLoad(lod, sequence, onComplete);
        }

        private void CancelBaseLoad()
        {
            loadVersion++;
            isBaseLoading = false;
            chunkLoadIndex = -1;
            baseChunkCoordinates.Clear();
        }

        private void GenerateGrid()
        {
            grid = PlanetGenerator.GenerateGrid(recipe);
            chunkLoadIndex = -1;
        }

        private void PrepareBaseOctree()
        {
            if (!useBaseOctree)
            {
                return;
            }

            baseFocusGrid = CalculateBaseFocusGrid();
            baseChunkCoordinates.Sort(CompareBaseChunkDistance);

            int maxChunks = Mathf.Max(0, baseOctreeMaxChunks);
            if (maxChunks > 0 && baseChunkCoordinates.Count > maxChunks)
            {
                baseChunkCoordinates.RemoveRange(maxChunks, baseChunkCoordinates.Count - maxChunks);
            }
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
                oceanSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                oceanSphere.name = "Ocean";
                oceanSphere.layer = gameObject.layer;
                oceanSphere.transform.SetParent(transform, false);
                Collider oceanCollider = oceanSphere.GetComponent<Collider>();
                if (oceanCollider != null)
                {
                    Destroy(oceanCollider);
                }
            }

            if (oceanRenderer == null)
            {
                oceanRenderer = oceanSphere.GetComponent<MeshRenderer>();
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

        private void ClearLegacyMesh()
        {
            MeshFilter target = GetMeshFilter();
            if (target != null)
            {
                target.sharedMesh = null;
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.1f, 0.7f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, activationRange));
        }
    }
}
