using MarchingCubesPlanet.Compute;
using MarchingCubesPlanet.Coordinates;
using MarchingCubesPlanet.MarchingCubes;
using MarchingCubesPlanet.Shape;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MarchingCubesPlanet.Tests
{
    public sealed class PlanetMarchingCubesSettingsTests
    {
        [Test]
        public void DefaultChunkRangeMatchesCanonicalCartesianGrid()
        {
            PlanetMarchingCubesChunkRange range = PlanetMarchingCubesChunkRange.Default();

            Assert.AreEqual(16, range.chunkSize);
            Assert.AreEqual(1, range.cellSizeGrid);
            Assert.AreEqual(4, range.safetyMargin);
            Assert.AreEqual(0, range.maxCandidateChunks);
        }

        [Test]
        public void ChunkRangeRejectsNonPositiveChunkSize()
        {
            PlanetMarchingCubesChunkRange range = PlanetMarchingCubesChunkRange.Default();
            range.chunkSize = 0;

            Assert.IsFalse(range.Validate(out string message));
            StringAssert.Contains("chunkSize", message);
        }

        [Test]
        public void ChunkRangeRejectsNonUnitCellSize()
        {
            PlanetMarchingCubesChunkRange range = PlanetMarchingCubesChunkRange.Default();
            range.cellSizeGrid = 2;

            Assert.IsFalse(range.Validate(out string message));
            StringAssert.Contains("cellSizeGrid", message);
        }

        [Test]
        public void SettingsValidateOutputVertexCapacity()
        {
            PlanetMarchingCubesSettings settings = PlanetMarchingCubesSettings.Default();
            settings.outputVertexCapacity = 0;

            Assert.IsFalse(settings.Validate(out string message));
            StringAssert.Contains("outputVertexCapacity", message);
        }

        [Test]
        public void SettingsEnsureDefaultsRecoversNewSerializedFields()
        {
            PlanetMarchingCubesSettings settings = default;

            settings.EnsureDefaults();

            Assert.IsTrue(settings.Validate(out string message), message);
            Assert.AreEqual(16, settings.chunkRange.chunkSize);
            Assert.AreEqual(1, settings.chunkRange.cellSizeGrid);
            Assert.AreEqual(4, settings.chunkRange.safetyMargin);
            Assert.AreEqual(3000000, settings.outputVertexCapacity);
        }

        [Test]
        public void SettingsCalculateChunkScopedOutputVertexCapacity()
        {
            Assert.AreEqual(7680, PlanetMarchingCubesSettings.CalculateMaxOutputVertexCapacityForChunkSize(8));
            Assert.AreEqual(61440, PlanetMarchingCubesSettings.CalculateMaxOutputVertexCapacityForChunkSize(16));
            Assert.AreEqual(491520, PlanetMarchingCubesSettings.CalculateMaxOutputVertexCapacityForChunkSize(32));
            Assert.AreEqual(8000, PlanetMarchingCubesSettings.GetOutputVertexCapacityBudgetForLod(PlanetChunkLod.LOD2));
            Assert.AreEqual(62000, PlanetMarchingCubesSettings.GetOutputVertexCapacityBudgetForLod(PlanetChunkLod.LOD1));
            Assert.AreEqual(500000, PlanetMarchingCubesSettings.GetOutputVertexCapacityBudgetForLod(PlanetChunkLod.LOD0));
            Assert.AreEqual(3, PlanetMarchingCubesSettings.CountOnlyOutputVertexCapacity);
        }

        [Test]
        public void ChunkRangeCalculatesShellFromRecipeDisplacement()
        {
            PlanetRecipe recipe = PlanetRecipe.Default();
            recipe.GridRadius = 2000;

            PlanetMarchingCubesChunkRange.CalculateSurfaceShell(
                in recipe,
                PlanetMarchingCubesChunkRange.DefaultSafetyMargin,
                out float innerRadius,
                out float outerRadius);

            Assert.That(innerRadius, Is.EqualTo(1356f).Within(0.01f));
            Assert.That(outerRadius, Is.EqualTo(2905f).Within(0.01f));
        }

        [Test]
        public void ChunkRangeDetectsAabbShellIntersection()
        {
            Assert.IsTrue(PlanetMarchingCubesChunkRange.AabbIntersectsSphericalShell(
                new Vector3(64f, -32f, -32f),
                new Vector3(128f, 32f, 32f),
                90f,
                110f));

            Assert.IsFalse(PlanetMarchingCubesChunkRange.AabbIntersectsSphericalShell(
                new Vector3(256f, 256f, 256f),
                new Vector3(320f, 320f, 320f),
                90f,
                110f));
        }

        [Test]
        public void ChunkRangeBuildsCartesianChunkOrigins()
        {
            PlanetRecipe recipe = PlanetRecipe.Default();
            recipe.GridRadius = 64;
            recipe.MinLandElevation = 0.01f;
            recipe.MaxLandElevation = 0.01f;
            recipe.MinHeightModifier = 1f;
            recipe.MaxHeightModifier = 1f;
            recipe.OceanDepth = 0.01f;
            recipe.MinimumOceanDepth = 0.01f;
            recipe.SurfaceNoiseAmplitude = 0f;

            PlanetMarchingCubesChunkRange range = PlanetMarchingCubesChunkRange.Default();
            PlanetMarchingCubesChunkOrigin[] chunks = range.BuildCandidateChunks(in recipe, out PlanetMarchingCubesChunkBuildStats stats);

            Assert.Greater(chunks.Length, 0);
            Assert.AreEqual(chunks.Length, stats.CandidateChunkCount);
            Assert.AreEqual((long)chunks.Length * 16L * 16L * 16L, stats.CandidateCellCount);
            Assert.AreEqual(0, chunks[0].x % 16);
            Assert.AreEqual(0, chunks[0].y % 16);
            Assert.AreEqual(0, chunks[0].z % 16);
        }

        [Test]
        public void GpuStructStridesMatchHlslContract()
        {
            Assert.AreEqual(32, PlanetMarchingCubesVertex.Stride);
            Assert.AreEqual(32, PlanetMarchingCubesState.Stride);
            Assert.AreEqual(16, PlanetMarchingCubesChunkOrigin.Stride);
        }

        [Test]
        public void GpuExtractionStoresSubstanceAndSurfaceAppearanceBeforeRendering()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders are required for the GPU material composition contract.");
            }

            PlanetRecipe recipe = PlanetRecipe.Default();
            recipe.GridRadius = 8;
            recipe.WorldScale = 1f;
            recipe.VoronoiDivision = 8;
            recipe.ContinentCells = 8;
            recipe.MountainBiomeCells = 2;
            recipe.SurfaceNoiseAmplitude = 0f;
            PlanetCaveSettings caves = recipe.CaveSystem;
            caves.Enabled = false;
            recipe.CaveSystem = caves;
            recipe.MaterialLayers = new[]
            {
                PlanetMaterialLayer.Base("Hierro", PlanetLayerMaterial.Iron, Color.gray)
                    .WithSurfaceAppearance(Color.green, 100)
            };

            PlanetGpuShapeCell[] shapeCells = new PlanetGpuShapeCell[recipe.VoronoiDivision];
            PlanetGpuShapeCellBuilder.Build(in recipe, shapeCells);
            PlanetGpuShapeEvaluator shape = new PlanetGpuShapeEvaluator();
            PlanetMarchingCubesExtractor extractor = new PlanetMarchingCubesExtractor();
            PlanetMarchingCubesSettings settings = PlanetMarchingCubesSettings.Default();
            settings.chunkRange.chunkSize = 16;
            settings.outputVertexCapacity = 100000;

            try
            {
                ComputeShader shapeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                    "Assets/Shaders/Resources/Compute/PlanetShapeDensity.compute");
                ComputeShader marchingShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                    "Assets/Shaders/Resources/Compute/PlanetMarchingCubes.compute");
                Assert.IsNotNull(shapeShader);
                Assert.IsNotNull(marchingShader);

                shape.Initialize(shapeShader, in recipe, shapeCells, PlanetGpuBufferMode.ComputeBuffer);
                extractor.InitializeSingleChunk(
                    marchingShader,
                    in recipe,
                    in settings,
                    shape,
                    PlanetGpuBufferMode.ComputeBuffer,
                    new PlanetMarchingCubesChunkOrigin(-8, -8, -8));

                PlanetMarchingCubesExtractionResult result = extractor.ExtractCandidateChunkSurface(0);

                Assert.Greater(result.VertexCount, 0);
                for (int i = 0; i < result.VertexCount; i++)
                {
                    float packedMaterial = result.Vertices[i].positionAndMaterial.w;
                    Assert.GreaterOrEqual(packedMaterial, PlanetMaterialVertexEncoding.PackedMarker);
                    Assert.AreEqual(
                        (int)PlanetLayerMaterial.Iron,
                        PlanetMaterialVertexEncoding.DecodeMaterialId(packedMaterial));
                    Assert.AreEqual(
                        PlanetMaterialAppearanceState.Surface,
                        PlanetMaterialVertexEncoding.DecodeAppearanceState(packedMaterial));
                }
            }
            finally
            {
                extractor.Release();
                shape.Release();
            }
        }
    }
}
