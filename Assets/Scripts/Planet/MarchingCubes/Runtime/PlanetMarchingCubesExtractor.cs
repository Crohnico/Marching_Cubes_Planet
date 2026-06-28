using System;
using MarchingCubesPlanet.Compute;
using MarchingCubesPlanet.Coordinates;
using MarchingCubesPlanet.Shape;
using UnityEngine;

namespace MarchingCubesPlanet.MarchingCubes
{
    public sealed class PlanetMarchingCubesExtractor
    {
        private static readonly int ShapeParametersId = Shader.PropertyToID("_PlanetShapeParameters");
        private static readonly int ShapeCellsId = Shader.PropertyToID("_PlanetShapeCells");
        private static readonly int VerticesId = Shader.PropertyToID("_MarchingCubesVertices");
        private static readonly int StateId = Shader.PropertyToID("_MarchingCubesState");
        private static readonly int CubeCountId = Shader.PropertyToID("_MarchingCubesCubeCount");
        private static readonly int GridRadiusId = Shader.PropertyToID("_MarchingCubesGridRadius");
        private static readonly int RadialStartOffsetId = Shader.PropertyToID("_MarchingCubesRadialStartOffset");
        private static readonly int RadialCubeCountId = Shader.PropertyToID("_MarchingCubesRadialCubeCount");
        private static readonly int TangentHalfExtentId = Shader.PropertyToID("_MarchingCubesTangentHalfExtent");
        private static readonly int TangentCubeCountId = Shader.PropertyToID("_MarchingCubesTangentCubeCount");
        private static readonly int CubeSizeGridId = Shader.PropertyToID("_MarchingCubesCubeSizeGrid");
        private static readonly int MaxTriangleCountId = Shader.PropertyToID("_MarchingCubesMaxTriangleCount");

        private const string ExtractKernelName = "CS_ExtractValidationPatch";

        private readonly PlanetMarchingCubesState[] stateUpload = new PlanetMarchingCubesState[1];
        private readonly PlanetMarchingCubesState[] stateReadback = new PlanetMarchingCubesState[1];

        private ComputeShader computeShader;
        private int extractKernel;
        private uint threadGroupSizeX;
        private PlanetGpuBufferMode bufferMode;
        private PlanetRecipe recipe;
        private PlanetMarchingCubesSettings settings;
        private PlanetGpuShapeEvaluator shapeEvaluator;
        private PlanetGpuBufferHandle vertexBuffer;
        private PlanetGpuBufferHandle stateBuffer;
        private PlanetMarchingCubesVertex[] vertexReadback;

        public bool IsInitialized => vertexBuffer != null && vertexBuffer.IsAlive &&
                                     stateBuffer != null && stateBuffer.IsAlive &&
                                     shapeEvaluator != null && shapeEvaluator.IsInitialized;
        public uint ThreadGroupSizeX => threadGroupSizeX;
        public PlanetGpuBufferHandle VertexBuffer => vertexBuffer;
        public PlanetGpuBufferHandle StateBuffer => stateBuffer;
        public PlanetMarchingCubesSettings Settings => settings;

