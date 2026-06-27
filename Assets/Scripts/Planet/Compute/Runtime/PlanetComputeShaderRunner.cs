using System;
using UnityEngine;

namespace MarchingCubesPlanet.Compute
{
    // Step 02 debug adapter: this is not the final generic compute-dispatch API.
    // DispatchDebugWrite is intentionally tied to PlanetComputeDebug.compute.
    public sealed class PlanetComputeShaderRunner
    {
        private static readonly int DebugSamplesId = Shader.PropertyToID("_DebugSamples");
        private static readonly int DebugTextureId = Shader.PropertyToID("_DebugTexture");
        private static readonly int BufferCountId = Shader.PropertyToID("_BufferCount");
        private static readonly int TextureWidthId = Shader.PropertyToID("_TextureWidth");
        private static readonly int TextureHeightId = Shader.PropertyToID("_TextureHeight");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int DispatchIndexId = Shader.PropertyToID("_DispatchIndex");
        private static readonly int DispatchBaseIndexId = Shader.PropertyToID("_DispatchBaseIndex");

        public const int MaxThreadGroupsPerDispatchAxis = 65535;

        private ComputeShader computeShader;
        private int kernelIndex;
        private uint threadGroupSizeX;

        public uint ThreadGroupSizeX => threadGroupSizeX;
        public bool IsConfigured => computeShader != null && threadGroupSizeX > 0;

        public void Configure(ComputeShader shader, string kernelName)
        {
            if (shader == null)
            {
                throw new ArgumentNullException(nameof(shader));
            }

            computeShader = shader;
            kernelIndex = computeShader.FindKernel(kernelName);
            computeShader.GetKernelThreadGroupSizes(kernelIndex, out threadGroupSizeX, out _, out _);

            if (threadGroupSizeX == 0)
            {
                throw new InvalidOperationException("Compute shader threadGroupSizeX must be greater than zero.");
            }
        }

        public int CalculateThreadGroups(int workItemCount)
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException("PlanetComputeShaderRunner is not configured.");
            }

            if (workItemCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(workItemCount), "workItemCount must be greater than zero.");
            }

            return Mathf.CeilToInt(workItemCount / (float)threadGroupSizeX);
        }

        public int CalculateDispatchBatchCount(int workItemCount)
        {
            int threadGroups = CalculateThreadGroups(workItemCount);
            return Mathf.CeilToInt(threadGroups / (float)MaxThreadGroupsPerDispatchAxis);
        }

        public void DispatchDebugWrite(
            PlanetGpuBufferHandle buffer,
            RenderTexture outputTexture,
            int bufferCount,
            int textureWidth,
            int textureHeight,
            uint seed,
            uint dispatchIndex)
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException("PlanetComputeShaderRunner is not configured.");
            }

            if (buffer == null || !buffer.IsAlive)
            {
                throw new InvalidOperationException("A live GPU buffer is required before Dispatch.");
            }

            if (outputTexture == null || !outputTexture.IsCreated())
            {
                throw new InvalidOperationException("A created RenderTexture is required before Dispatch.");
            }

            int texturePixels = textureWidth * textureHeight;
            int workItemCount = Mathf.Max(bufferCount, texturePixels);

            buffer.BindTo(computeShader, kernelIndex, DebugSamplesId);
            computeShader.SetTexture(kernelIndex, DebugTextureId, outputTexture);
            computeShader.SetInt(BufferCountId, bufferCount);
            computeShader.SetInt(TextureWidthId, textureWidth);
            computeShader.SetInt(TextureHeightId, textureHeight);
            computeShader.SetInt(SeedId, unchecked((int)seed));
            computeShader.SetInt(DispatchIndexId, unchecked((int)dispatchIndex));

            int baseIndex = 0;
            int remainingWorkItems = workItemCount;
            int maxWorkItemsPerDispatch = MaxThreadGroupsPerDispatchAxis * (int)threadGroupSizeX;

            while (remainingWorkItems > 0)
            {
                int batchWorkItems = Mathf.Min(remainingWorkItems, maxWorkItemsPerDispatch);
                int threadGroups = CalculateThreadGroups(batchWorkItems);

                computeShader.SetInt(DispatchBaseIndexId, baseIndex);
                computeShader.Dispatch(kernelIndex, threadGroups, 1, 1);

                int dispatchedWorkItems = threadGroups * (int)threadGroupSizeX;
                baseIndex += dispatchedWorkItems;
                remainingWorkItems -= dispatchedWorkItems;
            }
        }
    }
}
