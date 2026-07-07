using System;
using System.Collections;
using System.Threading.Tasks;
using MarchingCubesPlanet.Coordinates;
using UnityEngine;

namespace MarchingCubesPlanet.MarchingCubes
{
    [DisallowMultipleComponent]
    public sealed class PlanetDirector : MonoBehaviour
    {
        [SerializeField] private PlanetRecipe recipe = PlanetRecipe.Default();
        [SerializeField] private PlanetPlacement placement = PlanetPlacement.Default();
        [SerializeField] private Transform player;
        [SerializeField] private float activationRange = 10000f;
        [SerializeField] private PlanetChunkLod shellLod = PlanetChunkLod.LOD2;
        [SerializeField] private PlanetChunkLod baseLod = PlanetChunkLod.LOD2;

        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private PlanetGpuMarchingCubesSurface gpuSurface;
        private PlanetGrid grid;
        private int chunkLoadIndex = -1;
        private int loadVersion;
        private bool isAlive;
        private bool isSetUp;
        private bool isBaseLoading;

        public PlanetGrid Grid => grid;
        public bool IsAlive => isAlive;
        public bool IsSetUp => isSetUp;

        public PlanetRecipe Recipe
        {
            get => recipe;
            set
            {
                recipe = value;
                grid = null;
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
            onComplete?.Invoke();
        }

        public void LoadBase(PlanetChunkLod lod, Action onComplete = null)
        {
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
            if (chunkLoadIndex >= grid.InformationCount)
            {
                isBaseLoading = false;
                onComplete?.Invoke();
                return;
            }

            PlanetGridCoordinates chunkID = grid.GetInfoCell(chunkLoadIndex);
            ClearLegacyMesh();
            GetGpuSurface().GenerateChunk(
                recipe,
                placement,
                lod,
                chunkID,
                meshRenderer);
            await Task.Yield();
            QueueChunkLoad(lod, sequence, onComplete);
        }

        private void CancelBaseLoad()
        {
            loadVersion++;
            isBaseLoading = false;
            chunkLoadIndex = -1;
        }

        private void GenerateGrid()
        {
            grid = PlanetGenerator.GenerateGrid(recipe);
            chunkLoadIndex = -1;
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