        public void Initialize(
            ComputeShader shader,
            in PlanetRecipe sourceRecipe,
            in PlanetMarchingCubesSettings extractionSettings,
            PlanetGpuShapeEvaluator initializedShapeEvaluator,
            PlanetGpuBufferMode requestedBufferMode)
        {
            if (shader == null)
            {
                throw new ArgumentNullException(nameof(shader));
            }

            if (!PlanetRecipeValidator.Validate(in sourceRecipe, out string recipeMessage))
            {
                throw new ArgumentException(recipeMessage, nameof(sourceRecipe));
            }

            if (!extractionSettings.Validate(out string settingsMessage))
            {
                throw new ArgumentException(settingsMessage, nameof(extractionSettings));
            }

            if (initializedShapeEvaluator == null || !initializedShapeEvaluator.IsInitialized)
            {
                throw new ArgumentException("PlanetGpuShapeEvaluator must be initialized before Marching Cubes extraction.", nameof(initializedShapeEvaluator));
            }

            Release();

            computeShader = shader;
            recipe = sourceRecipe;
            settings = extractionSettings;
            shapeEvaluator = initializedShapeEvaluator;
            bufferMode = requestedBufferMode;
            extractKernel = computeShader.FindKernel(ExtractKernelName);
            computeShader.GetKernelThreadGroupSizes(extractKernel, out threadGroupSizeX, out _, out _);

            vertexBuffer = CreateBuffer(
                "Planet Marching Cubes Vertices",
                settings.MaxValidationVertices,
                PlanetMarchingCubesVertex.Stride);
            stateBuffer = CreateBuffer("Planet Marching Cubes State", 1, PlanetMarchingCubesState.Stride);
            vertexReadback = new PlanetMarchingCubesVertex[settings.MaxValidationVertices];
        }

        public PlanetMarchingCubesExtractionResult ExtractValidationPatch()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("PlanetMarchingCubesExtractor must be initialized before extraction.");
            }

            stateUpload[0] = default;
            SetData(stateBuffer, stateUpload, 1);

            shapeEvaluator.ParameterBuffer.BindTo(computeShader, extractKernel, ShapeParametersId);
            shapeEvaluator.CellBuffer.BindTo(computeShader, extractKernel, ShapeCellsId);
            vertexBuffer.BindTo(computeShader, extractKernel, VerticesId);
            stateBuffer.BindTo(computeShader, extractKernel, StateId);

            PlanetMarchingCubesRange range = settings.range;
            computeShader.SetInt(CubeCountId, (int)range.CubeCount);
            computeShader.SetInt(GridRadiusId, recipe.GridRadius);
            computeShader.SetInt(RadialStartOffsetId, range.radialStartOffset);
            computeShader.SetInt(RadialCubeCountId, range.radialCubeCount);
            computeShader.SetInt(TangentHalfExtentId, range.tangentHalfExtent);
            computeShader.SetInt(TangentCubeCountId, range.tangentCubeCount);
            computeShader.SetFloat(CubeSizeGridId, range.cubeSizeGrid);
            computeShader.SetInt(MaxTriangleCountId, settings.maxValidationTriangles);

            uint safeThreadGroupSizeX = threadGroupSizeX == 0u ? 1u : threadGroupSizeX;
            int groupCount = Mathf.CeilToInt(range.CubeCount / (float)safeThreadGroupSizeX);
            computeShader.Dispatch(extractKernel, groupCount, 1, 1);

            GetData(stateBuffer, stateReadback, 1);
            PlanetMarchingCubesState state = stateReadback[0];
            int clampedVertexCount = Mathf.Min(
                (int)Math.Min(state.vertexCountWritten, (uint)settings.MaxValidationVertices),
                settings.MaxValidationVertices);

            if (clampedVertexCount > 0)
            {
                GetData(vertexBuffer, vertexReadback, clampedVertexCount);
            }

            return new PlanetMarchingCubesExtractionResult(
                state,
                vertexReadback,
                clampedVertexCount,
                settings.maxValidationTriangles);
        }

        public void Release()
        {
            ReleaseBuffer(ref stateBuffer);
            ReleaseBuffer(ref vertexBuffer);
            computeShader = null;
            extractKernel = 0;
            threadGroupSizeX = 0;
            shapeEvaluator = null;
            vertexReadback = null;
        }

        private PlanetGpuBufferHandle CreateBuffer(string debugName, int elementCount, int stride)
        {
            return bufferMode == PlanetGpuBufferMode.GraphicsBuffer
                ? PlanetGpuBufferHandle.CreateGraphicsBuffer(debugName, elementCount, stride)
                : PlanetGpuBufferHandle.CreateComputeBuffer(debugName, elementCount, stride);
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
