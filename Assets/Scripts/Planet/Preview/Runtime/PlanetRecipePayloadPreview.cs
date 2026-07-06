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

        private void Start()
        {
            SyncPlacementFromTransform();

            meshFilter = GetMeshFilter();
            meshRenderer = GetMeshRenderer();
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

        public void Generate(GenerationType type, PlanetChunkLod lod)
        {
            SyncPlacementFromTransform();

            if (planetGrid == null) GenerateGrid();

            if (type == GenerationType.Shell)
            {
                GenerateShell(lod);
                return;
            }

            if (type == GenerationType.Chunk)
            {
                index = -1;
                isConcatenatingChunks = true;
                ConcatenateChunk(lod);
            }
        }

        public void GenerateRandomSeed(GenerationType type, PlanetChunkLod lod)
        {
            recipe.Seed = Random.Range(int.MinValue, int.MaxValue);
            planetGrid = null;
            GenerateGrid();
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
            PlanetGenerator.GenerateChunk(
                recipe,
                placement,
                lod,
                chunkID,
                GetMeshFilter(),
                GetMeshRenderer());
            await Task.Yield();
            onComplete?.Invoke();
        }

        public void Release()
        {
            SyncPlacementFromTransform();
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
            PlanetGenerator.GenerateShell(
                recipe,
                placement,
                lod,
                GetMeshFilter(),
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
    }
}
