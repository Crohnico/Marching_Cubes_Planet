using MarchingCubesPlanet.Coordinates;
using MarchingCubesPlanet.MarchingCubes;
using NUnit.Framework;
using UnityEngine;

namespace MarchingCubesPlanet.Lab.Tests
{
    public sealed class PlanetChunkLodTests
    {
        [Test]
        public void LodRecipesKeepWorldRadiusFromLod1Base()
        {
            PlanetRecipe lod1 = PlanetRecipe.Default();
            lod1.GridRadius = 130;
            lod1.WorldScale = 61.5f;

            PlanetRecipe lod0 = PlanetChunkLodUtility.BuildRecipeForLod(in lod1, PlanetChunkLod.LOD0);
            PlanetRecipe lod2 = PlanetChunkLodUtility.BuildRecipeForLod(in lod1, PlanetChunkLod.LOD2);

            Assert.AreEqual(260, lod0.GridRadius);
            Assert.AreEqual(30.75f, lod0.WorldScale);
            Assert.AreEqual(130, lod1.GridRadius);
            Assert.AreEqual(61.5f, lod1.WorldScale);
            Assert.AreEqual(65, lod2.GridRadius);
            Assert.AreEqual(123f, lod2.WorldScale);
            Assert.AreEqual(lod1.WorldRadius, lod0.WorldRadius);
            Assert.AreEqual(lod1.WorldRadius, lod2.WorldRadius);
        }

        [Test]
        public void DistanceThresholdsAssignDesiredLod()
        {
            Assert.AreEqual(PlanetChunkLod.LOD0, PlanetChunkLodScorer.ResolveDesiredLod(3f));
            Assert.AreEqual(PlanetChunkLod.LOD1, PlanetChunkLodScorer.ResolveDesiredLod(6f));
            Assert.AreEqual(PlanetChunkLod.LOD2, PlanetChunkLodScorer.ResolveDesiredLod(6.1f));
        }

        [Test]
        public void ScoringUsesProximityAndCameraForward()
        {
            PlanetChunkLodScoringContext context = new PlanetChunkLodScoringContext(
                Vector3.zero,
                Vector3.forward,
                10f);

            PlanetChunkLodScore centered = PlanetChunkLodScorer.Evaluate(1, new Vector3(0f, 0f, 20f), in context);
            PlanetChunkLodScore sideways = PlanetChunkLodScorer.Evaluate(2, new Vector3(20f, 0f, 20f), in context);

            Assert.AreEqual(PlanetChunkLod.LOD0, centered.DesiredLod);
            Assert.Greater(centered.ViewScore, sideways.ViewScore);
            Assert.Greater(centered.Score, sideways.Score);
        }
    }
}
