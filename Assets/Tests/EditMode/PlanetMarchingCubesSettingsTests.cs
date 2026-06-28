using MarchingCubesPlanet.MarchingCubes;
using NUnit.Framework;

namespace MarchingCubesPlanet.Lab.Tests
{
    public sealed class PlanetMarchingCubesSettingsTests
    {
        [Test]
        public void DefaultRangeMatchesValidationPatch()
        {
            PlanetMarchingCubesRange range = PlanetMarchingCubesRange.Default();

            Assert.AreEqual(-512, range.radialStartOffset);
            Assert.AreEqual(1024, range.radialCubeCount);
            Assert.AreEqual(8, range.tangentHalfExtent);
            Assert.AreEqual(16, range.tangentCubeCount);
            Assert.AreEqual(1f, range.cubeSizeGrid);
            Assert.AreEqual(262144L, range.CubeCount);
        }

        [Test]
        public void RangeRejectsNonUnitCubeSize()
        {
            PlanetMarchingCubesRange range = PlanetMarchingCubesRange.Default();
            range.cubeSizeGrid = 2f;

            Assert.IsFalse(range.Validate(out string message));
            StringAssert.Contains("cubeSizeGrid", message);
        }

        [Test]
        public void SettingsValidateTriangleBudget()
        {
            PlanetMarchingCubesSettings settings = PlanetMarchingCubesSettings.Default();
            settings.maxValidationTriangles = 0;

            Assert.IsFalse(settings.Validate(out string message));
            StringAssert.Contains("maxValidationTriangles", message);
        }

        [Test]
        public void GpuStructStridesMatchHlslContract()
        {
            Assert.AreEqual(32, PlanetMarchingCubesVertex.Stride);
            Assert.AreEqual(32, PlanetMarchingCubesState.Stride);
        }
    }
}
