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
        private static readonly int EdgeTableId = Shader.PropertyToID("_MarchingCubesEdgeTable");
        private static readonly int TriTableId = Shader.PropertyToID("_MarchingCubesTriTable");
        private static readonly int CubeCountId = Shader.PropertyToID("_MarchingCubesCubeCount");
        private static readonly int CubeStartIndexId = Shader.PropertyToID("_MarchingCubesCubeStartIndex");
        private static readonly int GridRadiusId = Shader.PropertyToID("_MarchingCubesGridRadius");
        private static readonly int RadialStartOffsetId = Shader.PropertyToID("_MarchingCubesRadialStartOffset");
        private static readonly int RadialCubeCountId = Shader.PropertyToID("_MarchingCubesRadialCubeCount");
        private static readonly int SurfaceFaceResolutionId = Shader.PropertyToID("_MarchingCubesSurfaceFaceResolution");
        private static readonly int CubeSizeGridId = Shader.PropertyToID("_MarchingCubesCubeSizeGrid");
        private static readonly int MaxTriangleCountId = Shader.PropertyToID("_MarchingCubesMaxTriangleCount");

        private const string ExtractPlanetSurfaceKernelName = "CS_ExtractPlanetSurface";
        private const int MaxThreadGroupsPerDispatchAxis = 65535;

        private readonly PlanetMarchingCubesState[] stateUpload = new PlanetMarchingCubesState[1];
        private readonly PlanetMarchingCubesState[] stateReadback = new PlanetMarchingCubesState[1];

        private ComputeShader computeShader;
        private int extractPlanetSurfaceKernel;
        private uint threadGroupSizeX;
        private PlanetGpuBufferMode bufferMode;
        private PlanetRecipe recipe;
        private PlanetMarchingCubesSettings settings;
        private PlanetGpuShapeEvaluator shapeEvaluator;
        private PlanetGpuBufferHandle vertexBuffer;
        private PlanetGpuBufferHandle stateBuffer;
        private PlanetGpuBufferHandle edgeTableBuffer;
        private PlanetGpuBufferHandle triTableBuffer;
        private PlanetMarchingCubesVertex[] vertexReadback;

        public bool IsInitialized => vertexBuffer != null && vertexBuffer.IsAlive &&
                                     stateBuffer != null && stateBuffer.IsAlive &&
                                     shapeEvaluator != null && shapeEvaluator.IsInitialized;
        public uint ThreadGroupSizeX => threadGroupSizeX;
        public PlanetGpuBufferHandle VertexBuffer => vertexBuffer;
        public PlanetGpuBufferHandle StateBuffer => stateBuffer;
        public PlanetGpuBufferHandle EdgeTableBuffer => edgeTableBuffer;
        public PlanetGpuBufferHandle TriTableBuffer => triTableBuffer;
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

            PlanetMarchingCubesSettings sanitizedSettings = extractionSettings;
            sanitizedSettings.EnsureDefaults();
            if (!sanitizedSettings.Validate(out string settingsMessage))
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
            settings = sanitizedSettings;
            shapeEvaluator = initializedShapeEvaluator;
            bufferMode = requestedBufferMode;
            extractPlanetSurfaceKernel = computeShader.FindKernel(ExtractPlanetSurfaceKernelName);
            computeShader.GetKernelThreadGroupSizes(extractPlanetSurfaceKernel, out threadGroupSizeX, out _, out _);

            vertexBuffer = CreateBuffer(
                "Planet Marching Cubes Vertices",
                settings.MaxPlanetSurfaceVertices,
                PlanetMarchingCubesVertex.Stride);
            stateBuffer = CreateBuffer("Planet Marching Cubes State", 1, PlanetMarchingCubesState.Stride);
            edgeTableBuffer = CreateBuffer("Planet Marching Cubes Edge Table", PlanetMarchingCubesLookupTables.EdgeTable.Length, sizeof(uint));
            triTableBuffer = CreateBuffer("Planet Marching Cubes Tri Table", PlanetMarchingCubesLookupTables.TriTable.Length, sizeof(int));
            SetData(edgeTableBuffer, PlanetMarchingCubesLookupTables.EdgeTable, PlanetMarchingCubesLookupTables.EdgeTable.Length);
            SetData(triTableBuffer, PlanetMarchingCubesLookupTables.TriTable, PlanetMarchingCubesLookupTables.TriTable.Length);
            vertexReadback = new PlanetMarchingCubesVertex[settings.MaxPlanetSurfaceVertices];
        }

        public PlanetMarchingCubesExtractionResult ExtractPlanetSurface()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("PlanetMarchingCubesExtractor must be initialized before extraction.");
            }

            PlanetMarchingCubesSurfaceRange range = settings.surfaceRange;
            BindCommonBuffers(extractPlanetSurfaceKernel);
            SetCommonParameters((int)range.CubeCount);
            computeShader.SetInt(RadialStartOffsetId, range.radialStartOffset);
            computeShader.SetInt(RadialCubeCountId, range.radialCubeCount);
            computeShader.SetInt(SurfaceFaceResolutionId, range.faceResolution);
            computeShader.SetFloat(CubeSizeGridId, range.cubeSizeGrid);

            Dispatch(extractPlanetSurfaceKernel, range.CubeCount);
            return ReadbackResult();
        }

        public void Release()
        {
            ReleaseBuffer(ref triTableBuffer);
            ReleaseBuffer(ref edgeTableBuffer);
            ReleaseBuffer(ref stateBuffer);
            ReleaseBuffer(ref vertexBuffer);
            computeShader = null;
            extractPlanetSurfaceKernel = 0;
            threadGroupSizeX = 0;
            shapeEvaluator = null;
            vertexReadback = null;
        }

        private void BindCommonBuffers(int kernel)
        {
            stateUpload[0] = default;
            SetData(stateBuffer, stateUpload, 1);

            shapeEvaluator.ParameterBuffer.BindTo(computeShader, kernel, ShapeParametersId);
            shapeEvaluator.CellBuffer.BindTo(computeShader, kernel, ShapeCellsId);
            vertexBuffer.BindTo(computeShader, kernel, VerticesId);
            stateBuffer.BindTo(computeShader, kernel, StateId);
            edgeTableBuffer.BindTo(computeShader, kernel, EdgeTableId);
            triTableBuffer.BindTo(computeShader, kernel, TriTableId);
        }

        private void SetCommonParameters(int cubeCount)
        {
            computeShader.SetInt(CubeCountId, cubeCount);
            computeShader.SetInt(GridRadiusId, recipe.GridRadius);
            computeShader.SetInt(MaxTriangleCountId, settings.maxPlanetSurfaceTriangles);
        }

        private void Dispatch(int kernel, long cubeCount)
        {
            uint safeThreadGroupSizeX = threadGroupSizeX == 0u ? 1u : threadGroupSizeX;
            long maxCubesPerDispatch = MaxThreadGroupsPerDispatchAxis * (long)safeThreadGroupSizeX;
            long cubeStartIndex = 0L;

            while (cubeStartIndex < cubeCount)
            {
                long remainingCubes = cubeCount - cubeStartIndex;
                long dispatchCubeCount = Math.Min(remainingCubes, maxCubesPerDispatch);
                int groupCount = Mathf.CeilToInt(dispatchCubeCount / (float)safeThreadGroupSizeX);
                computeShader.SetInt(CubeStartIndexId, (int)cubeStartIndex);
                computeShader.Dispatch(kernel, groupCount, 1, 1);
                cubeStartIndex += dispatchCubeCount;
            }
        }

        private PlanetMarchingCubesExtractionResult ReadbackResult()
        {
            GetData(stateBuffer, stateReadback, 1);
            PlanetMarchingCubesState state = stateReadback[0];
            int clampedVertexCount = Mathf.Min(
                (int)Math.Min(state.vertexCountWritten, (uint)settings.MaxPlanetSurfaceVertices),
                settings.MaxPlanetSurfaceVertices);

            if (clampedVertexCount > 0)
            {
                GetData(vertexBuffer, vertexReadback, clampedVertexCount);
            }

            return new PlanetMarchingCubesExtractionResult(
                state,
                vertexReadback,
                clampedVertexCount,
                settings.maxPlanetSurfaceTriangles);
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
