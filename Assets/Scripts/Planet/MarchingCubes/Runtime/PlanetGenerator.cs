using System;
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

        private const int ChunkExtractorLodCount = 3;

        private static readonly PlanetMarchingCubesMeshPainter Painter = new PlanetMarchingCubesMeshPainter();
        private static readonly PlanetMarchingCubesExtractor[] ChunkExtractorsByLod = new PlanetMarchingCubesExtractor[ChunkExtractorLodCount];

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
            extractSettings.outputVertexCapacity = PlanetMarchingCubesSettings.CountOnlyOutputVertexCapacity;

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
            PlanetMarchingCubesExtractor extractor = GetChunkExtractor(lod);
            PlanetMarchingCubesSettings extractSettings = PlanetMarchingCubesSettings.Default();
            extractSettings.chunkRange.chunkSize = PlanetChunkLodUtility.GetChunkSizeForLod(lod);
            extractSettings.outputVertexCapacity = PlanetMarchingCubesSettings.GetOutputVertexCapacityBudgetForLod(lod);
            PlanetMarchingCubesPaintSettings paintSettings = PlanetMarchingCubesPaintSettings.Default();

            try
            {
                shape.Initialize(LoadShader(ShapeShaderResource, ShapeShaderAsset), in lodRecipe, cells, PlanetGpuBufferMode.ComputeBuffer);
                extractor.InitializeReusableSingleChunk(
                    LoadShader(MarchingShaderResource, MarchingShaderAsset),
                    in lodRecipe,
                    in extractSettings,
                    shape,
                    PlanetGpuBufferMode.ComputeBuffer,
                    chunkID.ToChunkOrigin(lod));

                PlanetMarchingCubesExtractionResult extraction = extractor.ExtractPlanetSurface();
                return Painter.PaintNamedExtraction(
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
            }
            finally
            {
                extractor.DetachShapeEvaluator();
                shape.Release();
            }
        }

        public static void Release(MeshFilter targetMeshFilter, MeshRenderer targetMeshRenderer)
        {
            Painter.Release(targetMeshFilter, targetMeshRenderer);
            ReleaseChunkExtractors();
        }

        private static PlanetMarchingCubesExtractor GetChunkExtractor(PlanetChunkLod lod)
        {
            int slot = GetChunkExtractorSlot(lod);
            PlanetMarchingCubesExtractor extractor = ChunkExtractorsByLod[slot];
            if (extractor == null)
            {
                extractor = new PlanetMarchingCubesExtractor();
                ChunkExtractorsByLod[slot] = extractor;
            }

            return extractor;
        }

        private static void ReleaseChunkExtractors()
        {
            for (int i = 0; i < ChunkExtractorsByLod.Length; i++)
            {
                PlanetMarchingCubesExtractor extractor = ChunkExtractorsByLod[i];
                if (extractor == null)
                {
                    continue;
                }

                extractor.Release();
                ChunkExtractorsByLod[i] = null;
            }
        }

        private static int GetChunkExtractorSlot(PlanetChunkLod lod)
        {
            int slot = (int)lod;
            if (slot < 0 || slot >= ChunkExtractorLodCount)
            {
                throw new ArgumentOutOfRangeException(nameof(lod), lod, "Unknown planet chunk LOD.");
            }

            return slot;
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
