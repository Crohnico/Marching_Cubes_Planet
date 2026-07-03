using System;
using System.Diagnostics;
using MarchingCubesPlanet.Compute;
using MarchingCubesPlanet.MarchingCubes;
using UnityEngine;

namespace MarchingCubesPlanet.Lab
{
    public sealed class PlanetMarchingCubesLab : PlanetLabModule
    {
        private const string OwnerName = "PlanetMarchingCubesLab";

        [Header("References")]
        [SerializeField] private ComputeShader marchingCubesComputeShader;
        [SerializeField] private PlanetGpuShapeLab shapeLab;
        [SerializeField] private PlanetLabResourceRegistry resourceRegistry;

        [Header("Extraction")]
        [SerializeField] private PlanetGpuBufferMode bufferMode = PlanetGpuBufferMode.GraphicsBuffer;
        [SerializeField] private PlanetMarchingCubesSettings settings = PlanetMarchingCubesSettings.Default();

        [Header("State")]
        [SerializeField] private bool supportsComputeShaders;
        [SerializeField] private uint lastCandidateChunkCount;
        [SerializeField] private uint lastProcessedChunkCount;
        [SerializeField] private uint lastProcessedCellCount;
        [SerializeField] private uint lastTriangleCountAttempted;
        [SerializeField] private uint lastTriangleCountWritten;
        [SerializeField] private uint lastVertexCountWritten;
        [SerializeField] private bool lastOverflow;
        [SerializeField] private bool lastInvalidCase;
        [SerializeField] private int lastExtractedCandidateChunkIndex = -1;
        [SerializeField] private string lastAction;
        [SerializeField] private PlanetLabMetricsSnapshot lastSnapshot;
        [SerializeField] private PlanetLabDiagnostic lastDiagnostic;

        private readonly Stopwatch stopwatch = new Stopwatch();
        private readonly PlanetMarchingCubesExtractor extractor = new PlanetMarchingCubesExtractor();

        private PlanetMarchingCubesExtractionResult lastResult = PlanetMarchingCubesExtractionResult.Empty;
        private int chunkOriginBufferResourceId;
        private int vertexBufferResourceId;
        private int stateBufferResourceId;
        private int edgeTableBufferResourceId;
        private int triTableBufferResourceId;

        public override string ModuleName => "Planet Marching Cubes Lab";
        public override bool HasLiveResources => extractor.IsInitialized;

        public PlanetMarchingCubesExtractionResult LastResult => lastResult;
        public PlanetMarchingCubesSettings Settings => settings;
        public PlanetLabDiagnostic LastDiagnostic => lastDiagnostic;
        public PlanetLabMetricsSnapshot LastSnapshot => lastSnapshot;
        public string LastAction => lastAction;
        public uint LastCandidateChunkCount => lastCandidateChunkCount;
        public uint LastProcessedChunkCount => lastProcessedChunkCount;
        public uint LastProcessedCellCount => lastProcessedCellCount;
        public uint LastTriangleCountAttempted => lastTriangleCountAttempted;
        public uint LastTriangleCountWritten => lastTriangleCountWritten;
        public uint LastVertexCountWritten => lastVertexCountWritten;
        public bool LastOverflow => lastOverflow;
        public bool LastInvalidCase => lastInvalidCase;
        public int LastExtractedCandidateChunkIndex => lastExtractedCandidateChunkIndex;

