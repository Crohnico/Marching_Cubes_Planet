using MarchingCubesPlanet.MarchingCubes;
using NUnit.Framework;

namespace MarchingCubesPlanet.Lab.Tests
{
    public sealed class PlanetMarchingCubesPaintSettingsTests
    {
        [Test]
        public void DefaultPaintSettingsMatchPlanetSurfaceBudget()
        {
            PlanetMarchingCubesPaintSettings settings = PlanetMarchingCubesPaintSettings.Default();

            Assert.AreEqual(PlanetMarchingCubesPaintColorMode.PlanetSurfaceAtlas, settings.colorMode);
            Assert.AreEqual(1000000, settings.maxPaintedTriangles);
            Assert.AreEqual(3000000, settings.MaxPaintedVertices);
            Assert.IsTrue(settings.Validate(out string message), message);
        }

        [Test]
        public void PaintSettingsRejectInvalidTriangleBudget()
        {
            PlanetMarchingCubesPaintSettings settings = PlanetMarchingCubesPaintSettings.Default();
            settings.maxPaintedTriangles = 0;

            Assert.IsFalse(settings.Validate(out string message));
            StringAssert.Contains("maxPaintedTriangles", message);
        }

        [Test]
        public void PaintSettingsRejectInvalidColorMode()
        {
            PlanetMarchingCubesPaintSettings settings = PlanetMarchingCubesPaintSettings.Default();
            settings.colorMode = (PlanetMarchingCubesPaintColorMode)99;

            Assert.IsFalse(settings.Validate(out string message));
            StringAssert.Contains("colorMode", message);
        }

        [Test]
        public void PaintResultReportsTruncatedVisual()
        {
            PlanetMarchingCubesPaintResult result = new PlanetMarchingCubesPaintResult(
                100,
                25,
                75,
                true,
                1024,
                PlanetMarchingCubesPaintColorMode.HeightColor);

            Assert.IsTrue(result.HasVisibleMesh);
            Assert.IsTrue(result.VisualTruncated);
            Assert.AreEqual(100, result.SourceTriangleCount);
            Assert.AreEqual(25, result.PaintedTriangleCount);
            Assert.AreEqual(75, result.PaintedVertexCount);
            Assert.AreEqual(1024, result.MeshEstimatedBytes);
            Assert.AreEqual(PlanetMarchingCubesPaintColorMode.HeightColor, result.ColorMode);
        }

        [Test]
        public void MeshByteEstimateIncludesVerticesAndIndices()
        {
            long estimate = PlanetMarchingCubesPaintResult.CalculateMeshEstimatedBytes(300, 100);

            Assert.AreEqual(300 * 36L + 300 * 2L, estimate);
        }

        [Test]
        public void MeshByteEstimateUsesUInt32IndicesAboveUShortLimit()
        {
            long estimate = PlanetMarchingCubesPaintResult.CalculateMeshEstimatedBytes(90000, 30000);

            Assert.AreEqual(90000 * 36L + 90000 * 4L, estimate);
        }
    }
}
