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
        private const string ShapeShaderAsset = "Assets/Shaders/Resources/Compute/PlanetShapeDensity.compute";
        private const string MarchingShaderResource = "Compute/PlanetMarchingCubes";
        private const string MarchingShaderAsset = "Assets/Shaders/Resources/Compute/PlanetMarchingCubes.compute";

        private const int ChunkExtractorLodCount = 3;

        private static readonly PlanetMarchingCubesMeshPainter Painter = new PlanetMarchingCubesMeshPainter();
        private static PlanetMarchingCubesExtractor chunkExtractor;
        private static PlanetGpuShapeEvaluator chunkShapeEvaluator;
        private static PlanetGpuShapeCell[] chunkShapeCells = Array.Empty<PlanetGpuShapeCell>();
        private static PlanetRecipe chunkShapeCellsRecipe;
        private static int chunkOutputVertexCapacity = PlanetMarchingCubesSettings.Lod0OutputVertexCapacityBudget;
        private static bool hasChunkShapeCellsRecipe;

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
            PlanetGpuShapeCell[] cells = GetChunkShapeCells(in lodRecipe);
            PlanetGpuShapeEvaluator shape = GetChunkShapeEvaluator();
            PlanetMarchingCubesExtractor extractor = GetChunkExtractor(lod);
            PlanetMarchingCubesSettings extractSettings = PlanetMarchingCubesSettings.Default();
            extractSettings.chunkRange.chunkSize = PlanetChunkLodUtility.GetChunkSizeForLod(lod);
            PlanetMarchingCubesPaintSettings paintSettings = PlanetMarchingCubesPaintSettings.Default();

            try
            {
                shape.InitializeReusable(LoadShader(ShapeShaderResource, ShapeShaderAsset), in lodRecipe, cells, PlanetGpuBufferMode.ComputeBuffer);
                ComputeShader marchingShader = LoadShader(MarchingShaderResource, MarchingShaderAsset);
                PlanetMarchingCubesChunkOrigin chunkOrigin = chunkID.ToChunkOrigin(lod);
                while (true)
                {
                    extractSettings.outputVertexCapacity = GetChunkOutputVertexCapacity(lod);
                    extractor.InitializeReusableSingleChunk(
                        marchingShader,
                        in lodRecipe,
                        in extractSettings,
                        shape,
                        PlanetGpuBufferMode.ComputeBuffer,
                        chunkOrigin);

                    PlanetMarchingCubesExtractionResult extraction = extractor.ExtractPlanetSurface();
                    if (extraction.HasOverflow)
                    {
                        GrowChunkOutputVertexCapacity(extraction.State);
                        continue;
                    }

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
            }
            finally
            {
                extractor.DetachShapeEvaluator();
            }
        }

        public static void Release(MeshFilter targetMeshFilter, MeshRenderer targetMeshRenderer)
        {
            Painter.Release(targetMeshFilter, targetMeshRenderer);
            ReleaseChunkExtractors();
            ReleaseChunkShapeResources();
        }

        private static PlanetMarchingCubesExtractor GetChunkExtractor(PlanetChunkLod lod)
        {
            ValidateChunkLod(lod);
            if (chunkExtractor == null)
            {
                chunkExtractor = new PlanetMarchingCubesExtractor();
            }

            return chunkExtractor;
        }

        private static void ReleaseChunkExtractors()
        {
            if (chunkExtractor == null)
            {
                return;
            }

            chunkExtractor.Release();
            chunkExtractor = null;
        }

        private static PlanetGpuShapeEvaluator GetChunkShapeEvaluator()
        {
            if (chunkShapeEvaluator == null)
            {
                chunkShapeEvaluator = new PlanetGpuShapeEvaluator();
            }

            return chunkShapeEvaluator;
        }

        private static PlanetGpuShapeCell[] GetChunkShapeCells(in PlanetRecipe lodRecipe)
        {
            if (chunkShapeCells.Length < lodRecipe.VoronoiDivision)
            {
                chunkShapeCells = new PlanetGpuShapeCell[lodRecipe.VoronoiDivision];
            }

            if (!hasChunkShapeCellsRecipe || !RecipeEquals(in chunkShapeCellsRecipe, in lodRecipe))
            {
                PlanetGpuShapeCellBuilder.Build(in lodRecipe, chunkShapeCells);
                chunkShapeCellsRecipe = lodRecipe;
                hasChunkShapeCellsRecipe = true;
            }

            return chunkShapeCells;
        }

        private static void ReleaseChunkShapeResources()
        {
            if (chunkShapeEvaluator != null)
            {
                chunkShapeEvaluator.Release();
                chunkShapeEvaluator = null;
            }

            chunkShapeCells = Array.Empty<PlanetGpuShapeCell>();
            chunkShapeCellsRecipe = default;
            chunkOutputVertexCapacity = PlanetMarchingCubesSettings.Lod0OutputVertexCapacityBudget;
            hasChunkShapeCellsRecipe = false;
        }

        private static int GetChunkOutputVertexCapacity(PlanetChunkLod lod)
        {
            return Math.Max(
                PlanetMarchingCubesSettings.GetOutputVertexCapacityBudgetForLod(lod),
                chunkOutputVertexCapacity);
        }

        private static void GrowChunkOutputVertexCapacity(PlanetMarchingCubesState state)
        {
            ulong requiredVertexCapacity = (ulong)state.triangleCountAttempted * 3UL;
            if (requiredVertexCapacity > int.MaxValue)
            {
                throw new InvalidOperationException("Chunk Marching Cubes output exceeds the supported int vertex capacity.");
            }

            int requestedCapacity = Math.Max(1, (int)requiredVertexCapacity);
            int grownCapacity = RoundUpToPowerOfTwo(requestedCapacity);
            if (grownCapacity <= chunkOutputVertexCapacity)
            {
                throw new InvalidOperationException("Chunk Marching Cubes overflow could not grow output capacity.");
            }

            chunkOutputVertexCapacity = grownCapacity;
        }

        private static void ValidateChunkLod(PlanetChunkLod lod)
        {
            int slot = (int)lod;
            if (slot < 0 || slot >= ChunkExtractorLodCount)
            {
                throw new ArgumentOutOfRangeException(nameof(lod), lod, "Unknown planet chunk LOD.");
            }
        }

        private static bool RecipeEquals(in PlanetRecipe left, in PlanetRecipe right)
        {
            return left.GridRadius == right.GridRadius &&
                   Mathf.Approximately(left.WorldScale, right.WorldScale) &&
                   left.Seed == right.Seed &&
                   Mathf.Approximately(left.IsoLevel, right.IsoLevel) &&
                   left.VoronoiDivision == right.VoronoiDivision &&
                   left.ContinentCells == right.ContinentCells &&
                   Mathf.Approximately(left.ContinentEdgeBlend, right.ContinentEdgeBlend) &&
                   Mathf.Approximately(left.ContinentEdgeWidthMin, right.ContinentEdgeWidthMin) &&
                   Mathf.Approximately(left.ContinentEdgeWidthMax, right.ContinentEdgeWidthMax) &&
                   Mathf.Approximately(left.ContinentEdgeShiftStrength, right.ContinentEdgeShiftStrength) &&
                   Mathf.Approximately(left.MinLandElevation, right.MinLandElevation) &&
                   Mathf.Approximately(left.MaxLandElevation, right.MaxLandElevation) &&
                   Mathf.Approximately(left.MinHeightModifier, right.MinHeightModifier) &&
                   Mathf.Approximately(left.MaxHeightModifier, right.MaxHeightModifier) &&
                   Mathf.Approximately(left.OceanDepth, right.OceanDepth) &&
                   Mathf.Approximately(left.MinimumOceanDepth, right.MinimumOceanDepth) &&
                   Mathf.Approximately(left.SurfaceNoiseAmplitude, right.SurfaceNoiseAmplitude) &&
                   Mathf.Approximately(left.SurfaceNoiseFrequency, right.SurfaceNoiseFrequency) &&
                   left.SurfaceNoiseOctaves == right.SurfaceNoiseOctaves &&
                   Mathf.Approximately(left.SurfaceNoiseLacunarity, right.SurfaceNoiseLacunarity) &&
                   Mathf.Approximately(left.SurfaceNoisePersistence, right.SurfaceNoisePersistence) &&
                   Mathf.Approximately(left.SurfaceNoiseResponsePower, right.SurfaceNoiseResponsePower) &&
                   left.MountainBiomeCells == right.MountainBiomeCells &&
                   left.MountainBiomeMinPeaks == right.MountainBiomeMinPeaks &&
                   left.MountainBiomeMaxPeaks == right.MountainBiomeMaxPeaks &&
                   Mathf.Approximately(left.MountainBiomeHeight, right.MountainBiomeHeight) &&
                   Mathf.Approximately(left.MountainBiomePeakRadius, right.MountainBiomePeakRadius) &&
                   Mathf.Approximately(left.MountainBiomePeakSpread, right.MountainBiomePeakSpread) &&
                   Mathf.Approximately(left.MountainBiomeEdgeBlend, right.MountainBiomeEdgeBlend) &&
                   Mathf.Approximately(left.MountainBiomePeakFalloff, right.MountainBiomePeakFalloff) &&
                   Mathf.Approximately(left.MinRoughness, right.MinRoughness) &&
                   Mathf.Approximately(left.MaxRoughness, right.MaxRoughness);
        }

        private static int RoundUpToPowerOfTwo(int value)
        {
            int safeValue = Mathf.Max(1, value);
            if (safeValue >= 1073741824)
            {
                return safeValue;
            }

            return Mathf.NextPowerOfTwo(safeValue);
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
