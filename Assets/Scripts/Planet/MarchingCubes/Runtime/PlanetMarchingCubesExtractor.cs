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
        private static readonly int ChunkOriginsId = Shader.PropertyToID("_MarchingCubesChunkOrigins");
        private static readonly int VerticesId = Shader.PropertyToID("_MarchingCubesVertices");
        private static readonly int StateId = Shader.PropertyToID("_MarchingCubesState");
        private static readonly int EdgeTableId = Shader.PropertyToID("_MarchingCubesEdgeTable");
        private static readonly int TriTableId = Shader.PropertyToID("_MarchingCubesTriTable");
        private static readonly int CellCountId = Shader.PropertyToID("_MarchingCubesCellCount");
        private static readonly int CellStartIndexId = Shader.PropertyToID("_MarchingCubesCellStartIndex");
        private static readonly int ChunkSizeId = Shader.PropertyToID("_MarchingCubesChunkSize");
        private static readonly int ChunkIndexBaseId = Shader.PropertyToID("_MarchingCubesChunkIndexBase");
        private static readonly int MaxTriangleCountId = Shader.PropertyToID("_MarchingCubesMaxTriangleCount");
        private static readonly int WriteEnabledId = Shader.PropertyToID("_MarchingCubesWriteEnabled");

        private const string ExtractChunkedCartesianSurfaceKernelName = "CS_ExtractChunkedCartesianSurface";
        private const int MaxThreadGroupsPerDispatchAxis = 65535;

        private readonly PlanetMarchingCubesState[] stateUpload = new PlanetMarchingCubesState[1];
        private readonly PlanetMarchingCubesState[] stateReadback = new PlanetMarchingCubesState[1];
        private readonly PlanetMarchingCubesChunkOrigin[] singleChunkUpload = new PlanetMarchingCubesChunkOrigin[1];

        private ComputeShader computeShader;
        private int extractKernel;
        private uint threadGroupSizeX;
        private PlanetGpuBufferMode bufferMode;
        private PlanetRecipe recipe;
        private PlanetMarchingCubesSettings settings;
        private PlanetGpuShapeEvaluator shapeEvaluator;
        private PlanetGpuBufferHandle chunkOriginBuffer;
        private PlanetGpuBufferHandle vertexBuffer;
        private PlanetGpuBufferHandle stateBuffer;
        private PlanetGpuBufferHandle edgeTableBuffer;
        private PlanetGpuBufferHandle triTableBuffer;
        private PlanetMarchingCubesVertex[] vertexReadback;
        private PlanetMarchingCubesChunkOrigin[] chunkOrigins = Array.Empty<PlanetMarchingCubesChunkOrigin>();
        private PlanetMarchingCubesChunkBuildStats chunkBuildStats;

        public bool IsInitialized => vertexBuffer != null && vertexBuffer.IsAlive &&
                                     stateBuffer != null && stateBuffer.IsAlive &&
                                     chunkOriginBuffer != null && chunkOriginBuffer.IsAlive &&
                                     shapeEvaluator != null && shapeEvaluator.IsInitialized;
        public uint ThreadGroupSizeX => threadGroupSizeX;
        public PlanetGpuBufferHandle ChunkOriginBuffer => chunkOriginBuffer;
        public PlanetGpuBufferHandle VertexBuffer => vertexBuffer;
        public PlanetGpuBufferHandle StateBuffer => stateBuffer;
        public PlanetGpuBufferHandle EdgeTableBuffer => edgeTableBuffer;
        public PlanetGpuBufferHandle TriTableBuffer => triTableBuffer;
        public PlanetMarchingCubesSettings Settings => settings;
        public PlanetMarchingCubesChunkBuildStats ChunkBuildStats => chunkBuildStats;
        public int CandidateChunkCount => chunkOrigins.Length;
        public int ActiveChunkSize => Mathf.Max(1, settings.chunkRange.chunkSize);
        public long CellsPerActiveChunk => CalculateCellsPerChunk(ActiveChunkSize);
        public long CandidateCellCount => (long)chunkOrigins.Length * CellsPerActiveChunk;

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

            PlanetMarchingCubesChunkOrigin[] candidates =
                sanitizedSettings.chunkRange.BuildCandidateChunks(in sourceRecipe, out PlanetMarchingCubesChunkBuildStats buildStats);

            Release();

            computeShader = shader;
            recipe = sourceRecipe;
            settings = sanitizedSettings;
            shapeEvaluator = initializedShapeEvaluator;
            bufferMode = requestedBufferMode;
            chunkOrigins = candidates;
            chunkBuildStats = buildStats;
            if (CandidateCellCount > uint.MaxValue)
            {
                throw new InvalidOperationException(
                    "Candidate cell count exceeds the current 32-bit GPU dispatch contract. Reduce GridRadius or split the extraction into several 07 jobs.");
            }

            extractKernel = computeShader.FindKernel(ExtractChunkedCartesianSurfaceKernelName);
            computeShader.GetKernelThreadGroupSizes(extractKernel, out threadGroupSizeX, out _, out _);

            int chunkOriginElementCount = Math.Max(1, chunkOrigins.Length);
            chunkOriginBuffer = CreateBuffer(
                "Planet Marching Cubes Chunk Origins",
                chunkOriginElementCount,
                PlanetMarchingCubesChunkOrigin.Stride);
            vertexBuffer = CreateBuffer(
                "Planet Marching Cubes Vertices",
                settings.TemporaryOutputVertexCapacity,
                PlanetMarchingCubesVertex.Stride);
            stateBuffer = CreateBuffer("Planet Marching Cubes State", 1, PlanetMarchingCubesState.Stride);
            edgeTableBuffer = CreateBuffer("Planet Marching Cubes Edge Table", PlanetMarchingCubesLookupTables.EdgeTable.Length, sizeof(uint));
            triTableBuffer = CreateBuffer("Planet Marching Cubes Tri Table", PlanetMarchingCubesLookupTables.TriTable.Length, sizeof(int));

            if (chunkOrigins.Length > 0)
            {
                SetData(chunkOriginBuffer, chunkOrigins, chunkOrigins.Length);
            }

            SetData(edgeTableBuffer, PlanetMarchingCubesLookupTables.EdgeTable, PlanetMarchingCubesLookupTables.EdgeTable.Length);
            SetData(triTableBuffer, PlanetMarchingCubesLookupTables.TriTable, PlanetMarchingCubesLookupTables.TriTable.Length);
            vertexReadback = new PlanetMarchingCubesVertex[settings.TemporaryOutputVertexCapacity];
        }

        public PlanetMarchingCubesExtractionResult ExtractPlanetSurface()
        {
            return ExtractCartesianPlanetSurface();
        }

        public PlanetMarchingCubesExtractionResult ExtractCartesianPlanetSurface()
        {
            return ExtractCartesianSurfaceRange(0, chunkOrigins.Length, 0);
        }

        public PlanetMarchingCubesExtractionResult ExtractCandidateChunkSurface(int candidateChunkIndex)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("PlanetMarchingCubesExtractor must be initialized before extraction.");
            }

            if (candidateChunkIndex < 0 || candidateChunkIndex >= chunkOrigins.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(candidateChunkIndex),
                    candidateChunkIndex,
                    "Candidate chunk index is outside the current 07 candidate list.");
            }

            return ExtractCartesianSurfaceRange(candidateChunkIndex, 1, candidateChunkIndex);
        }

        public bool TryGetCandidateChunkOrigin(int candidateChunkIndex, out PlanetMarchingCubesChunkOrigin origin)
        {
            if (candidateChunkIndex >= 0 && candidateChunkIndex < chunkOrigins.Length)
            {
                origin = chunkOrigins[candidateChunkIndex];
                return true;
            }

            origin = default;
            return false;
        }

        private PlanetMarchingCubesExtractionResult ExtractCartesianSurfaceRange(
            int candidateStartIndex,
            int candidateCount,
            int chunkIndexBase)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("PlanetMarchingCubesExtractor must be initialized before extraction.");
            }

            int safeCandidateStartIndex = Mathf.Max(0, candidateStartIndex);
            if (safeCandidateStartIndex >= chunkOrigins.Length)
            {
                return PlanetMarchingCubesExtractionResult.Empty;
            }

            int safeCandidateCount = Mathf.Clamp(candidateCount, 0, chunkOrigins.Length - safeCandidateStartIndex);
            if (safeCandidateCount <= 0)
            {
                return PlanetMarchingCubesExtractionResult.Empty;
            }

            UploadChunkOrigins(safeCandidateStartIndex, safeCandidateCount);
            long activeCellCount = (long)safeCandidateCount * CellsPerActiveChunk;

            BindCommonBuffers(extractKernel, safeCandidateCount);
            SetCommonParameters(activeCellCount, chunkIndexBase);
            computeShader.SetInt(WriteEnabledId, 0);
            Dispatch(extractKernel, activeCellCount);

            GetData(stateBuffer, stateReadback, 1);
            PlanetMarchingCubesState countedState = stateReadback[0];
            if (countedState.triangleCountAttempted > settings.temporaryOutputTriangleCapacity)
            {
                countedState.triangleCountWritten = 0u;
                countedState.vertexCountWritten = 0u;
                countedState.overflowFlag = 1u;
                return new PlanetMarchingCubesExtractionResult(
                    countedState,
                    vertexReadback,
                    0,
                    settings.temporaryOutputTriangleCapacity);
            }

            BindCommonBuffers(extractKernel, safeCandidateCount);
            SetCommonParameters(activeCellCount, chunkIndexBase);
            computeShader.SetInt(WriteEnabledId, 1);
            Dispatch(extractKernel, activeCellCount);
            return ReadbackResult();
        }

        public void Release()
        {
            ReleaseBuffer(ref triTableBuffer);
            ReleaseBuffer(ref edgeTableBuffer);
            ReleaseBuffer(ref stateBuffer);
            ReleaseBuffer(ref vertexBuffer);
            ReleaseBuffer(ref chunkOriginBuffer);
            computeShader = null;
            extractKernel = 0;
            threadGroupSizeX = 0;
            shapeEvaluator = null;
            vertexReadback = null;
            chunkOrigins = Array.Empty<PlanetMarchingCubesChunkOrigin>();
            chunkBuildStats = default;
        }

        private void BindCommonBuffers(int kernel, int activeChunkCount)
        {
            uint safeActiveChunkCount = (uint)Mathf.Max(0, activeChunkCount);
            stateUpload[0] = new PlanetMarchingCubesState
            {
                chunkCountCandidate = safeActiveChunkCount,
                chunkCountProcessed = safeActiveChunkCount
            };
            SetData(stateBuffer, stateUpload, 1);

            shapeEvaluator.ParameterBuffer.BindTo(computeShader, kernel, ShapeParametersId);
            shapeEvaluator.CellBuffer.BindTo(computeShader, kernel, ShapeCellsId);
            chunkOriginBuffer.BindTo(computeShader, kernel, ChunkOriginsId);
            vertexBuffer.BindTo(computeShader, kernel, VerticesId);
            stateBuffer.BindTo(computeShader, kernel, StateId);
            edgeTableBuffer.BindTo(computeShader, kernel, EdgeTableId);
            triTableBuffer.BindTo(computeShader, kernel, TriTableId);
        }

        private void SetCommonParameters(long activeCellCount, int chunkIndexBase)
        {
            computeShader.SetInt(CellCountId, unchecked((int)(uint)Math.Max(0L, activeCellCount)));
            computeShader.SetInt(ChunkSizeId, ActiveChunkSize);
            computeShader.SetInt(ChunkIndexBaseId, Mathf.Max(0, chunkIndexBase));
            computeShader.SetInt(MaxTriangleCountId, settings.temporaryOutputTriangleCapacity);
        }

        private static long CalculateCellsPerChunk(int chunkSize)
        {
            long safeChunkSize = Math.Max(1, chunkSize);
            return safeChunkSize * safeChunkSize * safeChunkSize;
        }

        private void UploadChunkOrigins(int candidateStartIndex, int candidateCount)
        {
            if (candidateCount <= 0)
            {
                return;
            }

            if (candidateCount == 1)
            {
                singleChunkUpload[0] = chunkOrigins[candidateStartIndex];
                SetData(chunkOriginBuffer, singleChunkUpload, 1);
                return;
            }

            if (candidateStartIndex == 0 && candidateCount == chunkOrigins.Length)
            {
                SetData(chunkOriginBuffer, chunkOrigins, chunkOrigins.Length);
                return;
            }

            PlanetMarchingCubesChunkOrigin[] range = new PlanetMarchingCubesChunkOrigin[candidateCount];
            Array.Copy(chunkOrigins, candidateStartIndex, range, 0, candidateCount);
            SetData(chunkOriginBuffer, range, candidateCount);
        }

        private void Dispatch(int kernel, long cellCount)
        {
            if (cellCount <= 0)
            {
                return;
            }

            uint safeThreadGroupSizeX = threadGroupSizeX == 0u ? 1u : threadGroupSizeX;
            long maxCellsPerDispatch = MaxThreadGroupsPerDispatchAxis * (long)safeThreadGroupSizeX;
            long cellStartIndex = 0L;

            while (cellStartIndex < cellCount)
            {
                long remainingCells = cellCount - cellStartIndex;
                long dispatchCellCount = Math.Min(remainingCells, maxCellsPerDispatch);
                int groupCount = Mathf.CeilToInt(dispatchCellCount / (float)safeThreadGroupSizeX);
                computeShader.SetInt(CellStartIndexId, unchecked((int)(uint)cellStartIndex));
                computeShader.Dispatch(kernel, groupCount, 1, 1);
                cellStartIndex += dispatchCellCount;
            }
        }

        private PlanetMarchingCubesExtractionResult ReadbackResult()
        {
            GetData(stateBuffer, stateReadback, 1);
            PlanetMarchingCubesState state = stateReadback[0];
            int clampedVertexCount = Mathf.Min(
                (int)Math.Min(state.vertexCountWritten, (uint)settings.TemporaryOutputVertexCapacity),
                settings.TemporaryOutputVertexCapacity);

            if (clampedVertexCount > 0)
            {
                GetData(vertexBuffer, vertexReadback, clampedVertexCount);
            }

            return new PlanetMarchingCubesExtractionResult(
                state,
                vertexReadback,
                clampedVertexCount,
                settings.temporaryOutputTriangleCapacity);
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
