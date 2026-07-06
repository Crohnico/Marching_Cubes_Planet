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

        public PlanetRecipe Recipe => recipe;
        public PlanetPlacement Placement => placement;

        private void Awake()
        {
            SyncPlacementFromTransform();
        }

        public void Generate(GenerationType type, PlanetChunkLod lod)
        {
            SyncPlacementFromTransform();

            if (type == GenerationType.Shell)
            {
                GenerateShell(lod);
                return;
            }
        }

        public void GenerateRandomSeed(GenerationType type, PlanetChunkLod lod)
        {
            recipe.Seed = Random.Range(int.MinValue, int.MaxValue);
            Generate(type, lod);
        }

        public void Release()
        {
            SyncPlacementFromTransform();
            PlanetGenerator.Release(GetComponent<MeshFilter>(), GetComponent<MeshRenderer>());
        }

        public void ResetDemoRecipe()
        {
            recipe = PlanetRecipe.Default();
            SyncPlacementFromTransform();
        }

        private void SyncPlacementFromTransform()
        {
            placement.PlanetWorldCenter = transform.position;
            placement.PlanetRotation = transform.rotation;
        }

        private void GenerateShell(PlanetChunkLod lod)
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }

            PlanetGenerator.GenerateShell(
                recipe,
                placement,
                lod,
                meshFilter,
                meshRenderer);
        }
    }
}
