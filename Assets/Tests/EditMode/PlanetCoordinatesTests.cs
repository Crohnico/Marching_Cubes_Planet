using System;
using System.Reflection;
using MarchingCubesPlanet.Coordinates;
using NUnit.Framework;
using UnityEngine;

namespace MarchingCubesPlanet.Lab.Tests
{
    public sealed class PlanetCoordinatesTests
    {
        [Test]
        public void WorldRadiusAndDiameterAreDerived()
        {
            PlanetRecipe recipe = PlanetRecipe.Default();
            recipe.GridRadius = 1000;
            recipe.WorldScale = 4f;

            Assert.AreEqual(4000f, recipe.WorldRadius);
            Assert.AreEqual(8000f, recipe.WorldDiameter);
        }

        [Test]
        public void PlanetRecipeDoesNotStoreWorldRadiusAsEditableField()
        {
            FieldInfo[] fields = typeof(PlanetRecipe).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            for (int i = 0; i < fields.Length; i++)
            {
                Assert.AreNotEqual("worldRadius", fields[i].Name, "WorldRadius must stay derived from GridRadius * WorldScale.");
            }
        }

        [Test]
        public void GridToWorldAndWorldToGridRoundTrip()
        {
            PlanetRecipe recipe = PlanetRecipe.Default();
            PlanetPlacement placement = new PlanetPlacement(new Vector3(10f, 20f, 30f));
            Vector3 gridPosition = new Vector3(125.25f, -3.5f, 9f);

            Vector3 worldPosition = PlanetCoordinateConverter.GridToWorld(gridPosition, in recipe, in placement);
            Vector3 roundTrip = PlanetCoordinateConverter.WorldToGrid(worldPosition, in recipe, in placement);

            AssertVectorApproximately(gridPosition, roundTrip);
        }

        [Test]
        public void DistanceConversionsUseWorldScale()
        {
            PlanetRecipe recipe = PlanetRecipe.Default();
            recipe.WorldScale = 4f;

            Assert.AreEqual(40f, PlanetCoordinateConverter.GridDistanceToWorldDistance(10f, in recipe));
            Assert.AreEqual(10f, PlanetCoordinateConverter.WorldDistanceToGridDistance(40f, in recipe));
        }

        [Test]
        public void InvalidRecipeValuesAreRejected()
        {
            PlanetRecipe recipe = PlanetRecipe.Default();

            recipe.GridRadius = 0;
            Assert.IsFalse(PlanetRecipeValidator.Validate(in recipe, out _));

            recipe = PlanetRecipe.Default();
            recipe.WorldScale = 0f;
            Assert.IsFalse(PlanetRecipeValidator.Validate(in recipe, out _));
        }

        [Test]
        public void PlanetPlacementDoesNotModifyRecipe()
        {
            PlanetRecipe recipe = PlanetRecipe.Default();
            int originalGridRadius = recipe.GridRadius;
            float originalWorldScale = recipe.WorldScale;

            PlanetPlacement placement = new PlanetPlacement(new Vector3(100f, 0f, 0f));
            _ = PlanetCoordinateConverter.GridToWorld(Vector3.one, in recipe, in placement);

            Assert.AreEqual(originalGridRadius, recipe.GridRadius);
            Assert.AreEqual(originalWorldScale, recipe.WorldScale);
        }

        [Test]
        public void GridPositionToCellUsesMathematicalFloor()
        {
            Assert.AreEqual(new GridCellCoordinates(73, 99, 13), GridCellCoordinates.FromGridPosition(new Vector3(73.25f, 99.99f, 13.2f)));
            Assert.AreEqual(new GridCellCoordinates(-1, 0, 0), GridCellCoordinates.FromGridPosition(new Vector3(-0.01f, 0f, 0f)));
            Assert.AreEqual(new GridCellCoordinates(-1, 0, 0), GridCellCoordinates.FromGridPosition(new Vector3(-1f, 0f, 0f)));
            Assert.AreEqual(new GridCellCoordinates(-2, 0, 0), GridCellCoordinates.FromGridPosition(new Vector3(-1.01f, 0f, 0f)));
        }

