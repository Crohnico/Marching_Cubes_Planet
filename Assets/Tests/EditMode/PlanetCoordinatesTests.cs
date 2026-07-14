using System;
using System.Reflection;
using MarchingCubesPlanet.Coordinates;
using MarchingCubesPlanet.MarchingCubes;
using NUnit.Framework;
using UnityEngine;

namespace MarchingCubesPlanet.Tests
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
        public void GridToWorldAppliesPlanetRotation()
        {
            PlanetRecipe recipe = PlanetRecipe.Default();
            recipe.WorldScale = 4f;
            PlanetPlacement placement = new PlanetPlacement(
                UniversePosition.Zero,
                UniversePosition.Zero,
                Quaternion.Euler(0f, 90f, 0f));
            Vector3 gridPosition = new Vector3(1f, 0f, 0f);

            Vector3 worldPosition = PlanetCoordinateConverter.GridToWorld(gridPosition, in recipe, in placement);
            Vector3 expectedWorldPosition = placement.PlanetRotation * new Vector3(4f, 0f, 0f);
            Vector3 roundTrip = PlanetCoordinateConverter.WorldToGrid(worldPosition, in recipe, in placement);

            AssertVectorApproximately(expectedWorldPosition, worldPosition);
            AssertVectorApproximately(gridPosition, roundTrip);
        }

        [Test]
        public void ActiveOriginChangesWorldPositionButNotGridIdentity()
        {
            PlanetRecipe recipe = PlanetRecipe.Default();
            Vector3 gridPosition = new Vector3(1f, 2f, 3f);
            UniversePosition planetCenter = new UniversePosition(10000d, 20000d, 30000d);
            PlanetPlacement firstFrame = new PlanetPlacement(
                planetCenter,
                new UniversePosition(9000d, 19000d, 29000d),
                Quaternion.Euler(0f, 45f, 0f));
            PlanetPlacement secondFrame = new PlanetPlacement(
                planetCenter,
                new UniversePosition(9500d, 19500d, 29500d),
                Quaternion.Euler(0f, 45f, 0f));

            UniversePosition firstStellar = PlanetCoordinateConverter.GridToStellar(gridPosition, in recipe, in firstFrame);
            UniversePosition secondStellar = PlanetCoordinateConverter.GridToStellar(gridPosition, in recipe, in secondFrame);
            Vector3 firstWorld = PlanetCoordinateConverter.GridToWorld(gridPosition, in recipe, in firstFrame);
            Vector3 secondWorld = PlanetCoordinateConverter.GridToWorld(gridPosition, in recipe, in secondFrame);
            Vector3 firstRoundTrip = PlanetCoordinateConverter.WorldToGrid(firstWorld, in recipe, in firstFrame);
            Vector3 secondRoundTrip = PlanetCoordinateConverter.WorldToGrid(secondWorld, in recipe, in secondFrame);

            Assert.AreEqual(firstStellar, secondStellar);
            Assert.AreNotEqual(firstWorld, secondWorld);
            AssertVectorApproximately(gridPosition, firstRoundTrip);
            AssertVectorApproximately(gridPosition, secondRoundTrip);
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

            recipe = PlanetRecipe.Default();
            recipe.VoronoiDivision = 0;
            Assert.IsFalse(PlanetRecipeValidator.Validate(in recipe, out _));

            recipe = PlanetRecipe.Default();
            recipe.ContinentCells = recipe.VoronoiDivision + 1;
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
        public void BaseLayerCanDefineThePlanetsMainSubstance()
        {
            PlanetMaterialLayer layer = PlanetMaterialLayer.Base(
                "Hierro",
                PlanetLayerMaterial.Iron,
                Color.gray);

            Assert.AreEqual(PlanetLayerOperation.BaseMaterial, layer.Operation);
            Assert.AreEqual(PlanetLayerMaterial.Iron, layer.Material);
            Assert.AreEqual(0, layer.MinAppearance);
            Assert.AreEqual(255, layer.MaxAppearance);
            Assert.AreEqual(100, layer.Abundance);
        }

        [Test]
        public void AirCannotBeUsedAsMaterialReplacement()
        {
            PlanetRecipe recipe = PlanetRecipe.Default();
            recipe.MaterialLayers = new[]
            {
                PlanetMaterialLayer.Base("Aire", PlanetLayerMaterial.Air, Color.clear)
            };

            Assert.IsFalse(PlanetRecipeValidator.Validate(in recipe, out string message));
            StringAssert.Contains("Air is a density operation", message);
        }

        [Test]
        public void ReplaceCreatesCompactMaterialAuthoring()
        {
            PlanetMaterialLayer layer = PlanetMaterialLayer.Replace(
                "Silicio alto",
                PlanetLayerMaterial.Silicon,
                Color.white,
                180,
                230,
                42,
                75);

            Assert.AreEqual(PlanetLayerOperation.ReplaceMaterial, layer.Operation);
            Assert.AreEqual(PlanetLayerMaterial.Silicon, layer.Material);
            Assert.AreEqual(180, layer.MinAppearance);
            Assert.AreEqual(230, layer.MaxAppearance);
            Assert.AreEqual(42, layer.Abundance);
            Assert.AreEqual(75, layer.Coherence);
        }

        [Test]
        public void SurfaceAndUnderwaterAppearancesKeepLogicalSubstance()
        {
            PlanetMaterialLayer layer = PlanetMaterialLayer.Replace(
                    "Cobre",
                    PlanetLayerMaterial.Copper,
                    new Color(0.72f, 0.35f, 0.16f),
                    20,
                    220,
                    30,
                    80)
                .WithSurfaceAppearance(Color.green, 65, 61, 90, 210)
                .WithUnderwaterAppearance(Color.black, 40, 37);

            Assert.AreEqual(PlanetLayerMaterial.Copper, layer.Material);
            Assert.AreEqual(PlanetMaterialEnvironmentBehaviour.OverrideColor, layer.SurfaceBehaviour);
            Assert.AreEqual(65, layer.SurfaceProbability);
            Assert.AreEqual(61, layer.SurfaceCoherence);
            Assert.AreEqual(90, layer.SurfaceMinAppearance);
            Assert.AreEqual(210, layer.SurfaceMaxAppearance);
            Assert.AreEqual(Color.green, layer.SurfaceAtlasColor);
            Assert.AreEqual(PlanetMaterialEnvironmentBehaviour.OverrideColor, layer.UnderwaterBehaviour);
            Assert.AreEqual(40, layer.UnderwaterProbability);
            Assert.AreEqual(37, layer.UnderwaterCoherence);
            Assert.AreEqual(Color.black, layer.UnderwaterAtlasColor);
        }

        [Test]
        public void VisualColorDoesNotInvalidateGeometryCacheIdentity()
        {
            PlanetRecipe redRecipe = PlanetRecipe.Default();
            redRecipe.MaterialLayers = new[]
            {
                PlanetMaterialLayer.Base("Hierro", PlanetLayerMaterial.Iron, Color.red)
            };
            PlanetRecipe blueRecipe = PlanetRecipe.Default();
            blueRecipe.MaterialLayers = new[]
            {
                PlanetMaterialLayer.Base("Hierro", PlanetLayerMaterial.Iron, Color.blue)
            };

            Assert.AreEqual(
                PlanetChunkMeshCache.BuildPlanetId(in redRecipe),
                PlanetChunkMeshCache.BuildPlanetId(in blueRecipe));
        }

        [Test]
        public void LogicalSubstanceInvalidatesGeometryCacheIdentity()
        {
            PlanetRecipe ironRecipe = PlanetRecipe.Default();
            ironRecipe.MaterialLayers = new[]
            {
                PlanetMaterialLayer.Base("Principal", PlanetLayerMaterial.Iron, Color.gray)
            };
            PlanetRecipe siliconRecipe = PlanetRecipe.Default();
            siliconRecipe.MaterialLayers = new[]
            {
                PlanetMaterialLayer.Base("Principal", PlanetLayerMaterial.Silicon, Color.gray)
            };

            Assert.AreNotEqual(
                PlanetChunkMeshCache.BuildPlanetId(in ironRecipe),
                PlanetChunkMeshCache.BuildPlanetId(in siliconRecipe));
        }

        [Test]
        public void AppearanceProbabilityInvalidatesGeometryCacheButColorDoesNot()
        {
            PlanetRecipe first = PlanetRecipe.Default();
            first.MaterialLayers = new[]
            {
                PlanetMaterialLayer.Base("Tierra", PlanetLayerMaterial.BasicTerrain, Color.gray)
                    .WithSurfaceAppearance(Color.green, 25)
            };
            PlanetRecipe second = PlanetRecipe.Default();
            second.MaterialLayers = new[]
            {
                PlanetMaterialLayer.Base("Tierra", PlanetLayerMaterial.BasicTerrain, Color.gray)
                    .WithSurfaceAppearance(Color.red, 25)
            };
            PlanetRecipe third = PlanetRecipe.Default();
            third.MaterialLayers = new[]
            {
                PlanetMaterialLayer.Base("Tierra", PlanetLayerMaterial.BasicTerrain, Color.gray)
                    .WithSurfaceAppearance(Color.red, 75)
            };
            PlanetRecipe fourth = PlanetRecipe.Default();
            fourth.MaterialLayers = new[]
            {
                PlanetMaterialLayer.Base("Tierra", PlanetLayerMaterial.BasicTerrain, Color.gray)
                    .WithSurfaceAppearance(Color.red, 75, 90)
            };
            PlanetRecipe fifth = PlanetRecipe.Default();
            fifth.MaterialLayers = new[]
            {
                PlanetMaterialLayer.Base("Tierra", PlanetLayerMaterial.BasicTerrain, Color.gray)
                    .WithSurfaceAppearance(Color.red, 75, 90, 0, 200)
            };

            Assert.AreEqual(PlanetChunkMeshCache.BuildPlanetId(in first), PlanetChunkMeshCache.BuildPlanetId(in second));
            Assert.AreNotEqual(PlanetChunkMeshCache.BuildPlanetId(in second), PlanetChunkMeshCache.BuildPlanetId(in third));
            Assert.AreNotEqual(PlanetChunkMeshCache.BuildPlanetId(in third), PlanetChunkMeshCache.BuildPlanetId(in fourth));
            Assert.AreNotEqual(PlanetChunkMeshCache.BuildPlanetId(in fourth), PlanetChunkMeshCache.BuildPlanetId(in fifth));
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
