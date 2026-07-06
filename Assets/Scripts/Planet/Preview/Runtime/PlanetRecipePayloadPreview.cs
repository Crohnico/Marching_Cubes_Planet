using MarchingCubesPlanet.Coordinates;
using UnityEngine;

namespace MarchingCubesPlanet.Preview
{
    [DisallowMultipleComponent]
    public sealed class PlanetRecipePayloadPreview : MonoBehaviour
    {
        [SerializeField] private PlanetRecipe recipe = PlanetRecipe.Default();
        [SerializeField] private PlanetPlacement placement = PlanetPlacement.Default();
        [SerializeField, TextArea] private string lastDiagnostic;

        public PlanetRecipe Recipe => recipe;
        public PlanetPlacement Placement => placement;
        public string LastDiagnostic => lastDiagnostic;


        private void Awake()
        {
            SyncPlacementFromTransform();
        }

        public void Generate()
        {
            SyncPlacementFromTransform();

            if (!PlanetRecipeValidator.Validate(in recipe, out string recipeMessage))
            {
                lastDiagnostic = "Generate blocked: " + recipeMessage;
                return;
            }

            lastDiagnostic = "Recipe ready. Runtime planet generation is pending 10 rebuild.";
        }

        public void GenerateRandomSeed()
        {
            recipe.Seed = Random.Range(int.MinValue, int.MaxValue);
            Generate();
        }

        public void Release()
        {
            SyncPlacementFromTransform();
            lastDiagnostic = "Planet anchor released. No runtime mesh is owned by this component.";
        }

        public void ResetDemoRecipe()
        {
            recipe = PlanetRecipe.Default();
            SyncPlacementFromTransform();
            lastDiagnostic = "Recipe reset. PlanetPlacement was synced from Transform.";
        }

        private void SyncPlacementFromTransform()
        {
            placement.PlanetWorldCenter = transform.position;
            placement.PlanetRotation = transform.rotation;
        }
    }
}