        [Test]
        public void GridCellDerivesMinCenterAndMax()
        {
            GridCellCoordinates cell = new GridCellCoordinates(10, 33, 65);

            AssertVectorApproximately(new Vector3(10f, 33f, 65f), cell.Min);
            AssertVectorApproximately(new Vector3(10.5f, 33.5f, 65.5f), cell.Center);
            AssertVectorApproximately(new Vector3(11f, 34f, 66f), cell.Max);
        }

        [Test]
        public void GridCellToWorldBoundsUsesRecipeScale()
        {
            PlanetRecipe recipe = PlanetRecipe.Default();
            recipe.WorldScale = 4f;
            PlanetPlacement placement = PlanetPlacement.Default();
            GridCellCoordinates cell = new GridCellCoordinates(10, 33, 65);

            Bounds bounds = PlanetCoordinateConverter.GridCellToWorldBounds(cell, in recipe, in placement);

            AssertVectorApproximately(new Vector3(40f, 132f, 260f), bounds.min);
            AssertVectorApproximately(new Vector3(44f, 136f, 264f), bounds.max);
        }

        [Test]
        public void GridCellQueryCenterInsideIsDeterministic()
        {
            GridCellCoordinates[] first = new GridCellCoordinates[64];
            GridCellCoordinates[] second = new GridCellCoordinates[64];

            int firstCount = GridCellQuery.Sphere(Vector3.zero, 1.5f, GridCellQueryMode.CenterInside, first, out GridCellQueryResult firstResult);
            int secondCount = GridCellQuery.Sphere(Vector3.zero, 1.5f, GridCellQueryMode.CenterInside, second, out GridCellQueryResult secondResult);

            Assert.AreEqual(firstCount, secondCount);
            Assert.AreEqual(firstResult.MatchedCount, secondResult.MatchedCount);

            for (int i = 0; i < firstCount; i++)
            {
                Assert.AreEqual(first[i], second[i]);
            }
        }

        [Test]
        public void GridCellQueryIntersectsIsConservative()
        {
            GridCellCoordinates[] centerInside = new GridCellCoordinates[128];
            GridCellCoordinates[] intersects = new GridCellCoordinates[128];

            GridCellQuery.Sphere(Vector3.zero, 1.5f, GridCellQueryMode.CenterInside, centerInside, out GridCellQueryResult centerResult);
            GridCellQuery.Sphere(Vector3.zero, 1.5f, GridCellQueryMode.Intersects, intersects, out GridCellQueryResult intersectsResult);

            Assert.GreaterOrEqual(intersectsResult.MatchedCount, centerResult.MatchedCount);
        }

        [Test]
        public void GridCellQueryReportsOverflowWithoutAllocatingNewOutput()
        {
            GridCellCoordinates[] buffer = new GridCellCoordinates[1];

            int written = GridCellQuery.Sphere(Vector3.zero, 1.5f, GridCellQueryMode.Intersects, buffer, out GridCellQueryResult result);

            Assert.AreEqual(1, written);
            Assert.IsTrue(result.Overflow);
            Assert.Greater(result.MatchedCount, result.WrittenCount);
        }

        [Test]
        public void GridCellQueryDoesNotAllocateOnWarmPath()
        {
            GridCellCoordinates[] buffer = new GridCellCoordinates[128];
            GridCellQuery.Sphere(Vector3.zero, 1.5f, GridCellQueryMode.Intersects, buffer, out _);

            long before = GC.GetAllocatedBytesForCurrentThread();
            GridCellQuery.Sphere(Vector3.zero, 1.5f, GridCellQueryMode.Intersects, buffer, out _);
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(0, after - before);
        }

        private static void AssertVectorApproximately(Vector3 expected, Vector3 actual)
        {
            Assert.LessOrEqual((expected - actual).sqrMagnitude, 0.000001f);
        }
    }
}
