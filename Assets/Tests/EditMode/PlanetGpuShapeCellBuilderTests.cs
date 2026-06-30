using MarchingCubesPlanet.Coordinates;
using MarchingCubesPlanet.Shape;
using NUnit.Framework;
using UnityEngine;

namespace MarchingCubesPlanet.Lab.Tests
{
    public sealed class PlanetGpuShapeCellBuilderTests
    {
        [Test]
        public void DefaultRecipeContainsInitialShapeValues()
        {
            PlanetRecipe recipe = PlanetRecipe.Default();

            Assert.AreEqual(100, recipe.VoronoiDivision);
            Assert.AreEqual(84, recipe.ContinentCells);
            Assert.AreEqual(0.16f, recipe.ContinentEdgeBlend);
            Assert.AreEqual(0.65f, recipe.ContinentEdgeWidthMin);
            Assert.AreEqual(1.75f, recipe.ContinentEdgeWidthMax);
            Assert.AreEqual(0.75f, recipe.ContinentEdgeShiftStrength);
            Assert.AreEqual(7f, recipe.SurfaceNoiseFrequency);
            Assert.AreEqual(0.08f, recipe.SurfaceNoiseAmplitude);
            Assert.AreEqual(4, recipe.SurfaceNoiseOctaves);
            Assert.AreEqual(2f, recipe.SurfaceNoiseLacunarity);
            Assert.AreEqual(0.5f, recipe.SurfaceNoisePersistence);
            Assert.AreEqual(3.5f, recipe.SurfaceNoiseResponsePower);
            Assert.AreEqual(10, recipe.MountainBiomeCells);
            Assert.AreEqual(1, recipe.MountainBiomeMinPeaks);
            Assert.AreEqual(4, recipe.MountainBiomeMaxPeaks);
            Assert.AreEqual(0.18f, recipe.MountainBiomeHeight);
            Assert.AreEqual(0.055f, recipe.MountainBiomePeakRadius);
            Assert.AreEqual(0.45f, recipe.MountainBiomePeakSpread);
            Assert.AreEqual(0.18f, recipe.MountainBiomeEdgeBlend);
            Assert.AreEqual(2.25f, recipe.MountainBiomePeakFalloff);
            Assert.IsTrue(PlanetRecipeValidator.Validate(in recipe, out _));
        }

        [Test]
        public void BuildProducesExpectedContinentCellCount()
        {
            PlanetRecipe recipe = PlanetRecipe.Default();
            PlanetGpuShapeCell[] cells = new PlanetGpuShapeCell[recipe.VoronoiDivision];

            PlanetGpuShapeBuildSummary summary = PlanetGpuShapeCellBuilder.Build(in recipe, cells);

            int continentCount = 0;
            int mountainBiomeCount = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].IsContinent)
                {
                    continentCount++;
                }

