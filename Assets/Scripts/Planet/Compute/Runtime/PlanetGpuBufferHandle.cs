using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarchingCubesPlanet.Compute
{
    public sealed class PlanetGpuBufferHandle
    {
        private GraphicsBuffer graphicsBuffer;
        private ComputeBuffer computeBuffer;

        public PlanetGpuBufferMode BufferMode { get; private set; }
        public int ElementCount { get; private set; }
        public int Stride { get; private set; }
        public long EstimatedBytes { get; private set; }
        public string DebugName { get; private set; }
        public bool IsAlive { get; private set; }

        public GraphicsBuffer GraphicsBuffer => graphicsBuffer;
        public ComputeBuffer ComputeBuffer => computeBuffer;

        public static PlanetGpuBufferHandle CreateGraphicsBuffer(string debugName, int elementCount, int stride)
        {
            ValidateCreateArguments(elementCount, stride);

            GraphicsBuffer buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, elementCount, stride)
            {
                name = debugName
            };

            return new PlanetGpuBufferHandle
            {
                BufferMode = PlanetGpuBufferMode.GraphicsBuffer,
                ElementCount = elementCount,
                Stride = stride,
                EstimatedBytes = PlanetComputeMemory.EstimateBufferBytes(elementCount, stride),
                DebugName = debugName,
                graphicsBuffer = buffer,
                IsAlive = true
            };
        }

        public static PlanetGpuBufferHandle CreateComputeBuffer(string debugName, int elementCount, int stride)
        {
            ValidateCreateArguments(elementCount, stride);

            ComputeBuffer buffer = new ComputeBuffer(elementCount, stride, ComputeBufferType.Structured)
            {
                name = debugName
            };

            return new PlanetGpuBufferHandle
            {
                BufferMode = PlanetGpuBufferMode.ComputeBuffer,
                ElementCount = elementCount,
                Stride = stride,
                EstimatedBytes = PlanetComputeMemory.EstimateBufferBytes(elementCount, stride),
                DebugName = debugName,
                computeBuffer = buffer,
                IsAlive = true
            };
        }

        public void BindTo(ComputeShader shader, int kernelIndex, int propertyId)
        {
            if (!IsAlive)
            {
                throw new InvalidOperationException("Cannot bind a released GPU buffer.");
            }

            if (BufferMode == PlanetGpuBufferMode.GraphicsBuffer)
            {
                shader.SetBuffer(kernelIndex, propertyId, graphicsBuffer);
                return;
            }

            shader.SetBuffer(kernelIndex, propertyId, computeBuffer);
        }

        public void Release()
        {
            if (!IsAlive)
            {
                return;
            }

            if (graphicsBuffer != null)
            {
                graphicsBuffer.Release();
                graphicsBuffer = null;
            }

            if (computeBuffer != null)
            {
                computeBuffer.Release();
                computeBuffer = null;
            }

            IsAlive = false;
        }

        private static void ValidateCreateArguments(int elementCount, int stride)
        {
            if (elementCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elementCount), "elementCount must be greater than zero.");
            }

            if (stride <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stride), "stride must be greater than zero.");
            }
        }

    }
}
