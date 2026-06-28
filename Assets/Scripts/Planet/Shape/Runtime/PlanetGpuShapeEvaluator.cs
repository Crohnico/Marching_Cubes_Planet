using System;
using MarchingCubesPlanet.Compute;
using MarchingCubesPlanet.Coordinates;
using UnityEngine;

namespace MarchingCubesPlanet.Shape
{
    public sealed class PlanetGpuShapeEvaluator
    {
        private static readonly int ShapeParametersId = Shader.PropertyToID("_PlanetShapeParameters");
        private static readonly int ShapeCellsId = Shader.PropertyToID("_PlanetShapeCells");
        private static readonly int ShapeSamplePositionsId = Shader.PropertyToID("_ShapeSamplePositions");
        private static readonly int DensitySamplesId = Shader.PropertyToID("_DensitySamples");
        private static readonly int ShapeSampleCountId = Shader.PropertyToID("_ShapeSampleCount");

        private const string EvaluateKernelName = "CS_EvaluateDensitySamples";

        private readonly PlanetGpuShapeParameters[] parameterUpload = new PlanetGpuShapeParameters[1];

        private ComputeShader computeShader;
        private int evaluateKernel;
        private uint threadGroupSizeX;
        private PlanetGpuBufferMode bufferMode;

        private PlanetGpuBufferHandle parameterBuffer;
        private PlanetGpuBufferHandle cellBuffer;
        private PlanetGpuBufferHandle sampleInputBuffer;
        private PlanetGpuBufferHandle sampleOutputBuffer;

        public bool IsInitialized => parameterBuffer != null && parameterBuffer.IsAlive &&
                                     cellBuffer != null && cellBuffer.IsAlive;
        public uint ThreadGroupSizeX => threadGroupSizeX;
        public PlanetGpuBufferHandle ParameterBuffer => parameterBuffer;
        public PlanetGpuBufferHandle CellBuffer => cellBuffer;
        public PlanetGpuBufferHandle SampleInputBuffer => sampleInputBuffer;
        public PlanetGpuBufferHandle SampleOutputBuffer => sampleOutputBuffer;

        public void Initialize(
            ComputeShader shader,
            in PlanetRecipe recipe,
            PlanetGpuShapeCell[] cells,
            PlanetGpuBufferMode requestedBufferMode)
        {
            if (shader == null)
            {
                throw new ArgumentNullException(nameof(shader));
            }

            if (!PlanetRecipeValidator.Validate(in recipe, out string recipeMessage))
            {
                throw new ArgumentException(recipeMessage, nameof(recipe));
            }

            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            if (cells.Length < recipe.VoronoiDivision)
            {
                throw new ArgumentException("cells length must be greater than or equal to VoronoiDivision.", nameof(cells));
            }

            Release();

            computeShader = shader;
            evaluateKernel = computeShader.FindKernel(EvaluateKernelName);
            computeShader.GetKernelThreadGroupSizes(evaluateKernel, out threadGroupSizeX, out _, out _);
            bufferMode = requestedBufferMode;

            parameterUpload[0] = PlanetGpuShapeParameters.FromRecipe(in recipe);
            parameterBuffer = CreateBuffer("Planet Shape Parameters", 1, PlanetGpuShapeParameters.Stride);
            cellBuffer = CreateBuffer("Planet Shape Voronoi Cells", recipe.VoronoiDivision, PlanetGpuShapeCell.Stride);

            SetData(parameterBuffer, parameterUpload, 1);
            SetData(cellBuffer, cells, recipe.VoronoiDivision);
        }

        public void EvaluateDensitySamples(Vector4[] samplePositions, Vector4[] sampleResults)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("PlanetGpuShapeEvaluator must be initialized before evaluating samples.");
            }

            if (samplePositions == null)
            {
                throw new ArgumentNullException(nameof(samplePositions));
            }

            if (sampleResults == null)
            {
                throw new ArgumentNullException(nameof(sampleResults));
            }

            if (samplePositions.Length == 0)
            {
                throw new ArgumentException("At least one sample is required.", nameof(samplePositions));
            }

            if (sampleResults.Length < samplePositions.Length)
            {
                throw new ArgumentException("sampleResults must be at least as large as samplePositions.", nameof(sampleResults));
            }

            EnsureSampleBuffers(samplePositions.Length);
            SetData(sampleInputBuffer, samplePositions, samplePositions.Length);

            parameterBuffer.BindTo(computeShader, evaluateKernel, ShapeParametersId);
            cellBuffer.BindTo(computeShader, evaluateKernel, ShapeCellsId);
            sampleInputBuffer.BindTo(computeShader, evaluateKernel, ShapeSamplePositionsId);
            sampleOutputBuffer.BindTo(computeShader, evaluateKernel, DensitySamplesId);
            computeShader.SetInt(ShapeSampleCountId, samplePositions.Length);

            uint safeThreadGroupSizeX = threadGroupSizeX == 0u ? 1u : threadGroupSizeX;
            int groupCount = Mathf.CeilToInt(samplePositions.Length / (float)safeThreadGroupSizeX);
            computeShader.Dispatch(evaluateKernel, groupCount, 1, 1);

            GetData(sampleOutputBuffer, sampleResults, samplePositions.Length);
        }

        public void Release()
        {
            ReleaseBuffer(ref sampleOutputBuffer);
            ReleaseBuffer(ref sampleInputBuffer);
            ReleaseBuffer(ref cellBuffer);
            ReleaseBuffer(ref parameterBuffer);
            computeShader = null;
            evaluateKernel = 0;
            threadGroupSizeX = 0;
        }

        private void EnsureSampleBuffers(int sampleCount)
        {
            if (sampleInputBuffer != null && sampleInputBuffer.IsAlive && sampleInputBuffer.ElementCount == sampleCount &&
                sampleOutputBuffer != null && sampleOutputBuffer.IsAlive && sampleOutputBuffer.ElementCount == sampleCount)
            {
                return;
            }

            ReleaseBuffer(ref sampleInputBuffer);
            ReleaseBuffer(ref sampleOutputBuffer);
            sampleInputBuffer = CreateBuffer("Planet Shape Sample Positions", sampleCount, 16);
            sampleOutputBuffer = CreateBuffer("Planet Shape Density Samples", sampleCount, 16);
        }

        private PlanetGpuBufferHandle CreateBuffer(string resourceName, int elementCount, int stride)
        {
            return bufferMode == PlanetGpuBufferMode.GraphicsBuffer
                ? PlanetGpuBufferHandle.CreateGraphicsBuffer(resourceName, elementCount, stride)
                : PlanetGpuBufferHandle.CreateComputeBuffer(resourceName, elementCount, stride);
        }

        private static void SetData<T>(PlanetGpuBufferHandle handle, T[] data, int count) where T : struct
        {
            if (handle.BufferMode == PlanetGpuBufferMode.GraphicsBuffer)
            {
                handle.GraphicsBuffer.SetData(data, 0, 0, count);
                return;
            }

            handle.ComputeBuffer.SetData(data, 0, 0, count);
        }

        private static void GetData<T>(PlanetGpuBufferHandle handle, T[] data, int count) where T : struct
        {
            if (handle.BufferMode == PlanetGpuBufferMode.GraphicsBuffer)
            {
                handle.GraphicsBuffer.GetData(data, 0, 0, count);
                return;
            }

            handle.ComputeBuffer.GetData(data, 0, 0, count);
        }

        private static void ReleaseBuffer(ref PlanetGpuBufferHandle handle)
        {
            if (handle != null)
            {
                handle.Release();
                handle = null;
            }
        }
    }
}
