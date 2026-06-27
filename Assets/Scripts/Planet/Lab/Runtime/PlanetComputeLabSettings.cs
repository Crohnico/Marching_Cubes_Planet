using System;
using MarchingCubesPlanet.Compute;
using UnityEngine;

namespace MarchingCubesPlanet.Lab
{
    [Serializable]
    public struct PlanetComputeLabSettings
    {
        [Tooltip("Number of float4 entries used by the debug buffer.")]
        [Min(1)]
        public int bufferElementCount;

        [Tooltip("Number of times Dispatch is requested by repeated tests.")]
        [Min(1)]
        public int dispatchRepeatCount;

        [Tooltip("Width of the writable debug RenderTexture.")]
        [Min(1)]
        public int outputTextureWidth;

        [Tooltip("Height of the writable debug RenderTexture.")]
        [Min(1)]
        public int outputTextureHeight;

        [Tooltip("Maximum allowed buffer element count for this Lab module.")]
        [Min(1)]
        public int maxBufferElementCount;

        [Tooltip("Maximum allowed output texture width or height for this Lab module.")]
        [Min(1)]
        public int maxTextureSize;

        [Tooltip("Maximum repeated Dispatch calls allowed by this Lab module.")]
        [Min(1)]
        public int maxDispatchRepeatCount;

        [Tooltip("Maximum stress cycles allowed by this Lab module.")]
        [Min(1)]
        public int maxCycleCount;

        public bool releaseBetweenCycles;
        public bool captureMetricsEachCycle;

        public const int Float4Stride = PlanetComputeMemory.Float4Stride;
        public const int Argb32BytesPerPixel = 4;

        public static PlanetComputeLabSettings Default()
        {
            return new PlanetComputeLabSettings
            {
                bufferElementCount = 16 * 1024,
                dispatchRepeatCount = 1,
                outputTextureWidth = 256,
                outputTextureHeight = 256,
                maxBufferElementCount = 2_000_000,
                maxTextureSize = 2048,
                maxDispatchRepeatCount = 100,
                maxCycleCount = 100,
                releaseBetweenCycles = true,
                captureMetricsEachCycle = false
            };
        }

        public static PlanetComputeLabSettings StressPreset(
            int bufferElementCount,
            int outputTextureWidth,
            int outputTextureHeight,
            int dispatchRepeatCount,
            int maxBufferElementCount,
            int maxTextureSize,
            int maxDispatchRepeatCount,
            int maxCycleCount)
        {
            return new PlanetComputeLabSettings
            {
                bufferElementCount = bufferElementCount,
                dispatchRepeatCount = dispatchRepeatCount,
                outputTextureWidth = outputTextureWidth,
                outputTextureHeight = outputTextureHeight,
                maxBufferElementCount = maxBufferElementCount,
                maxTextureSize = maxTextureSize,
                maxDispatchRepeatCount = maxDispatchRepeatCount,
                maxCycleCount = maxCycleCount,
                releaseBetweenCycles = true,
                captureMetricsEachCycle = false
            };
        }

        public long EstimatedBufferBytes => PlanetComputeMemory.EstimateBufferBytes(bufferElementCount, Float4Stride);
        public long EstimatedTextureBytes => EstimateRenderTextureBytes(outputTextureWidth, outputTextureHeight, Argb32BytesPerPixel);

        public bool IsValid(out string message)
        {
            if (bufferElementCount <= 0)
            {
                message = "bufferElementCount must be greater than zero.";
                return false;
            }

            if (dispatchRepeatCount <= 0)
            {
                message = "dispatchRepeatCount must be greater than zero.";
                return false;
            }

            if (outputTextureWidth <= 0 || outputTextureHeight <= 0)
            {
                message = "outputTextureWidth and outputTextureHeight must be greater than zero.";
                return false;
            }

            if (bufferElementCount > maxBufferElementCount)
            {
                message = "bufferElementCount exceeds maxBufferElementCount.";
                return false;
            }

            if (outputTextureWidth > maxTextureSize || outputTextureHeight > maxTextureSize)
            {
                message = "output texture size exceeds maxTextureSize.";
                return false;
            }

            if (dispatchRepeatCount > maxDispatchRepeatCount)
            {
                message = "dispatchRepeatCount exceeds maxDispatchRepeatCount.";
                return false;
            }

            message = "Compute Lab settings are valid.";
            return true;
        }

        public static long EstimateBufferBytes(int elementCount, int stride)
        {
            if (elementCount <= 0 || stride <= 0)
            {
                return 0;
            }

            return PlanetComputeMemory.EstimateBufferBytes(elementCount, stride);
        }

        public static long EstimateRenderTextureBytes(int width, int height, int bytesPerPixel)
        {
            if (width <= 0 || height <= 0 || bytesPerPixel <= 0)
            {
                return 0;
            }

            return (long)width * height * bytesPerPixel;
        }
    }
}
