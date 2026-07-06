using System.Reflection;
using MarchingCubesPlanet.Coordinates;
using MarchingCubesPlanet.MarchingCubes;
using NUnit.Framework;
using UnityEngine;

namespace MarchingCubesPlanet.Tests
{
    public sealed class PlanetMarchingCubesPaintSettingsTests
    {
        [Test]
        public void DefaultPaintSettingsMatchPlanetSurfaceVertexCapacity()
        {
            PlanetMarchingCubesPaintSettings settings = PlanetMarchingCubesPaintSettings.Default();

            Assert.AreEqual(PlanetMarchingCubesPaintColorMode.PlanetSurfaceAtlas, settings.colorMode);
            Assert.AreEqual(3000000, settings.meshVertexCapacity);
            Assert.IsTrue(settings.Validate(out string message), message);
        }

        [Test]
        public void PaintSettingsRejectInvalidVertexCapacity()
        {
            PlanetMarchingCubesPaintSettings settings = PlanetMarchingCubesPaintSettings.Default();
            settings.meshVertexCapacity = 0;

            Assert.IsFalse(settings.Validate(out string message));
            StringAssert.Contains("meshVertexCapacity", message);
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
                PlanetMarchingCubesPaintColorMode.HeightColor,
                7);

            Assert.IsTrue(result.HasVisibleMesh);
            Assert.AreEqual(100, result.SourceTriangleCount);
            Assert.AreEqual(100, result.PaintedTriangleCount);
            Assert.AreEqual(300, result.PaintedVertexCount);
            Assert.AreEqual(1024, result.MeshEstimatedBytes);
            Assert.AreEqual(12, result.WaterTriangleCount);
            Assert.AreEqual(36, result.WaterVertexCount);
            Assert.AreEqual(256, result.WaterMeshEstimatedBytes);
            Assert.AreEqual(7, result.ChunkCount);
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

        [Test]
        public void SurfaceAtlasUvUsesSeaLevelAtMiddleOfGradient()
        {
            PlanetRecipe recipe = PlanetRecipe.Default();

            Vector2 seaLevelUv = EvaluateSurfaceAtlasUv(recipe.GridRadius, in recipe);

            Assert.AreEqual(0.5f, seaLevelUv.x);
            Assert.AreEqual(0.5f, seaLevelUv.y);
        }

        [Test]
        public void SurfaceAtlasUvCompressesLandRangeForReadableHighColors()
        {
            PlanetRecipe recipe = PlanetRecipe.Default();
            float theoreticalLandOffset =
                recipe.GridRadius * recipe.MaxLandElevation * recipe.MaxHeightModifier +
                recipe.GridRadius * recipe.MountainBiomeHeight +
                recipe.GridRadius * recipe.SurfaceNoiseAmplitude;
            float compressedLandOffset = theoreticalLandOffset * 0.6f;

            Vector2 highLandUv = EvaluateSurfaceAtlasUv(recipe.GridRadius + compressedLandOffset, in recipe);
            Vector2 halfLandUv = EvaluateSurfaceAtlasUv(recipe.GridRadius + compressedLandOffset * 0.5f, in recipe);

            Assert.AreEqual(1f, highLandUv.y);
            Assert.AreEqual(0.75f, halfLandUv.y);
        }

        private static Vector2 EvaluateSurfaceAtlasUv(float radius, in PlanetRecipe recipe)
        {
            MethodInfo method = typeof(PlanetMarchingCubesMeshPainter).GetMethod(
                "EvaluateSurfaceAtlasUv",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(method);
            object[] arguments = { radius, recipe };
            return (Vector2)method.Invoke(null, arguments);
        }
    }
}
