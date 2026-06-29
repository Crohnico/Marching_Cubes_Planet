using MarchingCubesPlanet.MarchingCubes;
using NUnit.Framework;

namespace MarchingCubesPlanet.Lab.Tests
{
    public sealed class PlanetMarchingCubesPaintSettingsTests
    {
        [Test]
        public void DefaultPaintSettingsMatchPlanetSurfaceCapacity()
        {
            PlanetMarchingCubesPaintSettings settings = PlanetMarchingCubesPaintSettings.Default();

            Assert.AreEqual(PlanetMarchingCubesPaintColorMode.PlanetSurfaceAtlas, settings.colorMode);
            Assert.AreEqual(1000000, settings.meshTriangleCapacity);
            Assert.AreEqual(3000000, settings.MeshVertexCapacity);
            Assert.IsTrue(settings.Validate(out string message), message);
        }

        [Test]
        public void PaintSettingsRejectInvalidTriangleCapacity()
        {
            PlanetMarchingCubesPaintSettings settings = PlanetMarchingCubesPaintSettings.Default();
            settings.meshTriangleCapacity = 0;

            Assert.IsFalse(settings.Validate(out string message));
            StringAssert.Contains("meshTriangleCapacity", message);
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
        public void PaintResultReportsFullVisual()
        {
            PlanetMarchingCubesPaintResult result = new PlanetMarchingCubesPaintResult(
                100,
                100,
                300,
                1024,
                12,
                36,
                256,
                PlanetMarchingCubesPaintColorMode.HeightColor);

            Assert.IsTrue(result.HasVisibleMesh);
            Assert.AreEqual(100, result.SourceTriangleCount);
            Assert.AreEqual(100, result.PaintedTriangleCount);
            Assert.AreEqual(300, result.PaintedVertexCount);
            Assert.AreEqual(1024, result.MeshEstimatedBytes);
            Assert.AreEqual(12, result.WaterTriangleCount);
            Assert.AreEqual(36, result.WaterVertexCount);
            Assert.AreEqual(256, result.WaterMeshEstimatedBytes);
            Assert.AreEqual(1280, result.TotalMeshEstimatedBytes);
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