                if (cells[i].BiomeId == PlanetBiomeId.Mountain)
                {
                    mountainBiomeCount++;
                    Assert.IsTrue(cells[i].IsContinent);
                }
            }

            Assert.AreEqual(recipe.VoronoiDivision, summary.cellCount);
            Assert.AreEqual(recipe.ContinentCells, continentCount);
            Assert.AreEqual(recipe.MountainBiomeCells, mountainBiomeCount);
            Assert.AreEqual(recipe.ContinentCells, summary.continentCellCount);
            Assert.AreEqual(recipe.MountainBiomeCells, summary.mountainBiomeCellCount);
            Assert.AreEqual(recipe.VoronoiDivision * PlanetGpuShapeCell.Stride, summary.estimatedBytes);
        }

        [Test]
        public void RecipeRejectsInvalidContinentEdgeShapeValues()
        {
            PlanetRecipe recipe = PlanetRecipe.Default();
            recipe.ContinentEdgeWidthMin = 0f;

            Assert.IsFalse(PlanetRecipeValidator.Validate(in recipe, out string minMessage));
            StringAssert.Contains("ContinentEdgeWidthMin", minMessage);

            recipe = PlanetRecipe.Default();
            recipe.ContinentEdgeWidthMax = 0.5f;

            Assert.IsFalse(PlanetRecipeValidator.Validate(in recipe, out string maxMessage));
            StringAssert.Contains("ContinentEdgeWidthMax", maxMessage);

            recipe = PlanetRecipe.Default();
            recipe.ContinentEdgeShiftStrength = 2.1f;

            Assert.IsFalse(PlanetRecipeValidator.Validate(in recipe, out string shiftMessage));
            StringAssert.Contains("ContinentEdgeShiftStrength", shiftMessage);
        }

        [Test]
        public void RecipeRejectsInvalidSurfaceNoiseFractalValues()
        {
            PlanetRecipe recipe = PlanetRecipe.Default();
            recipe.SurfaceNoiseOctaves = 9;

            Assert.IsFalse(PlanetRecipeValidator.Validate(in recipe, out string octavesMessage));
            StringAssert.Contains("SurfaceNoiseOctaves", octavesMessage);

            recipe = PlanetRecipe.Default();
            recipe.SurfaceNoiseLacunarity = 0f;

            Assert.IsFalse(PlanetRecipeValidator.Validate(in recipe, out string lacunarityMessage));
            StringAssert.Contains("SurfaceNoiseLacunarity", lacunarityMessage);

            recipe = PlanetRecipe.Default();
            recipe.SurfaceNoisePersistence = 1.1f;

            Assert.IsFalse(PlanetRecipeValidator.Validate(in recipe, out string persistenceMessage));
            StringAssert.Contains("SurfaceNoisePersistence", persistenceMessage);

            recipe = PlanetRecipe.Default();
            recipe.SurfaceNoiseResponsePower = 0f;

            Assert.IsFalse(PlanetRecipeValidator.Validate(in recipe, out string responseMessage));
            StringAssert.Contains("SurfaceNoiseResponsePower", responseMessage);
        }

        [Test]
        public void RecipeRejectsInvalidMountainBiomeValues()
        {
            PlanetRecipe recipe = PlanetRecipe.Default();
            recipe.MountainBiomeCells = recipe.ContinentCells + 1;

            Assert.IsFalse(PlanetRecipeValidator.Validate(in recipe, out string countMessage));
            StringAssert.Contains("MountainBiomeCells", countMessage);

            recipe = PlanetRecipe.Default();
            recipe.MountainBiomeMinPeaks = 0;

            Assert.IsFalse(PlanetRecipeValidator.Validate(in recipe, out string minPeaksMessage));
            StringAssert.Contains("MountainBiomeMinPeaks", minPeaksMessage);

            recipe = PlanetRecipe.Default();
            recipe.MountainBiomeMaxPeaks = recipe.MountainBiomeMinPeaks - 1;

            Assert.IsFalse(PlanetRecipeValidator.Validate(in recipe, out string maxPeaksMessage));
            StringAssert.Contains("MountainBiomeMaxPeaks", maxPeaksMessage);

            recipe = PlanetRecipe.Default();
            recipe.MountainBiomeHeight = -0.01f;

            Assert.IsFalse(PlanetRecipeValidator.Validate(in recipe, out string heightMessage));
            StringAssert.Contains("MountainBiomeHeight", heightMessage);

            recipe = PlanetRecipe.Default();
            recipe.MountainBiomePeakRadius = 0f;

            Assert.IsFalse(PlanetRecipeValidator.Validate(in recipe, out string radiusMessage));
            StringAssert.Contains("MountainBiomePeakRadius", radiusMessage);

            recipe = PlanetRecipe.Default();
            recipe.MountainBiomePeakSpread = 2.1f;

            Assert.IsFalse(PlanetRecipeValidator.Validate(in recipe, out string spreadMessage));
            StringAssert.Contains("MountainBiomePeakSpread", spreadMessage);

            recipe = PlanetRecipe.Default();
            recipe.MountainBiomeEdgeBlend = 0f;

            Assert.IsFalse(PlanetRecipeValidator.Validate(in recipe, out string edgeMessage));
            StringAssert.Contains("MountainBiomeEdgeBlend", edgeMessage);

            recipe = PlanetRecipe.Default();
            recipe.MountainBiomePeakFalloff = 0f;

            Assert.IsFalse(PlanetRecipeValidator.Validate(in recipe, out string falloffMessage));
            StringAssert.Contains("MountainBiomePeakFalloff", falloffMessage);
        }

        [Test]
        public void GpuParametersPackContinentEdgeShapeValues()
        {
            PlanetRecipe recipe = PlanetRecipe.Default();
            recipe.ContinentEdgeWidthMin = 0.5f;
            recipe.ContinentEdgeWidthMax = 1.8f;
            recipe.ContinentEdgeShiftStrength = 0.9f;

            PlanetGpuShapeParameters parameters = PlanetGpuShapeParameters.FromRecipe(in recipe);

            Assert.AreEqual(0.5f, parameters.continentEdgeShape.x);
            Assert.AreEqual(1.8f, parameters.continentEdgeShape.y);
            Assert.AreEqual(0.9f, parameters.continentEdgeShape.z);
        }

        [Test]
        public void GpuParametersPackSurfaceNoiseFractalValues()
        {
            PlanetRecipe recipe = PlanetRecipe.Default();
            recipe.SurfaceNoiseOctaves = 6;
            recipe.SurfaceNoiseLacunarity = 2.3f;
            recipe.SurfaceNoisePersistence = 0.42f;
            recipe.SurfaceNoiseResponsePower = 2.7f;

            PlanetGpuShapeParameters parameters = PlanetGpuShapeParameters.FromRecipe(in recipe);

            Assert.AreEqual(6f, parameters.noiseFractal.x);
            Assert.AreEqual(2.3f, parameters.noiseFractal.y);
            Assert.AreEqual(0.42f, parameters.noiseFractal.z);
            Assert.AreEqual(2.7f, parameters.noiseFractal.w);
        }

        [Test]
        public void GpuParametersPackBiomeValues()
        {
            PlanetRecipe recipe = PlanetRecipe.Default();
            recipe.MountainBiomeMinPeaks = 2;
            recipe.MountainBiomeMaxPeaks = 3;
            recipe.MountainBiomeHeight = 0.22f;
            recipe.MountainBiomePeakRadius = 0.07f;
            recipe.MountainBiomePeakSpread = 0.5f;
            recipe.MountainBiomeEdgeBlend = 0.2f;
            recipe.MountainBiomePeakFalloff = 4f;

            PlanetGpuShapeParameters parameters = PlanetGpuShapeParameters.FromRecipe(in recipe);

            Assert.AreEqual(0.22f, parameters.biomeShape.x);
            Assert.AreEqual(0.07f, parameters.biomeShape.y);
            Assert.AreEqual(0.2f, parameters.biomeShape.z);
            Assert.AreEqual(0.5f, parameters.biomeShape.w);
            Assert.AreEqual(2f, parameters.mountainBiome.x);
            Assert.AreEqual(3f, parameters.mountainBiome.y);
            Assert.AreEqual(4f, parameters.mountainBiome.z);
        }

        [Test]
        public void BuildIsDeterministicForSameRecipe()
        {
            PlanetRecipe recipe = PlanetRecipe.Default();
            PlanetGpuShapeCell[] first = new PlanetGpuShapeCell[recipe.VoronoiDivision];
            PlanetGpuShapeCell[] second = new PlanetGpuShapeCell[recipe.VoronoiDivision];

            PlanetGpuShapeCellBuilder.Build(in recipe, first);
            PlanetGpuShapeCellBuilder.Build(in recipe, second);

            for (int i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(first[i].directionAndFlag, second[i].directionAndFlag);
                Assert.AreEqual(first[i].offsetRoughnessHash, second[i].offsetRoughnessHash);
            }
        }

        [Test]
        public void LegacyRandomDirectionsAreNormalized()
        {
            PlanetRecipe recipe = PlanetRecipe.Default();

            for (int i = 0; i < recipe.VoronoiDivision; i++)
            {
                Vector3 direction = PlanetGpuShapeCellBuilder.LegacyRandomUnitVector(recipe.Seed, i);
                Assert.LessOrEqual(Mathf.Abs(1f - direction.magnitude), 0.00001f);
            }
        }

        [Test]
        public void CellStrideIsThirtyTwoBytes()
        {
            Assert.AreEqual(32, PlanetGpuShapeCell.Stride);
            Assert.AreEqual(128, PlanetGpuShapeParameters.Stride);
        }
    }
}
