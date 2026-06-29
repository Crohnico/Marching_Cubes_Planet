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
    }
}