        public override bool ValidateModule()
        {
            supportsComputeShaders = SystemInfo.supportsComputeShaders;
            settings.EnsureDefaults();

            if (marchingCubesComputeShader == null)
            {
                lastDiagnostic = PlanetLabDiagnostic.Critical(
                    "Marching Cubes ComputeShader is missing",
                    "PlanetMarchingCubesLab cannot extract triangles without PlanetMarchingCubes.compute.",
                    "Assign Assets/Shaders/Compute/PlanetMarchingCubes.compute.",
                    "marchingCubesComputeShader=null");
                lastAction = "Validate Marching Cubes Setup failed.";
                return false;
            }

            if (shapeLab == null)
            {
                lastDiagnostic = PlanetLabDiagnostic.Critical(
                    "Shape Lab reference is missing",
                    "07 consumes the live GPU buffers produced by PlanetGpuShapeLab.",
                    "Assign the PlanetGpuShapeLab from the same PlanetImplementationLab scene.",
                    "shapeLab=null");
                lastAction = "Validate Marching Cubes Setup failed.";
                return false;
            }

            if (!shapeLab.IsShapeGpuInitialized)
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "Shape GPU is not initialized",
                    "07 requires PlanetGpuShapeEvaluator buffers from 06 to be alive before extraction.",
                    "Run Init Shape GPU in PlanetGpuShapeLab first.",
                    "shapeInitialized=false");
                lastAction = "Validate Marching Cubes Setup failed.";
                return false;
            }

