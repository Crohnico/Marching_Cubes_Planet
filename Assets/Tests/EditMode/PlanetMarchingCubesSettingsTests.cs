using MarchingCubesPlanet.Coordinates;
using MarchingCubesPlanet.MarchingCubes;
using NUnit.Framework;

namespace MarchingCubesPlanet.Lab.Tests
{
    public sealed class PlanetMarchingCubesSettingsTests
    {
        [Test]
        public void DefaultSurfaceRangeMatchesPlanetSurface()
        {
            PlanetMarchingCubesSurfaceRange range = PlanetMarchingCubesSurfaceRange.Default();

            Assert.AreEqual(-512, range.radialStartOffset);
            Assert.AreEqual(1024, range.radialCubeCount);
            Assert.AreEqual(16, range.faceResolution);
            Assert.AreEqual(1f, range.cubeSizeGrid);
            Assert.AreEqual(1572864L, range.CubeCount);
        }

        [Test]
        public void SurfaceRangeRejectsNonUnitCubeSize()
        {
            PlanetMarchingCubesSurfaceRange range = PlanetMarchingCubesSurfaceRange.Default();
            range.cubeSizeGrid = 2f;

            Assert.IsFalse(range.Validate(out string message));
            StringAssert.Contains("cubeSizeGrid", message);
        }

        [Test]
        public void SettingsValidateTriangleBudget()
        {
            PlanetMarchingCubesSettings settings = PlanetMarchingCubesSettings.Default();
            settings.maxPlanetSurfaceTriangles = 0;

            Assert.IsFalse(settings.Validate(out string message));
            StringAssert.Contains("maxPlanetSurfaceTriangles", message);
        }

        [Test]
        public void SettingsEnsureDefaultsRecoversNewSerializedFields()
        {
            PlanetMarchingCubesSettings settings = default;

            settings.EnsureDefaults();

            Assert.IsTrue(settings.Validate(out string message), message);
            Assert.AreEqual(PlanetMarchingCubesSurfaceRange.Default().CubeCount, settings.surfaceRange.CubeCount);
            Assert.AreEqual(1000000, settings.maxPlanetSurfaceTriangles);
        }

        [Test]
        public void SurfaceRangeExpandsToCoverRecipeDisplacement()
        {
            PlanetRecipe recipe = PlanetRecipe.Default();
            recipe.GridRadius = 2000;
            PlanetMarchingCubesSettings settings = PlanetMarchingCubesSettings.Default();

            settings.EnsureSurfaceRangeCoversRecipe(in recipe);

            Assert.LessOrEqual(settings.surfaceRange.radialStartOffset, -1000);
            Assert.GreaterOrEqual(settings.surfaceRange.RadialEndOffset, 1061);
            Assert.AreEqual(1f, settings.surfaceRange.cubeSizeGrid);
        }

        [Test]
        public void GpuStructStridesMatchHlslContract()
        {
            Assert.AreEqual(32, PlanetMarchingCubesVertex.Stride);
            Assert.AreEqual(32, PlanetMarchingCubesState.Stride);
        }
    }
}
