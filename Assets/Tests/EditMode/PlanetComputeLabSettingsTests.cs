using MarchingCubesPlanet.Compute;
using MarchingCubesPlanet.Lab;
using NUnit.Framework;
using UnityEngine;

namespace MarchingCubesPlanet.Lab.Tests
{
    public sealed class PlanetComputeLabSettingsTests
    {
        [Test]
        public void BufferBytesUseElementCountTimesStride()
        {
            Assert.AreEqual(16L * 1024L * 16L, PlanetComputeLabSettings.EstimateBufferBytes(16 * 1024, 16));
        }

        [Test]
        public void RenderTextureBytesUseWidthHeightAndBytesPerPixel()
        {
            Assert.AreEqual(256L * 256L * 4L, PlanetComputeLabSettings.EstimateRenderTextureBytes(256, 256, 4));
        }

        [Test]
        public void DefaultSettingsAreValid()
        {
            PlanetComputeLabSettings settings = PlanetComputeLabSettings.Default();

            Assert.IsTrue(settings.IsValid(out string message), message);
        }

        [Test]
        public void SettingsRejectElementCountAboveLimit()
        {
            PlanetComputeLabSettings settings = PlanetComputeLabSettings.Default();
            settings.bufferElementCount = settings.maxBufferElementCount + 1;

            Assert.IsFalse(settings.IsValid(out _));
        }

        [Test]
        public void InvalidByteInputsReturnZero()
        {
            Assert.AreEqual(0, PlanetComputeLabSettings.EstimateBufferBytes(0, 16));
            Assert.AreEqual(0, PlanetComputeLabSettings.EstimateRenderTextureBytes(256, 0, 4));
        }

        [Test]
        public void ExtremeTextureRequiresMoreThanOneDispatchBatchWithSixtyFourWideKernel()
        {
            const int extremeTexturePixels = 2048 * 2048;
            const int threadGroupSizeX = 64;

            int threadGroups = Mathf.CeilToInt(extremeTexturePixels / (float)threadGroupSizeX);
            int batches = Mathf.CeilToInt(threadGroups / (float)PlanetComputeShaderRunner.MaxThreadGroupsPerDispatchAxis);

            Assert.AreEqual(65536, threadGroups);
            Assert.AreEqual(2, batches);
        }
    }
}
