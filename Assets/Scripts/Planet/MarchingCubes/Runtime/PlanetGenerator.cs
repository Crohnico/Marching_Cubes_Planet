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

        public static PlanetGrid GenerateGrid(PlanetRecipe recipe)
        {
            PlanetChunkLod lod = PlanetChunkLodUtility.BaseRecipeLod;
            PlanetRecipe lodRecipe = PlanetChunkLodUtility.BuildRecipeForLod(in recipe, lod);
            PlanetGpuShapeCell[] cells = new PlanetGpuShapeCell[lodRecipe.VoronoiDivision];
            PlanetGpuShapeCellBuilder.Build(in lodRecipe, cells);

            PlanetGpuShapeEvaluator shape = new PlanetGpuShapeEvaluator();
            PlanetMarchingCubesExtractor extractor = new PlanetMarchingCubesExtractor();
            PlanetMarchingCubesSettings extractSettings = PlanetMarchingCubesSettings.Default();
            extractSettings.chunkRange.chunkSize = PlanetChunkLodUtility.GetChunkSizeForLod(lod);

            try
            {
                shape.Initialize(LoadShader(ShapeShaderResource, ShapeShaderAsset), in lodRecipe, cells, PlanetGpuBufferMode.ComputeBuffer);
                extractor.Initialize(
                    LoadShader(MarchingShaderResource, MarchingShaderAsset),
                    in lodRecipe,
                    in extractSettings,
                    shape,
                    PlanetGpuBufferMode.ComputeBuffer);

                PlanetGrid grid = new PlanetGrid(extractor.CandidateChunkCount);
                for (int i = 0; i < extractor.CandidateChunkCount; i++)
                {
                    PlanetMarchingCubesState state = extractor.CountCandidateChunkSurface(i);
                    if (state.triangleCountAttempted == 0u)
                    {
                        continue;
                    }

                    if (extractor.TryGetCandidateChunkOrigin(i, out PlanetMarchingCubesChunkOrigin origin))
                    {
                        PlanetGridCoordinates coordinates = PlanetGridCoordinates.FromChunkOrigin(
                            origin,
                            extractSettings.chunkRange.chunkSize);
                        grid.Set(coordinates, 1u);
                    }
                }

                return grid;
            }
            finally
            {
                extractor.Release();
                shape.Release();
            }
        }

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

        public static PlanetMarchingCubesPaintResult GenerateChunk(
            PlanetRecipe recipe,
            PlanetPlacement placement,
            PlanetChunkLod lod,
            PlanetGridCoordinates chunkID,
            MeshFilter targetMeshFilter,
            MeshRenderer targetMeshRenderer)
        {
            PlanetRecipe lodRecipe = PlanetChunkLodUtility.BuildRecipeForLod(in recipe, lod);
            PlanetGpuShapeCell[] cells = new PlanetGpuShapeCell[lodRecipe.VoronoiDivision];
            PlanetGpuShapeCellBuilder.Build(in lodRecipe, cells);

            PlanetGpuShapeEvaluator shape = new PlanetGpuShapeEvaluator();
            PlanetMarchingCubesExtractor extractor = new PlanetMarchingCubesExtractor();
            PlanetMarchingCubesSettings extractSettings = PlanetMarchingCubesSettings.Default();
            extractSettings.chunkRange.chunkSize = PlanetChunkLodUtility.GetChunkSizeForLod(lod);
            PlanetMarchingCubesPaintSettings paintSettings = PlanetMarchingCubesPaintSettings.Default();

            shape.Initialize(LoadShader(ShapeShaderResource, ShapeShaderAsset), in lodRecipe, cells, PlanetGpuBufferMode.ComputeBuffer);
            extractor.InitializeSingleChunk(
                LoadShader(MarchingShaderResource, MarchingShaderAsset),
                in lodRecipe,
                in extractSettings,
                shape,
                PlanetGpuBufferMode.ComputeBuffer,
                chunkID.ToChunkOrigin(lod));

            PlanetMarchingCubesExtractionResult extraction = extractor.ExtractPlanetSurface();
            PlanetMarchingCubesPaintResult paint = Painter.PaintNamedExtraction(
                targetMeshFilter,
                targetMeshRenderer,
                null,
                "chunk_" + chunkID,
                -1,
                extraction,
                in lodRecipe,
                in placement,
                paintSettings,
                null,
                -1);

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