            if (!settings.Validate(out string settingsMessage))
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "Marching Cubes settings are invalid",
                    settingsMessage,
                    "Fix the Marching Cubes settings before extracting triangles.",
                    BuildSettingsMetrics());
                lastAction = "Validate Marching Cubes Setup failed.";
                return false;
            }

            if (!supportsComputeShaders)
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "Compute shaders are not supported",
                    "SystemInfo.supportsComputeShaders is false in the current runtime.",
                    "Run this module on a platform/runtime that supports compute shaders.",
                    "supportsComputeShaders=false");
                lastAction = "Validate Marching Cubes Setup unsupported.";
                return false;
            }

            lastDiagnostic = PlanetLabDiagnostic.Ok("Marching Cubes setup is valid", BuildSettingsMetrics());
            lastAction = "Validate Marching Cubes Setup OK.";
            return true;
        }

        public override void InitModule()
        {
            InitMarchingCubesGpu();
        }

        public void ResetDemoSettings()
        {
            ReleaseModule();
            settings = PlanetMarchingCubesSettings.Default();
            lastDiagnostic = PlanetLabDiagnostic.Ok("Marching Cubes demo settings reset", BuildSettingsMetrics());
            lastAction = "Marching Cubes demo settings reset.";
            CaptureMetrics("Reset Marching Cubes Demo Settings", 0);
        }

        public void EnsureTemporaryOutputTriangleCapacity(int minimumTriangleCapacity)
        {
            if (minimumTriangleCapacity > settings.temporaryOutputTriangleCapacity)
            {
                settings.temporaryOutputTriangleCapacity = minimumTriangleCapacity;
            }
        }

        public void InitMarchingCubesGpu()
        {
            stopwatch.Restart();
            ReleaseModule();

            if (!ValidateModule())
            {
                stopwatch.Stop();
                return;
            }

            try
            {
                extractor.Initialize(
                    marchingCubesComputeShader,
                    shapeLab.Recipe,
                    settings,
                    shapeLab.ShapeEvaluator,
                    bufferMode);
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "Marching Cubes GPU initialization failed",
                    exception.Message,
                    "Read the exception and fix the 07 extraction configuration, compute shader contract, or GPU resource setup.",
                    BuildSettingsMetrics());
                lastAction = "Init Marching Cubes GPU failed.";
                CaptureMetrics("Init Marching Cubes GPU Failed", stopwatch.Elapsed.TotalMilliseconds);
                return;
            }

            RegisterBuffers();

            stopwatch.Stop();
            lastDiagnostic = PlanetLabDiagnostic.Ok(
                "Marching Cubes GPU initialized",
                BuildSettingsMetrics() +
                "\n" + extractor.ChunkBuildStats +
                "\nthreadGroupSizeX=" + extractor.ThreadGroupSizeX);
            lastAction = "Init Marching Cubes GPU finished.";
            CaptureMetrics("Init Marching Cubes GPU", stopwatch.Elapsed.TotalMilliseconds);
        }

        public void ExtractPlanetSurface()
        {
            stopwatch.Restart();

            if (!extractor.IsInitialized)
            {
                InitMarchingCubesGpu();
            }

            if (!extractor.IsInitialized)
            {
                stopwatch.Stop();
                return;
            }

            lastResult = extractor.ExtractPlanetSurface();
            lastExtractedCandidateChunkIndex = -1;
            ApplyResultSummary(lastResult);

            stopwatch.Stop();
            lastDiagnostic = PlanetLabDiagnostic.Ok(
                "Cartesian planet surface extracted",
                BuildResultMetrics());
            lastAction = "Extract Cartesian Planet Surface finished.";
            CaptureMetrics("Extract Cartesian Planet Surface", stopwatch.Elapsed.TotalMilliseconds);
        }

        public void ExtractFirstCandidateChunkSurface()
        {
            ExtractCandidateChunkSurface(0);
        }

        public void ExtractCandidateChunkSurface(int candidateChunkIndex)
        {
            stopwatch.Restart();

            if (!extractor.IsInitialized)
            {
                InitMarchingCubesGpu();
            }

            if (!extractor.IsInitialized)
            {
                stopwatch.Stop();
                return;
            }

            if (candidateChunkIndex < 0 || candidateChunkIndex >= extractor.CandidateChunkCount)
            {
                stopwatch.Stop();
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "Candidate chunk extraction blocked",
                    "Requested chunk index is outside the current 07 candidate list.",
                    "Choose a candidateChunkIndex between 0 and " + Mathf.Max(0, extractor.CandidateChunkCount - 1) + ".",
                    "candidateChunkIndex=" + candidateChunkIndex +
                    "\ncandidateChunkCount=" + extractor.CandidateChunkCount);
                lastAction = "Extract Candidate Chunk Surface blocked.";
                CaptureMetrics("Extract Candidate Chunk Surface Blocked", stopwatch.Elapsed.TotalMilliseconds);
                return;
            }

            lastResult = extractor.ExtractCandidateChunkSurface(candidateChunkIndex);
            lastExtractedCandidateChunkIndex = candidateChunkIndex;
            ApplyResultSummary(lastResult);

            stopwatch.Stop();
            string chunkOriginMetrics = extractor.TryGetCandidateChunkOrigin(candidateChunkIndex, out PlanetMarchingCubesChunkOrigin origin)
                ? "\nchunkOrigin=" + origin
                : string.Empty;
            lastDiagnostic = PlanetLabDiagnostic.Ok(
                "Single candidate chunk surface extracted",
                BuildResultMetrics() +
                "\ncandidateChunkIndex=" + candidateChunkIndex +
                chunkOriginMetrics);
            lastAction = "Extract Candidate Chunk Surface finished.";
            CaptureMetrics("Extract Candidate Chunk Surface", stopwatch.Elapsed.TotalMilliseconds);
        }

        public void RunMarchingCubesSmokeTest()
        {
            InitMarchingCubesGpu();
            ExtractPlanetSurface();
        }

        public override void RunModuleTest()
        {
            RunMarchingCubesSmokeTest();
        }

        public override void RunModuleStress()
        {
            RunMarchingCubesSmokeTest();
        }

        public override void ReleaseModule()
        {
            stopwatch.Restart();
            extractor.Release();
            lastResult = PlanetMarchingCubesExtractionResult.Empty;
            lastCandidateChunkCount = 0u;
            lastProcessedChunkCount = 0u;
            lastProcessedCellCount = 0u;
            lastTriangleCountAttempted = 0u;
            lastTriangleCountWritten = 0u;
            lastVertexCountWritten = 0u;
            lastOverflow = false;
            lastInvalidCase = false;
            lastExtractedCandidateChunkIndex = -1;
            MarkReleased(ref triTableBufferResourceId);
            MarkReleased(ref edgeTableBufferResourceId);
            MarkReleased(ref stateBufferResourceId);
            MarkReleased(ref vertexBufferResourceId);
            MarkReleased(ref chunkOriginBufferResourceId);

            if (resourceRegistry != null)
            {
                resourceRegistry.RecalculateLiveTotals();
            }

            stopwatch.Stop();
            CaptureMetrics("Release Marching Cubes GPU", stopwatch.Elapsed.TotalMilliseconds);
        }

        public override PlanetLabMetricsSnapshot CaptureMetrics()
        {
            CaptureMetrics("Capture Marching Cubes Metrics", 0);
            return lastSnapshot;
        }

        private void ApplyResultSummary(PlanetMarchingCubesExtractionResult result)
        {
            PlanetMarchingCubesState state = result.State;
            lastCandidateChunkCount = state.chunkCountCandidate;
            lastProcessedChunkCount = state.chunkCountProcessed;
            lastProcessedCellCount = state.cellCountProcessed;
            lastTriangleCountAttempted = state.triangleCountAttempted;
            lastTriangleCountWritten = state.triangleCountWritten;
            lastVertexCountWritten = state.vertexCountWritten;
            lastOverflow = state.overflowFlag != 0u;
            lastInvalidCase = state.invalidCaseFlag != 0u;
        }

        private void RegisterBuffers()
        {
            chunkOriginBufferResourceId = RegisterResource(extractor.ChunkOriginBuffer);
            vertexBufferResourceId = RegisterResource(extractor.VertexBuffer);
            stateBufferResourceId = RegisterResource(extractor.StateBuffer);
            edgeTableBufferResourceId = RegisterResource(extractor.EdgeTableBuffer);
            triTableBufferResourceId = RegisterResource(extractor.TriTableBuffer);
        }

        private int RegisterResource(PlanetGpuBufferHandle handle)
        {
            if (resourceRegistry == null || handle == null)
            {
                return 0;
            }

            PlanetLabResourceType resourceType = handle.BufferMode == PlanetGpuBufferMode.GraphicsBuffer
                ? PlanetLabResourceType.GraphicsBuffer
                : PlanetLabResourceType.ComputeBuffer;
            return resourceRegistry.RegisterResource(
                handle.DebugName,
                resourceType,
                OwnerName,
                handle.EstimatedBytes,
                handle.ElementCount,
                handle.Stride);
        }

        private void MarkReleased(ref int resourceId)
        {
            if (resourceRegistry != null && resourceId != 0)
            {
                resourceRegistry.MarkReleased(resourceId);
            }

            resourceId = 0;
        }

        private void CaptureMetrics(string operationName, double operationMs)
        {
            if (resourceRegistry != null)
            {
                resourceRegistry.RecalculateLiveTotals();
            }

            lastSnapshot = PlanetLabMetricsSnapshot.Capture(operationName, resourceRegistry, operationMs);
            if (string.IsNullOrEmpty(lastDiagnostic.title))
            {
                lastDiagnostic = lastSnapshot.diagnostic;
            }
        }

        private string BuildSettingsMetrics()
        {
            return settings.ToString();
        }

        private string BuildResultMetrics()
        {
            return "chunkCountCandidate=" + lastCandidateChunkCount +
                   "\nchunkCountProcessed=" + lastProcessedChunkCount +
                   "\ncellCountProcessed=" + lastProcessedCellCount +
                   "\ntriangleCountAttempted=" + lastTriangleCountAttempted +
                   "\ntriangleCountWritten=" + lastTriangleCountWritten +
                   "\nvertexCountWritten=" + lastVertexCountWritten +
                   "\nlastExtractedCandidateChunkIndex=" + lastExtractedCandidateChunkIndex +
                   "\nreadbackVertexCount=" + lastResult.VertexCount +
                   "\noverflow=" + lastOverflow +
                   "\ninvalidCase=" + lastInvalidCase +
                   "\n" + extractor.ChunkBuildStats +
                   "\n" + BuildSettingsMetrics();
        }

        private void OnDisable()
        {
            ReleaseModule();
        }
    }
}
