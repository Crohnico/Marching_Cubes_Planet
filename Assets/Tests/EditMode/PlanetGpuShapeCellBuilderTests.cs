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
            Assert.AreEqual(7f, recipe.SurfaceNoiseFrequency);
            Assert.AreEqual(4, recipe.SurfaceNoiseOctaves);
            Assert.AreEqual(2f, recipe.SurfaceNoiseLacunarity);
            Assert.AreEqual(0.5f, recipe.SurfaceNoisePersistence);
            Assert.IsTrue(PlanetRecipeValidator.Validate(in recipe, out _));
        }

        [Test]
        public void BuildProducesExpectedContinentCellCount()
        {
            PlanetRecipe recipe = PlanetRecipe.Default();
            PlanetGpuShapeCell[] cells = new PlanetGpuShapeCell[recipe.VoronoiDivision];

            PlanetGpuShapeBuildSummary summary = PlanetGpuShapeCellBuilder.Build(in recipe, cells);

            int continentCount = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].IsContinent)
                {
                    continentCount++;
                }
            }

            Assert.AreEqual(recipe.VoronoiDivision, summary.cellCount);
            Assert.AreEqual(recipe.ContinentCells, continentCount);
            Assert.AreEqual(recipe.ContinentCells, summary.continentCellCount);
            Assert.AreEqual(recipe.VoronoiDivision * PlanetGpuShapeCell.Stride, summary.estimatedBytes);
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
        }

        [Test]
        public void GpuParametersPackSurfaceNoiseFractalValues()
        {
            PlanetRecipe recipe = PlanetRecipe.Default();
            recipe.SurfaceNoiseOctaves = 6;
            recipe.SurfaceNoiseLacunarity = 2.3f;
            recipe.SurfaceNoisePersistence = 0.42f;

            PlanetGpuShapeParameters parameters = PlanetGpuShapeParameters.FromRecipe(in recipe);

            Assert.AreEqual(6f, parameters.noiseFractal.x);
            Assert.AreEqual(2.3f, parameters.noiseFractal.y);
            Assert.AreEqual(0.42f, parameters.noiseFractal.z);
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
            Assert.AreEqual(80, PlanetGpuShapeParameters.Stride);
        }
    }
}
