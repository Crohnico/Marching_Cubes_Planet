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
        private static readonly int DrawArgsId = Shader.PropertyToID("_MarchingCubesDrawArgs");
        private static readonly int ChunkOccupancyId = Shader.PropertyToID("_MarchingCubesChunkOccupancy");
        private static readonly int EdgeTableId = Shader.PropertyToID("_MarchingCubesEdgeTable");
        private static readonly int TriTableId = Shader.PropertyToID("_MarchingCubesTriTable");
        private static readonly int CellCountId = Shader.PropertyToID("_MarchingCubesCellCount");
        private static readonly int CellStartIndexId = Shader.PropertyToID("_MarchingCubesCellStartIndex");
        private static readonly int CellEndIndexId = Shader.PropertyToID("_MarchingCubesCellEndIndex");
        private static readonly int ChunkSizeId = Shader.PropertyToID("_MarchingCubesChunkSize");
        private static readonly int ChunkIndexBaseId = Shader.PropertyToID("_MarchingCubesChunkIndexBase");
        private static readonly int OutputPrimitiveLimitId = Shader.PropertyToID("_MarchingCubesOutputPrimitiveLimit");
        private static readonly int OutputVertexLimitId = Shader.PropertyToID("_MarchingCubesOutputVertexLimit");
        private static readonly int WriteEnabledId = Shader.PropertyToID("_MarchingCubesWriteEnabled");
        private static readonly int DrawArgsEnabledId = Shader.PropertyToID("_MarchingCubesDrawArgsEnabled");
        private static readonly int ChunkOccupancyEnabledId = Shader.PropertyToID("_MarchingCubesChunkOccupancyEnabled");
        private static readonly int PlanetGridRadiusId = Shader.PropertyToID("_PlanetGridRadius");
        private static readonly int PlanetMaterialOuterRadiusId = Shader.PropertyToID("_PlanetMaterialOuterRadius");
        private static readonly int PlanetWorldScaleId = Shader.PropertyToID("_PlanetWorldScale");
        private static readonly int PlanetSeedId = Shader.PropertyToID("_PlanetSeed");
        private static readonly int PlanetLayerCountId = Shader.PropertyToID("_PlanetLayerCount");
        private static readonly int PlanetLayerHeightsId = Shader.PropertyToID("_PlanetLayerHeights");
        private static readonly int PlanetLayerNoiseId = Shader.PropertyToID("_PlanetLayerNoise");
        private static readonly int PlanetLayerFlagsId = Shader.PropertyToID("_PlanetLayerFlags");
        private static readonly int PlanetLayerSeedsId = Shader.PropertyToID("_PlanetLayerSeeds");
        private static readonly int CaveEvaluationEnabledId = Shader.PropertyToID("_PlanetCaveEvaluationEnabled");

        private const string ExtractChunkedCartesianSurfaceKernelName = "CS_ExtractChunkedCartesianSurface";
        private const int MaxThreadGroupsPerDispatchAxis = 65535;
        private const int ReusableChunkVertexCapacity = PlanetMarchingCubesSettings.Lod0OutputVertexCapacityBudget;

        private readonly PlanetMarchingCubesState[] stateUpload = new PlanetMarchingCubesState[1];
        private readonly PlanetMarchingCubesState[] stateReadback = new PlanetMarchingCubesState[1];
        private readonly PlanetMarchingCubesChunkOrigin[] singleChunkUpload = new PlanetMarchingCubesChunkOrigin[1];
        private readonly PlanetMarchingCubesChunkOrigin[] reusableSingleChunkCandidate = new PlanetMarchingCubesChunkOrigin[1];
        private readonly Vector4[] layerHeightUpload = new Vector4[PlanetRecipe.MaxMaterialLayers];
        private readonly Vector4[] layerNoiseUpload = new Vector4[PlanetRecipe.MaxMaterialLayers];
        private readonly Vector4[] layerFlagUpload = new Vector4[PlanetRecipe.MaxMaterialLayers];
        private readonly Vector4[] layerSeedUpload = new Vector4[PlanetRecipe.MaxMaterialLayers];

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
        private PlanetGpuBufferHandle drawArgsBuffer;
        private PlanetGpuBufferHandle chunkOccupancyBuffer;
        private PlanetGpuBufferHandle edgeTableBuffer;
        private PlanetGpuBufferHandle triTableBuffer;
        private PlanetMarchingCubesVertex[] vertexReadback;
        private uint[] chunkOccupancyClear = Array.Empty<uint>();
        private PlanetMarchingCubesChunkOrigin[] chunkOrigins = Array.Empty<PlanetMarchingCubesChunkOrigin>();
        private PlanetMarchingCubesChunkBuildStats chunkBuildStats;
        private bool hasActiveIncrementalExtraction;
        private int incrementalCandidateCount;
        private int incrementalChunkIndexBase;
        private long incrementalActiveCellCount;
        private long incrementalCountCellStart;
        private long incrementalWriteCellStart;
        private bool incrementalCountFinished;
        private bool caveEvaluationEnabled;

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
            PlanetGpuBufferMode requestedBufferMode,
            bool evaluateCaves = true)
        {
            PlanetMarchingCubesSettings sanitizedSettings = extractionSettings;
            sanitizedSettings.EnsureDefaults();
            PlanetMarchingCubesChunkOrigin[] candidates =
                sanitizedSettings.chunkRange.BuildCandidateChunks(in sourceRecipe, out PlanetMarchingCubesChunkBuildStats buildStats);

            InitializeWithCandidates(
                shader,
                in sourceRecipe,
                in sanitizedSettings,
                initializedShapeEvaluator,
                requestedBufferMode,
                candidates,
                buildStats,
                false,
                evaluateCaves);
        }

        public void InitializeSingleChunk(
            ComputeShader shader,
            in PlanetRecipe sourceRecipe,
            in PlanetMarchingCubesSettings extractionSettings,
            PlanetGpuShapeEvaluator initializedShapeEvaluator,
            PlanetGpuBufferMode requestedBufferMode,
            PlanetMarchingCubesChunkOrigin chunkOrigin,
            bool evaluateCaves = true)
        {
            PlanetMarchingCubesSettings sanitizedSettings = extractionSettings;
            sanitizedSettings.EnsureDefaults();
            sanitizedSettings.chunkRange.chunkSize = Mathf.Max(1, sanitizedSettings.chunkRange.chunkSize);
            PlanetMarchingCubesChunkOrigin[] candidates = { chunkOrigin };
            PlanetMarchingCubesChunkBuildStats buildStats = new PlanetMarchingCubesChunkBuildStats(
                1,
                1L,
                0f,
                0f,
                sanitizedSettings.chunkRange.chunkSize,
                false);

            InitializeWithCandidates(
                shader,
                in sourceRecipe,
                in sanitizedSettings,
                initializedShapeEvaluator,
                requestedBufferMode,
                candidates,
                buildStats,
                false,
                evaluateCaves);
        }

        public void InitializeReusableSingleChunk(
            ComputeShader shader,
            in PlanetRecipe sourceRecipe,
            in PlanetMarchingCubesSettings extractionSettings,
            PlanetGpuShapeEvaluator initializedShapeEvaluator,
            PlanetGpuBufferMode requestedBufferMode,
            PlanetMarchingCubesChunkOrigin chunkOrigin,
            bool evaluateCaves = true)
        {
            PlanetMarchingCubesSettings sanitizedSettings = extractionSettings;
            sanitizedSettings.EnsureDefaults();
            sanitizedSettings.chunkRange.chunkSize = Mathf.Max(1, sanitizedSettings.chunkRange.chunkSize);
            reusableSingleChunkCandidate[0] = chunkOrigin;
            PlanetMarchingCubesChunkBuildStats buildStats = new PlanetMarchingCubesChunkBuildStats(
                1,
                1L,
                0f,
                0f,
                sanitizedSettings.chunkRange.chunkSize,
                false);

            InitializeWithCandidates(
                shader,
                in sourceRecipe,
                in sanitizedSettings,
                initializedShapeEvaluator,
                requestedBufferMode,
                reusableSingleChunkCandidate,
                buildStats,
                true,
                evaluateCaves);
        }

        private void InitializeWithCandidates(
            ComputeShader shader,
            in PlanetRecipe sourceRecipe,
            in PlanetMarchingCubesSettings extractionSettings,
            PlanetGpuShapeEvaluator initializedShapeEvaluator,
            PlanetGpuBufferMode requestedBufferMode,
            PlanetMarchingCubesChunkOrigin[] candidates,
            PlanetMarchingCubesChunkBuildStats buildStats,
            bool reuseBuffers,
            bool evaluateCaves)
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

            if (reuseBuffers)
            {
                CancelActiveExtraction();
            }
            else
            {
                Release();
            }

            computeShader = shader;
            recipe = sourceRecipe;
            settings = extractionSettings;
            shapeEvaluator = initializedShapeEvaluator;
            bufferMode = requestedBufferMode;
            caveEvaluationEnabled = evaluateCaves;
            chunkOrigins = candidates;
            chunkBuildStats = buildStats;
            if (CandidateCellCount > uint.MaxValue)
            {
                throw new InvalidOperationException(
                    "Candidate cell count exceeds the current 32-bit GPU dispatch contract. Reduce GridRadius or split the extraction into several 07 jobs.");
            }

            extractKernel = computeShader.FindKernel(ExtractChunkedCartesianSurfaceKernelName);
            computeShader.GetKernelThreadGroupSizes(extractKernel, out threadGroupSizeX, out _, out _);
            UploadMaterialLayers(in sourceRecipe);

            int chunkOriginElementCount = Math.Max(1, chunkOrigins.Length);
            chunkOriginBuffer = EnsureBuffer(
                chunkOriginBuffer,
                "Planet Marching Cubes Chunk Origins",
                chunkOriginElementCount,
                PlanetMarchingCubesChunkOrigin.Stride);
            int reusableVertexCapacity = reuseBuffers
                ? RoundUpToPowerOfTwo(Math.Max(settings.outputVertexCapacity, ReusableChunkVertexCapacity))
                : settings.outputVertexCapacity;

            vertexBuffer = EnsureBuffer(
                vertexBuffer,
                "Planet Marching Cubes Vertices",
                reusableVertexCapacity,
                PlanetMarchingCubesVertex.Stride);
            stateBuffer = EnsureBuffer(stateBuffer, "Planet Marching Cubes State", 1, PlanetMarchingCubesState.Stride);
            drawArgsBuffer = EnsureBuffer(drawArgsBuffer, "Planet Marching Cubes Legacy Draw Args", 4, sizeof(uint));
            chunkOccupancyBuffer = EnsureBuffer(
                chunkOccupancyBuffer,
                "Planet Marching Cubes Chunk Occupancy",
                chunkOriginElementCount,
                sizeof(uint));
            edgeTableBuffer = EnsureBuffer(edgeTableBuffer, "Planet Marching Cubes Edge Table", PlanetMarchingCubesLookupTables.EdgeTable.Length, sizeof(uint));
            triTableBuffer = EnsureBuffer(triTableBuffer, "Planet Marching Cubes Tri Table", PlanetMarchingCubesLookupTables.TriTable.Length, sizeof(int));

            if (chunkOrigins.Length > 0)
            {
                SetData(chunkOriginBuffer, chunkOrigins, chunkOrigins.Length);
            }

            SetData(edgeTableBuffer, PlanetMarchingCubesLookupTables.EdgeTable, PlanetMarchingCubesLookupTables.EdgeTable.Length);
            SetData(triTableBuffer, PlanetMarchingCubesLookupTables.TriTable, PlanetMarchingCubesLookupTables.TriTable.Length);
            if (vertexReadback == null || vertexReadback.Length < reusableVertexCapacity)
            {
                vertexReadback = new PlanetMarchingCubesVertex[reusableVertexCapacity];
            }

            if (chunkOccupancyClear.Length < chunkOriginElementCount)
            {
                chunkOccupancyClear = new uint[chunkOriginElementCount];
            }
        }

        public int ClassifyCandidateChunkSurfaces(uint[] occupancyResults)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("PlanetMarchingCubesExtractor must be initialized before chunk classification.");
            }

            int candidateCount = chunkOrigins.Length;
            if (occupancyResults == null || occupancyResults.Length < candidateCount)
            {
                throw new ArgumentException("Chunk occupancy output must fit every candidate chunk.", nameof(occupancyResults));
            }

            if (candidateCount == 0)
            {
                return 0;
            }

            Array.Clear(chunkOccupancyClear, 0, candidateCount);
            SetData(chunkOccupancyBuffer, chunkOccupancyClear, candidateCount);
            UploadChunkOrigins(0, candidateCount);

            long activeCellCount = (long)candidateCount * CellsPerActiveChunk;
            BindCommonBuffers(extractKernel);
            ResetExtractionState(candidateCount);
            SetCommonParameters(activeCellCount, 0);
            computeShader.SetInt(WriteEnabledId, 0);
            computeShader.SetInt(ChunkOccupancyEnabledId, 1);
            Dispatch(extractKernel, activeCellCount);
            GetData(chunkOccupancyBuffer, occupancyResults, candidateCount);
            computeShader.SetInt(ChunkOccupancyEnabledId, 0);

            int occupiedCount = 0;
            for (int i = 0; i < candidateCount; i++)
            {
                if (occupancyResults[i] != 0u)
                {
                    occupiedCount++;
                }
            }

            return occupiedCount;
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

        public PlanetMarchingCubesState CountCandidateChunkSurface(int candidateChunkIndex)
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

            return CountCartesianSurfaceRange(candidateChunkIndex, 1, candidateChunkIndex);
        }

        public void BeginCandidateChunkSurfaceExtraction(int candidateChunkIndex)
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

            BeginCartesianSurfaceRangeExtraction(candidateChunkIndex, 1, candidateChunkIndex);
        }

        public bool ContinueActiveExtraction(
            int cellBudget,
            out bool completed,
            out PlanetMarchingCubesExtractionResult result)
        {
            completed = false;
            result = PlanetMarchingCubesExtractionResult.Empty;
            if (!hasActiveIncrementalExtraction)
            {
                return false;
            }

            int safeCellBudget = Mathf.Max(1, cellBudget);
            if (!incrementalCountFinished)
            {
                long remainingCountCells = incrementalActiveCellCount - incrementalCountCellStart;
                long countCellBatch = Math.Min(remainingCountCells, safeCellBudget);
                computeShader.SetInt(WriteEnabledId, 0);
                DispatchRange(extractKernel, incrementalCountCellStart, countCellBatch, incrementalActiveCellCount);
                incrementalCountCellStart += countCellBatch;

                if (incrementalCountCellStart < incrementalActiveCellCount)
                {
                    return true;
                }

                GetData(stateBuffer, stateReadback, 1);
                PlanetMarchingCubesState countedState = stateReadback[0];
                if ((ulong)countedState.triangleCountAttempted * 3UL > (ulong)settings.outputVertexCapacity)
                {
                    countedState.triangleCountWritten = 0u;
                    countedState.vertexCountWritten = 0u;
                    countedState.overflowFlag = 1u;
                    result = new PlanetMarchingCubesExtractionResult(
                        countedState,
                        vertexReadback,
                        0);
                    hasActiveIncrementalExtraction = false;
                    completed = true;
                    return true;
                }

                ResetExtractionState(incrementalCandidateCount);
                SetCommonParameters(incrementalActiveCellCount, incrementalChunkIndexBase);
                incrementalCountFinished = true;
                return true;
            }

            long remainingWriteCells = incrementalActiveCellCount - incrementalWriteCellStart;
            long writeCellBatch = Math.Min(remainingWriteCells, safeCellBudget);
            computeShader.SetInt(WriteEnabledId, 1);
            DispatchRange(extractKernel, incrementalWriteCellStart, writeCellBatch, incrementalActiveCellCount);
            incrementalWriteCellStart += writeCellBatch;

            if (incrementalWriteCellStart < incrementalActiveCellCount)
            {
                return true;
            }

            result = ReadbackResult();
            hasActiveIncrementalExtraction = false;
            completed = true;
            return true;
        }

        public void CancelActiveExtraction()
        {
            hasActiveIncrementalExtraction = false;
            incrementalCandidateCount = 0;
            incrementalChunkIndexBase = 0;
            incrementalActiveCellCount = 0L;
            incrementalCountCellStart = 0L;
            incrementalWriteCellStart = 0L;
            incrementalCountFinished = false;
        }

        public void DetachShapeEvaluator()
        {
            CancelActiveExtraction();
            shapeEvaluator = null;
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

            BindCommonBuffers(extractKernel);
            ResetExtractionState(safeCandidateCount);
            SetCommonParameters(activeCellCount, chunkIndexBase);
            computeShader.SetInt(WriteEnabledId, 0);
            Dispatch(extractKernel, activeCellCount);

            GetData(stateBuffer, stateReadback, 1);
            PlanetMarchingCubesState countedState = stateReadback[0];
            if ((ulong)countedState.triangleCountAttempted * 3UL > (ulong)settings.outputVertexCapacity)
            {
                countedState.triangleCountWritten = 0u;
                countedState.vertexCountWritten = 0u;
                countedState.overflowFlag = 1u;
                return new PlanetMarchingCubesExtractionResult(
                    countedState,
                    vertexReadback,
                    0);
            }

            BindCommonBuffers(extractKernel);
            ResetExtractionState(safeCandidateCount);
            SetCommonParameters(activeCellCount, chunkIndexBase);
            computeShader.SetInt(WriteEnabledId, 1);
            Dispatch(extractKernel, activeCellCount);
            return ReadbackResult();
        }

        private PlanetMarchingCubesState CountCartesianSurfaceRange(
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
                return default;
            }

            int safeCandidateCount = Mathf.Clamp(candidateCount, 0, chunkOrigins.Length - safeCandidateStartIndex);
            if (safeCandidateCount <= 0)
            {
                return default;
            }

            UploadChunkOrigins(safeCandidateStartIndex, safeCandidateCount);
            long activeCellCount = (long)safeCandidateCount * CellsPerActiveChunk;

            BindCommonBuffers(extractKernel);
            ResetExtractionState(safeCandidateCount);
            SetCommonParameters(activeCellCount, chunkIndexBase);
            computeShader.SetInt(WriteEnabledId, 0);
            Dispatch(extractKernel, activeCellCount);

            GetData(stateBuffer, stateReadback, 1);
            return stateReadback[0];
        }

        private void BeginCartesianSurfaceRangeExtraction(
            int candidateStartIndex,
            int candidateCount,
            int chunkIndexBase)
        {
            int safeCandidateStartIndex = Mathf.Max(0, candidateStartIndex);
            int safeCandidateCount = Mathf.Clamp(candidateCount, 0, chunkOrigins.Length - safeCandidateStartIndex);
            if (safeCandidateCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(candidateCount), candidateCount, "Candidate count must resolve to at least one chunk.");
            }

            UploadChunkOrigins(safeCandidateStartIndex, safeCandidateCount);
            BindCommonBuffers(extractKernel);
            ResetExtractionState(safeCandidateCount);
            incrementalCandidateCount = safeCandidateCount;
            incrementalChunkIndexBase = Mathf.Max(0, chunkIndexBase);
            incrementalActiveCellCount = (long)safeCandidateCount * CellsPerActiveChunk;
            incrementalCountCellStart = 0L;
            incrementalWriteCellStart = 0L;
            incrementalCountFinished = false;
            hasActiveIncrementalExtraction = true;
            SetCommonParameters(incrementalActiveCellCount, incrementalChunkIndexBase);
        }

        public void Release()
        {
            CancelActiveExtraction();
            ReleaseBuffer(ref triTableBuffer);
            ReleaseBuffer(ref edgeTableBuffer);
            ReleaseBuffer(ref drawArgsBuffer);
            ReleaseBuffer(ref chunkOccupancyBuffer);
            ReleaseBuffer(ref stateBuffer);
            ReleaseBuffer(ref vertexBuffer);
            ReleaseBuffer(ref chunkOriginBuffer);
            computeShader = null;
            extractKernel = 0;
            threadGroupSizeX = 0;
            shapeEvaluator = null;
            vertexReadback = null;
            chunkOccupancyClear = Array.Empty<uint>();
            chunkOrigins = Array.Empty<PlanetMarchingCubesChunkOrigin>();
            chunkBuildStats = default;
        }

        private void BindCommonBuffers(int kernel)
        {
            shapeEvaluator.ParameterBuffer.BindTo(computeShader, kernel, ShapeParametersId);
            shapeEvaluator.CellBuffer.BindTo(computeShader, kernel, ShapeCellsId);
            computeShader.SetInt(CaveEvaluationEnabledId, caveEvaluationEnabled ? 1 : 0);
            chunkOriginBuffer.BindTo(computeShader, kernel, ChunkOriginsId);
            vertexBuffer.BindTo(computeShader, kernel, VerticesId);
            stateBuffer.BindTo(computeShader, kernel, StateId);
            drawArgsBuffer.BindTo(computeShader, kernel, DrawArgsId);
            chunkOccupancyBuffer.BindTo(computeShader, kernel, ChunkOccupancyId);
            edgeTableBuffer.BindTo(computeShader, kernel, EdgeTableId);
            triTableBuffer.BindTo(computeShader, kernel, TriTableId);
            computeShader.SetInt(DrawArgsEnabledId, 0);
            computeShader.SetInt(ChunkOccupancyEnabledId, 0);
        }

        private void UploadMaterialLayers(in PlanetRecipe sourceRecipe)
        {
            PlanetMaterialLayer[] layers = sourceRecipe.MaterialLayers;
            int layerCount = Mathf.Clamp(layers == null ? 0 : layers.Length, 1, PlanetRecipe.MaxMaterialLayers);
            for (int i = 0; i < PlanetRecipe.MaxMaterialLayers; i++)
            {
                PlanetMaterialLayer layer = i < layerCount
                    ? layers[i]
                    : PlanetMaterialLayer.Base("Unused", Color.magenta);
                layerHeightUpload[i] = new Vector4(
                    layer.MinAppearance,
                    layer.MaxAppearance,
                    layer.UnderwaterCoherence * 0.01f,
                    layer.SurfaceCoherence * 0.01f);
                layerNoiseUpload[i] = new Vector4(
                    layer.Abundance * 0.01f,
                    layer.Coherence * 0.01f,
                    layer.UnderwaterProbability * 0.01f,
                    layer.SurfaceProbability * 0.01f);
                layerFlagUpload[i] = new Vector4(
                    layer.Enabled ? 1f : 0f,
                    (float)layer.Operation,
                    (float)layer.UnderwaterBehaviour,
                    (float)layer.SurfaceBehaviour);
                layerSeedUpload[i] = new Vector4(
                    i,
                    (float)layer.Material,
                    layer.SurfaceMinAppearance,
                    layer.SurfaceMaxAppearance);
            }

            PlanetMarchingCubesChunkRange.CalculateSurfaceShell(in sourceRecipe, 0, out _, out float materialOuterRadius);
            computeShader.SetFloat(PlanetGridRadiusId, sourceRecipe.GridRadius);
            computeShader.SetFloat(PlanetMaterialOuterRadiusId, materialOuterRadius);
            computeShader.SetFloat(PlanetWorldScaleId, sourceRecipe.WorldScale);
            computeShader.SetFloat(PlanetSeedId, sourceRecipe.Seed);
            computeShader.SetInt(PlanetLayerCountId, layerCount);
            computeShader.SetVectorArray(PlanetLayerHeightsId, layerHeightUpload);
            computeShader.SetVectorArray(PlanetLayerNoiseId, layerNoiseUpload);
            computeShader.SetVectorArray(PlanetLayerFlagsId, layerFlagUpload);
            computeShader.SetVectorArray(PlanetLayerSeedsId, layerSeedUpload);
        }

        private void ResetExtractionState(int activeChunkCount)
        {
            uint safeActiveChunkCount = (uint)Mathf.Max(0, activeChunkCount);
            stateUpload[0] = new PlanetMarchingCubesState
            {
                chunkCountCandidate = safeActiveChunkCount,
                chunkCountProcessed = safeActiveChunkCount
            };
            SetData(stateBuffer, stateUpload, 1);
        }

        private void SetCommonParameters(long activeCellCount, int chunkIndexBase)
        {
            computeShader.SetInt(CellCountId, unchecked((int)(uint)Math.Max(0L, activeCellCount)));
            computeShader.SetInt(ChunkSizeId, ActiveChunkSize);
            computeShader.SetInt(ChunkIndexBaseId, Mathf.Max(0, chunkIndexBase));
            computeShader.SetInt(OutputPrimitiveLimitId, settings.outputVertexCapacity / 3);
            computeShader.SetInt(OutputVertexLimitId, settings.outputVertexCapacity);
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
                DispatchRange(kernel, cellStartIndex, dispatchCellCount, cellCount);
                cellStartIndex += dispatchCellCount;
            }
        }

        private void DispatchRange(int kernel, long cellStartIndex, long cellCount, long totalCellCount)
        {
            if (cellCount <= 0)
            {
                return;
            }

            uint safeThreadGroupSizeX = threadGroupSizeX == 0u ? 1u : threadGroupSizeX;
            int groupCount = Mathf.CeilToInt(cellCount / (float)safeThreadGroupSizeX);
            computeShader.SetInt(CellCountId, unchecked((int)(uint)Math.Max(0L, totalCellCount)));
            computeShader.SetInt(CellStartIndexId, unchecked((int)(uint)Math.Max(0L, cellStartIndex)));
            computeShader.SetInt(CellEndIndexId, unchecked((int)(uint)Math.Min(totalCellCount, cellStartIndex + cellCount)));
            computeShader.Dispatch(kernel, groupCount, 1, 1);
        }

        private PlanetMarchingCubesExtractionResult ReadbackResult()
        {
            GetData(stateBuffer, stateReadback, 1);
            PlanetMarchingCubesState state = stateReadback[0];
            int clampedVertexCount = Mathf.Min(
                (int)Math.Min(state.vertexCountWritten, (uint)settings.outputVertexCapacity),
                settings.outputVertexCapacity);

            if (clampedVertexCount > 0)
            {
                GetData(vertexBuffer, vertexReadback, clampedVertexCount);
            }

            return new PlanetMarchingCubesExtractionResult(
                state,
                vertexReadback,
                clampedVertexCount);
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

        private static int RoundUpToPowerOfTwo(int value)
        {
            int safeValue = Mathf.Max(1, value);
            if (safeValue >= 1073741824)
            {
                return safeValue;
            }

            return Mathf.NextPowerOfTwo(safeValue);
        }
    }
}
