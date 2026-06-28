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
        public void FibonacciDirectionsAreNormalized()
        {
            PlanetRecipe recipe = PlanetRecipe.Default();

            for (int i = 0; i < recipe.VoronoiDivision; i++)
            {
                Vector3 direction = PlanetGpuShapeCellBuilder.FibonacciDirection(i, recipe.VoronoiDivision, recipe.Seed);
                Assert.LessOrEqual(Mathf.Abs(1f - direction.magnitude), 0.00001f);
            }
        }

        [Test]
        public void CellStrideIsThirtyTwoBytes()
        {
            Assert.AreEqual(32, PlanetGpuShapeCell.Stride);
            Assert.AreEqual(64, PlanetGpuShapeParameters.Stride);
        }
    }
}
