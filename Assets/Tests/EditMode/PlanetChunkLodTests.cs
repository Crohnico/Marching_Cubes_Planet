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
        public void DefaultChunkSizesUseCanonicalLod1Size()
        {
            Assert.AreEqual(8, PlanetChunkLodUtility.GetChunkSizeForLod(PlanetChunkLod.LOD2));
            Assert.AreEqual(16, PlanetChunkLodUtility.GetChunkSizeForLod(PlanetChunkLod.LOD1));
            Assert.AreEqual(32, PlanetChunkLodUtility.GetChunkSizeForLod(PlanetChunkLod.LOD0));
        }

        [Test]
        public void ActivationConfigOverridesCanonicalChunkSize()
        {
            PlanetChunkLodActivationConfig config = PlanetChunkLodActivationConfig.Default();
            config.canonicalChunkSize = 32;
            config.EnsureValid();

            Assert.AreEqual(16, PlanetChunkLodUtility.GetChunkSizeForLod(PlanetChunkLod.LOD2, in config));
            Assert.AreEqual(32, PlanetChunkLodUtility.GetChunkSizeForLod(PlanetChunkLod.LOD1, in config));
            Assert.AreEqual(64, PlanetChunkLodUtility.GetChunkSizeForLod(PlanetChunkLod.LOD0, in config));
        }

        [Test]
        public void DistanceThresholdsAssignDesiredLod()
        {
            PlanetChunkLodScoringContext context = new PlanetChunkLodScoringContext(
                Vector3.zero,
                Vector3.forward,
                10f,
                PlanetChunkLodActivationConfig.Default());

            Assert.AreEqual(PlanetChunkLod.LOD1, PlanetChunkLodScorer.ResolveDesiredLod(899f, in context));
            Assert.AreEqual(PlanetChunkLod.LOD1, PlanetChunkLodScorer.ResolveDesiredLod(1799f, in context));
            Assert.AreEqual(PlanetChunkLod.LOD2, PlanetChunkLodScorer.ResolveDesiredLod(1800f, in context));
        }

        [Test]
        public void ScoringUsesProximityAndCameraForward()
        {
            PlanetChunkLodScoringContext context = new PlanetChunkLodScoringContext(
                Vector3.zero,
                Vector3.forward,
                10f,
                PlanetChunkLodActivationConfig.Default());

            PlanetChunkLodScore centered = PlanetChunkLodScorer.Evaluate(1, new Vector3(0f, 0f, 20f), in context);
            PlanetChunkLodScore sideways = PlanetChunkLodScorer.Evaluate(2, new Vector3(20f, 0f, 20f), in context);

            Assert.AreEqual(PlanetChunkLod.LOD1, centered.DesiredLod);
            Assert.Greater(centered.ViewScore, sideways.ViewScore);
            Assert.Greater(centered.Score, sideways.Score);
        }

        [Test]
        public void FallbackLod2UsesImmediateShellPackage()
        {
            PlanetChunkWorkPackageSummary summary = PlanetChunkWorkPackagePlanner.BuildSummary(2, 17);

            Assert.AreEqual(PlanetChunkWorkPackageMode.ImmediateLod2Shell, summary.Mode);
            Assert.IsTrue(summary.IsImmediateShell);
            Assert.AreEqual(17, summary.PackageSize);
            Assert.AreEqual(1, summary.PackageCount);
        }

        [Test]
        public void RefinementLodsUseSmallPackages()
        {
            PlanetChunkWorkPackageSummary summary = PlanetChunkWorkPackagePlanner.BuildSummary(0, 17);

            Assert.AreEqual(PlanetChunkWorkPackageMode.RefinementPackages, summary.Mode);
            Assert.IsFalse(summary.IsImmediateShell);
            Assert.AreEqual(PlanetChunkWorkPackagePlanner.DefaultRefinementPackageSize, summary.PackageSize);
            Assert.AreEqual(3, summary.PackageCount);
        }
    }
}
