using MarchingCubesPlanet.Coordinates;
using MarchingCubesPlanet.Lab;
using MarchingCubesPlanet.TrianglePools;
using UnityEngine;

namespace MarchingCubesPlanet.Preview
{
    [DisallowMultipleComponent]
    public sealed class PlanetRecipePayloadPreviewGenerationFlow : MonoBehaviour
    {
        private const int DefaultTemporaryTriangleCapacity = 1000000;

        [Header("Runtime Chain")]
        [SerializeField] private PlanetGpuShapeLab shapeLab;
        [SerializeField] private PlanetMarchingCubesLab marchingCubesLab;
        [SerializeField] private PlanetMarchingCubesPaintLab paintLab;

        [Header("Budget")]
        [SerializeField] private int minimumTemporaryTriangleCapacity = DefaultTemporaryTriangleCapacity;

        [Header("State")]
        [SerializeField] private string lastDiagnostic;

        public string LastDiagnostic => lastDiagnostic;

        public bool GenerateFromPreview(PlanetRecipePayloadPreview preview, Vector3 priorityOriginWorld)
        {
            if (preview == null)
            {
                lastDiagnostic = "Generate blocked: PlanetRecipePayloadPreview is missing.";
                return false;
            }

            PlanetRecipe sourceRecipe = preview.Recipe;
            if (!sourceRecipe.IsValid(out string recipeMessage))
            {
                lastDiagnostic = "Generate blocked: preview PlanetRecipe is invalid. " + recipeMessage;
                return false;
            }

            preview.EnsureRenderTargets(out MeshFilter targetMeshFilter, out MeshRenderer targetMeshRenderer);
            PlanetPlacement placement = BuildPreviewPlacement(preview);
            int requestedTriangleBudget = Mathf.Max(1, preview.RequestedTrianglePayload);
            return Generate(
                in sourceRecipe,
                placement,
                targetMeshFilter,
                targetMeshRenderer,
                requestedTriangleBudget,
                priorityOriginWorld);
        }

        public bool Generate(
            in PlanetRecipe sourceRecipe,
            PlanetPlacement placement,
            MeshFilter targetMeshFilter,
            MeshRenderer targetMeshRenderer,
            int requestedTriangleBudget,
            Vector3 priorityOriginWorld)
        {
            ResolveReferences();
            if (shapeLab == null || marchingCubesLab == null || paintLab == null)
            {
                lastDiagnostic = "Generate blocked: runtime chain is incomplete. Required modules: 06 Shape, 07 Marching Cubes, 08 Paint, 09 Environment.";
                return false;
            }

            Release();

            int safeTriangleBudget = Mathf.Max(1, requestedTriangleBudget);
            int temporaryTriangleCapacity = Mathf.Max(safeTriangleBudget, Mathf.Max(1, minimumTemporaryTriangleCapacity));
            PlanetTrianglePoolRegistry.SetEnvironmentTriangleBudget(safeTriangleBudget);
            PlanetTrianglePoolRegistry.SetFallbackPriorityOriginWorld(priorityOriginWorld);

            shapeLab.SetRecipe(in sourceRecipe);
            shapeLab.InitShapeGpu();
            if (!shapeLab.IsShapeGpuInitialized)
            {
                lastDiagnostic = "Generate blocked: 06 Shape GPU did not initialize. " +
                                 FormatLabDiagnostic(shapeLab.LastDiagnostic);
                return false;
            }

            marchingCubesLab.EnsureTemporaryOutputTriangleCapacity(temporaryTriangleCapacity);
            marchingCubesLab.InitMarchingCubesGpu();
            if (!marchingCubesLab.HasLiveResources)
            {
                lastDiagnostic = "Generate blocked: 07 Marching Cubes did not initialize. " +
                                 FormatLabDiagnostic(marchingCubesLab.LastDiagnostic);
                return false;
            }

            marchingCubesLab.ExtractPlanetSurface();
            if (marchingCubesLab.LastOverflow)
            {
                lastDiagnostic = "Generate blocked: 07 temporary triangle buffer overflow (" +
                                 FormatBytes(CalculateMarchingCubesTemporaryBufferBytes(temporaryTriangleCapacity)) +
                                 "). Attempted tris=" + marchingCubesLab.LastTriangleCountAttempted +
                                 ", capacity tris=" + temporaryTriangleCapacity + ".";
                return false;
            }

            if (marchingCubesLab.LastTriangleCountWritten == 0u)
            {
                lastDiagnostic = "Generate finished without visible planet surface triangles. Check 07 chunk/shape diagnostics.";
                return false;
            }

            paintLab.SetPlacement(placement);
            paintLab.UsePlanetSurfaceAtlas((int)marchingCubesLab.LastTriangleCountWritten);
            paintLab.PaintLastExtractionByChunks(targetMeshFilter, targetMeshRenderer, placement);

            if (!paintLab.HasLiveMesh)
            {
                lastDiagnostic = "Generate finished without visible 09 Environment triangles. " +
                                 FormatLabDiagnostic(paintLab.LastDiagnostic);
                return false;
            }

            lastDiagnostic = "Generated via runtime flow 06 -> 07 -> 10 chunk paint -> 09.";
            return true;
        }

        public void Release()
        {
            ResolveReferences();

            if (paintLab != null)
            {
                paintLab.ReleaseModule();
            }

            PlanetTrianglePoolRegistry.ReleaseAllSlots();

            if (marchingCubesLab != null)
            {
                marchingCubesLab.ReleaseModule();
            }

            if (shapeLab != null)
            {
                shapeLab.ReleaseModule();
            }
        }

        private void ResolveReferences()
        {
            if (shapeLab == null)
            {
                shapeLab = FindFirstObjectByType<PlanetGpuShapeLab>();
            }

            if (marchingCubesLab == null)
            {
                marchingCubesLab = FindFirstObjectByType<PlanetMarchingCubesLab>();
            }

            if (paintLab == null)
            {
                paintLab = FindFirstObjectByType<PlanetMarchingCubesPaintLab>();
            }
        }

        private static PlanetPlacement BuildPreviewPlacement(PlanetRecipePayloadPreview preview)
        {
            PlanetPlacement placement = new PlanetPlacement(preview.TransformPlanetWorldCenter)
            {
                PlanetRotation = preview.TransformPlanetRotation
            };
            return placement;
        }

        private static long CalculateMarchingCubesTemporaryBufferBytes(int temporaryTriangleCapacity)
        {
            return (long)Mathf.Max(0, temporaryTriangleCapacity) * 3L * 32L;
        }

        private static string FormatBytes(long bytes)
        {
            double mib = bytes / (1024.0 * 1024.0);
            return bytes + " bytes / " + mib.ToString("0.00") + " MiB";
        }

        private static string FormatLabDiagnostic(PlanetLabDiagnostic diagnostic)
        {
            if (string.IsNullOrWhiteSpace(diagnostic.title))
            {
                return "No lab diagnostic was reported.";
            }

            return diagnostic.title +
                   " Cause: " + diagnostic.probableCause +
                   " Action: " + diagnostic.recommendedAction;
        }
    }
}
