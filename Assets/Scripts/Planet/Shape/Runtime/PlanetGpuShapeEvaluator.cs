using System;
using MarchingCubesPlanet.Compute;
using MarchingCubesPlanet.Coordinates;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarchingCubesPlanet.Shape
{
    public sealed class PlanetGpuShapeEvaluator
    {
        private static readonly int ShapeParametersId = Shader.PropertyToID("_PlanetShapeParameters");
        private static readonly int ShapeCellsId = Shader.PropertyToID("_PlanetShapeCells");
        private static readonly int ShapeSamplePositionsId = Shader.PropertyToID("_ShapeSamplePositions");
        private static readonly int DensitySamplesId = Shader.PropertyToID("_DensitySamples");
        private static readonly int ShapeSampleCountId = Shader.PropertyToID("_ShapeSampleCount");
        private static readonly int CaveEvaluationEnabledId = Shader.PropertyToID("_PlanetCaveEvaluationEnabled");

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
        private AsyncGPUReadbackRequest pendingReadback;
        private int pendingReadbackSampleCount;
        private bool hasPendingReadback;

        public bool IsInitialized => parameterBuffer != null && parameterBuffer.IsAlive &&
                                     cellBuffer != null && cellBuffer.IsAlive;
        public uint ThreadGroupSizeX => threadGroupSizeX;
        public PlanetGpuBufferHandle ParameterBuffer => parameterBuffer;
        public PlanetGpuBufferHandle CellBuffer => cellBuffer;
        public PlanetGpuBufferHandle SampleInputBuffer => sampleInputBuffer;
        public PlanetGpuBufferHandle SampleOutputBuffer => sampleOutputBuffer;
        public bool HasPendingReadback => hasPendingReadback;

        public void Initialize(
            ComputeShader shader,
            in PlanetRecipe recipe,
            PlanetGpuShapeCell[] cells,
            PlanetGpuBufferMode requestedBufferMode)
        {
            Release();
            InitializeReusable(shader, in recipe, cells, requestedBufferMode);
        }

        public void InitializeReusable(
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

            computeShader = shader;
            evaluateKernel = computeShader.FindKernel(EvaluateKernelName);
            computeShader.GetKernelThreadGroupSizes(evaluateKernel, out threadGroupSizeX, out _, out _);
            computeShader.SetInt(CaveEvaluationEnabledId, 1);
            bufferMode = requestedBufferMode;

            parameterUpload[0] = PlanetGpuShapeParameters.FromRecipe(in recipe);
            parameterBuffer = EnsureBuffer(parameterBuffer, "Planet Shape Parameters", 1, PlanetGpuShapeParameters.Stride);
            cellBuffer = EnsureBuffer(cellBuffer, "Planet Shape Voronoi Cells", recipe.VoronoiDivision, PlanetGpuShapeCell.Stride);

            SetData(parameterBuffer, parameterUpload, 1);
            SetData(cellBuffer, cells, recipe.VoronoiDivision);
        }

        public void EvaluateDensitySamples(Vector4[] samplePositions, Vector4[] sampleResults)
        {
            if (sampleResults == null)
            {
                throw new ArgumentNullException(nameof(sampleResults));
            }

            int sampleCount = DispatchDensitySamples(samplePositions);
            if (sampleResults.Length < sampleCount)
            {
                throw new ArgumentException("sampleResults must be at least as large as samplePositions.", nameof(sampleResults));
            }

            GetData(sampleOutputBuffer, sampleResults, sampleCount);
        }

        public void BeginEvaluateDensitySamplesAsync(Vector4[] samplePositions)
        {
            if (hasPendingReadback)
            {
                throw new InvalidOperationException("A density readback is already pending.");
            }

            if (!SystemInfo.supportsAsyncGPUReadback)
            {
                throw new NotSupportedException("Async GPU readback is required by the topology catalogue.");
            }

            pendingReadbackSampleCount = DispatchDensitySamples(samplePositions);
            pendingReadback = sampleOutputBuffer.BufferMode == PlanetGpuBufferMode.GraphicsBuffer
                ? AsyncGPUReadback.Request(sampleOutputBuffer.GraphicsBuffer)
                : AsyncGPUReadback.Request(sampleOutputBuffer.ComputeBuffer);
            hasPendingReadback = true;
        }

        public bool TryCompleteDensitySamplesAsync(Vector4[] sampleResults)
        {
            if (!hasPendingReadback || !pendingReadback.done)
            {
                return false;
            }

            if (pendingReadback.hasError)
            {
                ClearPendingReadback();
                throw new InvalidOperationException("The asynchronous density readback failed.");
            }

            if (sampleResults == null || sampleResults.Length < pendingReadbackSampleCount)
            {
                throw new ArgumentException("sampleResults is too small for the pending readback.", nameof(sampleResults));
            }

            var data = pendingReadback.GetData<Vector4>();
            for (int index = 0; index < pendingReadbackSampleCount; index++)
            {
                sampleResults[index] = data[index];
            }

            ClearPendingReadback();
            return true;
        }

        public void Release()
        {
            if (hasPendingReadback)
            {
                pendingReadback.WaitForCompletion();
                ClearPendingReadback();
            }

            ReleaseBuffer(ref sampleOutputBuffer);
            ReleaseBuffer(ref sampleInputBuffer);
            ReleaseBuffer(ref cellBuffer);
            ReleaseBuffer(ref parameterBuffer);
            computeShader = null;
            evaluateKernel = 0;
            threadGroupSizeX = 0;
        }

        private int DispatchDensitySamples(Vector4[] samplePositions)
        {
            if (hasPendingReadback)
            {
                throw new InvalidOperationException("Cannot dispatch while an asynchronous density readback is pending.");
            }

            if (!IsInitialized)
            {
                throw new InvalidOperationException("PlanetGpuShapeEvaluator must be initialized before evaluating samples.");
            }

            if (samplePositions == null)
            {
                throw new ArgumentNullException(nameof(samplePositions));
            }

            if (samplePositions.Length == 0)
            {
                throw new ArgumentException("At least one sample is required.", nameof(samplePositions));
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
            return samplePositions.Length;
        }

        private void ClearPendingReadback()
        {
            pendingReadback = default;
            pendingReadbackSampleCount = 0;
            hasPendingReadback = false;
        }

        private void EnsureSampleBuffers(int sampleCount)
        {
            sampleInputBuffer = EnsureBuffer(sampleInputBuffer, "Planet Shape Sample Positions", sampleCount, 16);
            sampleOutputBuffer = EnsureBuffer(sampleOutputBuffer, "Planet Shape Density Samples", sampleCount, 16);
        }

        private PlanetGpuBufferHandle CreateBuffer(string resourceName, int elementCount, int stride)
        {
            return bufferMode == PlanetGpuBufferMode.GraphicsBuffer
                ? PlanetGpuBufferHandle.CreateGraphicsBuffer(resourceName, elementCount, stride)
                : PlanetGpuBufferHandle.CreateComputeBuffer(resourceName, elementCount, stride);
        }

        private PlanetGpuBufferHandle EnsureBuffer(PlanetGpuBufferHandle handle, string resourceName, int elementCount, int stride)
        {
            if (handle != null &&
                handle.IsAlive &&
                handle.BufferMode == bufferMode &&
                handle.ElementCount >= elementCount &&
                handle.Stride == stride)
            {
                return handle;
            }

            ReleaseBuffer(ref handle);
            return CreateBuffer(resourceName, elementCount, stride);
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
