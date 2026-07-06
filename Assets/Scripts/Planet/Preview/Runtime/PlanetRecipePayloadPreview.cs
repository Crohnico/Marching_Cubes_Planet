using System.Threading.Tasks;
using MarchingCubesPlanet.Coordinates;
using MarchingCubesPlanet.MarchingCubes;
using UnityEngine;

namespace MarchingCubesPlanet.Preview
{
    [DisallowMultipleComponent]
    public sealed class PlanetRecipePayloadPreview : MonoBehaviour
    {
        public enum GenerationType
        {
            Shell,
            Chunk,
            Base
        }

        [SerializeField] private PlanetRecipe recipe = PlanetRecipe.Default();
        [SerializeField] private PlanetPlacement placement = PlanetPlacement.Default();
        private PlanetGrid planetGrid;
        private int index = -1;
        private bool isConcatenatingChunks;

        MeshFilter meshFilter;
        MeshRenderer meshRenderer;
        PlanetGpuMarchingCubesSurface gpuSurface;

        private void Start()
        {
            SyncPlacementFromTransform();

            meshFilter = GetMeshFilter();
            meshRenderer = GetMeshRenderer();
            gpuSurface = GetGpuSurface();
        }

        private MeshFilter GetMeshFilter()
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
            return meshFilter;
        }

        private MeshRenderer GetMeshRenderer()
        {
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
            return meshRenderer;
        }

        private PlanetGpuMarchingCubesSurface GetGpuSurface()
        {
            if (gpuSurface == null) gpuSurface = GetComponent<PlanetGpuMarchingCubesSurface>();
            if (gpuSurface == null) gpuSurface = gameObject.AddComponent<PlanetGpuMarchingCubesSurface>();
            return gpuSurface;
        }

        public void Generate(GenerationType type, PlanetChunkLod lod)
        {
            SyncPlacementFromTransform();

            if (type == GenerationType.Shell)
            {
                GenerateShell(lod);
                return;
            }

            if (type == GenerationType.Chunk)
            {
                if (planetGrid == null) GenerateGrid();
                index = -1;
                isConcatenatingChunks = true;
                GetGpuSurface().BeginChunkSequence();
                ConcatenateChunk(lod);
            }
        }

        public void GenerateRandomSeed(GenerationType type, PlanetChunkLod lod)
        {
            recipe.Seed = Random.Range(int.MinValue, int.MaxValue);
            planetGrid = null;
            Generate(type, lod);
        }

        public void GenerateGrid()
        {
            SyncPlacementFromTransform();
            planetGrid = PlanetGenerator.GenerateGrid(recipe);
            index = -1;
        }

        public async void GenerateChunk(PlanetChunkLod lod, PlanetGridCoordinates chunkID, System.Action onComplete = null)
        {
            ClearLegacyMesh();
            GetGpuSurface().GenerateChunk(
                recipe,
                placement,
                lod,
                chunkID,
                GetMeshRenderer());
            await Task.Yield();
            onComplete?.Invoke();
        }

        public void Release()
        {
            SyncPlacementFromTransform();
            GetGpuSurface().Release();
            PlanetGenerator.Release(GetMeshFilter(), GetMeshRenderer());
            planetGrid = null;
            index = -1;
            isConcatenatingChunks = false;
        }

        private void SyncPlacementFromTransform()
        {
            placement.PlanetWorldCenter = transform.position;
            placement.PlanetRotation = transform.rotation;
        }

        private void GenerateShell(PlanetChunkLod lod)
        {
            ClearLegacyMesh();
            GetGpuSurface().GenerateShell(
                recipe,
                placement,
                lod,
                GetMeshRenderer());
        }

        private void ConcatenateChunk(PlanetChunkLod lod)
        {
            if (!isConcatenatingChunks || planetGrid == null)
            {
                return;
            }

            index++;
            if (index >= planetGrid.InformationCount)
            {
                isConcatenatingChunks = false;
                return;
            }

            PlanetGridCoordinates chunkID = planetGrid.GetInfoCell(index);
            GenerateChunk(lod, chunkID, () => ConcatenateChunk(lod));
        }

        private void ClearLegacyMesh()
        {
            MeshFilter target = GetMeshFilter();
            if (target != null)
            {
                target.sharedMesh = null;
            }
        }
    }
}
