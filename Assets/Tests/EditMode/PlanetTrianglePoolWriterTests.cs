using MarchingCubesPlanet.TrianglePools;
using NUnit.Framework;

namespace MarchingCubesPlanet.Lab.Tests
{
    public sealed class PlanetTrianglePoolWriterTests
    {
        [Test]
        public void DrawHonorsBudgetAndPrefersNearestBuckets()
        {
            PlanetTriangleBudget budget = new PlanetTriangleBudget(
                PlanetTriangleArtistId.EnvironmentValue,
                "EnvironmentTest",
                3,
                4,
                64,
                8192);
            PlanetTrianglePoolController controller = new PlanetTrianglePoolController(budget);
            PlanetTrianglePoolWriter writer = new PlanetTrianglePoolWriter(controller);
            float[] scores = { 10f, 1f, 2f, 3f, 4f };

            PlanetTriangleDrawResult result = writer.Draw(
                scores.Length,
                index => scores[index],
                _ => 1,
                PlanetTriangleOwnerId.PlanetSurfaceValue);

            Assert.AreEqual(5, result.RequestedTriangleCount);
            Assert.AreEqual(3, result.GrantedTriangleCount);
            Assert.AreEqual(2, result.DeniedTriangleCount);
            Assert.IsFalse(writer.IsSourceTriangleSelected(0));
            Assert.IsTrue(writer.IsSourceTriangleSelected(1));
            Assert.IsTrue(writer.IsSourceTriangleSelected(2));
            Assert.IsTrue(writer.IsSourceTriangleSelected(3));
            Assert.IsFalse(writer.IsSourceTriangleSelected(4));
            Assert.AreEqual(3, controller.UsedTriangleSlots);
            Assert.AreEqual(0, controller.FreeTriangleSlots);
        }

        [Test]
        public void DrawCountsTriangleCostAgainstBudget()
        {
            PlanetTriangleBudget budget = new PlanetTriangleBudget(
                PlanetTriangleArtistId.EnvironmentValue,
                "EnvironmentTest",
                4,
                4,
                64,
                8192);
            PlanetTrianglePoolController controller = new PlanetTrianglePoolController(budget);
            PlanetTrianglePoolWriter writer = new PlanetTrianglePoolWriter(controller);
            float[] scores = { 1f, 2f, 3f };
            int[] costs = { 3, 1, 1 };

            PlanetTriangleDrawResult result = writer.Draw(
                scores.Length,
                index => scores[index],
                index => costs[index],
                PlanetTriangleOwnerId.PlanetSurfaceValue);

            Assert.AreEqual(5, result.RequestedTriangleCount);
            Assert.AreEqual(4, result.GrantedTriangleCount);
            Assert.AreEqual(1, result.DeniedTriangleCount);
            Assert.IsTrue(writer.IsSourceTriangleSelected(0));
            Assert.IsTrue(writer.IsSourceTriangleSelected(1));
            Assert.IsFalse(writer.IsSourceTriangleSelected(2));
            Assert.AreEqual(4, controller.UsedTriangleSlots);
        }

        [Test]
        public void DrawKeepsResidentSlotsAndConfiscatesWorseTriangles()
        {
            PlanetTriangleBudget budget = new PlanetTriangleBudget(
                PlanetTriangleArtistId.EnvironmentValue,
                "EnvironmentTest",
                3,
                4,
                64,
                8192);
            PlanetTrianglePoolController controller = new PlanetTrianglePoolController(budget);
            PlanetTrianglePoolWriter writer = new PlanetTrianglePoolWriter(controller);

            PlanetTriangleDrawResult first = writer.Draw(
                3,
                index => 50f + index,
                _ => 1,
                PlanetTriangleOwnerId.PlanetSurfaceValue,
                1u);

            Assert.AreEqual(3, first.GrantedTriangleCount);
            Assert.AreEqual(3, controller.UsedTriangleSlots);
            Assert.AreEqual(0, controller.FreeTriangleSlots);

            PlanetTriangleDrawResult worse = writer.Draw(
                2,
                index => 100f + index,
                _ => 1,
                PlanetTriangleOwnerId.PlanetSurfaceValue,
                2u);

            Assert.AreEqual(2, worse.RequestedTriangleCount);
            Assert.AreEqual(0, worse.GrantedTriangleCount);
            Assert.AreEqual(2, worse.DeniedTriangleCount);
            Assert.AreEqual(0, worse.ReclaimedTriangleCount);
            Assert.IsFalse(writer.IsSourceTriangleSelected(0));
            Assert.IsFalse(writer.IsSourceTriangleSelected(1));
            Assert.AreEqual(3, controller.UsedTriangleSlots);
            Assert.AreEqual(0, controller.FreeTriangleSlots);

            PlanetTriangleDrawResult better = writer.Draw(
                2,
                index => 10f + index,
                _ => 1,
                PlanetTriangleOwnerId.PlanetSurfaceValue,
                3u);

            Assert.AreEqual(2, better.RequestedTriangleCount);
            Assert.AreEqual(2, better.GrantedTriangleCount);
            Assert.AreEqual(0, better.DeniedTriangleCount);
            Assert.AreEqual(2, better.ReclaimedTriangleCount);
            Assert.IsTrue(writer.IsSourceTriangleSelected(0));
            Assert.IsTrue(writer.IsSourceTriangleSelected(1));
            Assert.AreEqual(2, controller.UsedTriangleSlots);
            Assert.AreEqual(1, controller.FreeTriangleSlots);
        }

        [Test]
        public void DrawReplacingSameMeshIdReleasesPreviousPublication()
        {
            PlanetTriangleBudget budget = new PlanetTriangleBudget(
                PlanetTriangleArtistId.EnvironmentValue,
                "EnvironmentTest",
                3,
                4,
                64,
                8192);
            PlanetTrianglePoolController controller = new PlanetTrianglePoolController(budget);
            PlanetTrianglePoolWriter writer = new PlanetTrianglePoolWriter(controller);

            writer.Draw(
                3,
                index => 10f + index,
                _ => 1,
                PlanetTriangleOwnerId.PlanetSurfaceValue,
                7u);

            PlanetTriangleDrawResult replacement = writer.Draw(
                2,
                index => 100f + index,
                _ => 1,
                PlanetTriangleOwnerId.PlanetSurfaceValue,
                7u);

            Assert.AreEqual(2, replacement.GrantedTriangleCount);
            Assert.AreEqual(0, replacement.ReclaimedTriangleCount);
            Assert.AreEqual(2, controller.UsedTriangleSlots);
            Assert.AreEqual(1, controller.FreeTriangleSlots);
        }
    }
}
