using MarchingCubesPlanet.Compute;
using MarchingCubesPlanet.Coordinates;
using MarchingCubesPlanet.Shape;
using UnityEngine;

namespace MarchingCubesPlanet.MarchingCubes
{
    public static class PlanetGenerator
    {
        private const string ShapeShaderResource = "Compute/PlanetShapeDensity";
        private const string ShapeShaderAsset = "Assets/Shaders/Compute/PlanetShapeDensity.compute";
        private const string MarchingShaderResource = "Compute/PlanetMarchingCubes";
        private const string MarchingShaderAsset = "Assets/Shaders/Compute/PlanetMarchingCubes.compute";

        private static readonly PlanetMarchingCubesMeshPainter Painter = new PlanetMarchingCubesMeshPainter();

        public static PlanetMarchingCubesPaintResult GenerateShell(
            PlanetRecipe recipe,
            PlanetPlacement placement,
            PlanetChunkLod lod,
            MeshFilter targetMeshFilter,
            MeshRenderer targetMeshRenderer)
        {
            PlanetRecipe lodRecipe = PlanetChunkLodUtility.BuildRecipeForLod(in recipe, lod);
            PlanetGpuShapeCell[] cells = new PlanetGpuShapeCell[lodRecipe.VoronoiDivision];
            PlanetGpuShapeCellBuilder.Build(in lodRecipe, cells);

            PlanetGpuShapeEvaluator shape = new PlanetGpuShapeEvaluator();
            PlanetMarchingCubesExtractor extractor = new PlanetMarchingCubesExtractor();
            PlanetMarchingCubesSettings extractSettings = PlanetMarchingCubesSettings.Default();
            PlanetMarchingCubesPaintSettings paintSettings = PlanetMarchingCubesPaintSettings.Default();

            shape.Initialize(LoadShader(ShapeShaderResource, ShapeShaderAsset), in lodRecipe, cells, PlanetGpuBufferMode.ComputeBuffer);
            extractor.Initialize(LoadShader(MarchingShaderResource, MarchingShaderAsset), in lodRecipe, in extractSettings, shape, PlanetGpuBufferMode.ComputeBuffer);

            PlanetMarchingCubesExtractionResult extraction = extractor.ExtractPlanetSurface();
            PlanetMarchingCubesPaintResult paint = Painter.Paint(
                targetMeshFilter,
                targetMeshRenderer,
                null,
                extraction,
                in lodRecipe,
                in placement,
                paintSettings);

            extractor.Release();
            shape.Release();
            return paint;
        }

        public static void Release(MeshFilter targetMeshFilter, MeshRenderer targetMeshRenderer)
        {
            Painter.Release(targetMeshFilter, targetMeshRenderer);
        }

        private static ComputeShader LoadShader(string resourcePath, string assetPath)
        {
            ComputeShader shader = Resources.Load<ComputeShader>(resourcePath);
#if UNITY_EDITOR
            if (shader == null)
            {
                shader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(assetPath);
            }
#endif
            return shader;
        }
    }
}
