using System;
using System.Diagnostics;
using MarchingCubesPlanet.Compute;
using MarchingCubesPlanet.Coordinates;
using MarchingCubesPlanet.Shape;
using UnityEngine;

namespace MarchingCubesPlanet.Lab
{
    public sealed class PlanetGpuShapeLab : PlanetLabModule
    {
        private const string OwnerName = "PlanetGpuShapeLab";

        [Header("References")]
        [SerializeField] private ComputeShader shapeComputeShader;
        [SerializeField] private PlanetLabResourceRegistry resourceRegistry;

        [Header("Recipe")]
        [SerializeField] private PlanetRecipe recipe = PlanetRecipe.Default();

        [Header("Debug Samples")]
        [SerializeField] private PlanetGpuBufferMode bufferMode = PlanetGpuBufferMode.GraphicsBuffer;
        [SerializeField] private int debugSampleCount = 32;

        [Header("State")]
        [SerializeField] private bool supportsComputeShaders;
        [SerializeField] private PlanetGpuShapeBuildSummary lastBuildSummary;
        [SerializeField] private int lastSampleCount;
        [SerializeField] private float lastMinDensity;
        [SerializeField] private float lastMaxDensity;
        [SerializeField] private string lastAction;
        [SerializeField] private PlanetLabMetricsSnapshot lastSnapshot;
        [SerializeField] private PlanetLabDiagnostic lastDiagnostic;

        private readonly Stopwatch stopwatch = new Stopwatch();
        private readonly PlanetGpuShapeEvaluator evaluator = new PlanetGpuShapeEvaluator();

        private PlanetGpuShapeCell[] cells;
        private Vector4[] samplePositions;
        private Vector4[] sampleResults;

        private int parameterBufferResourceId;
        private int cellBufferResourceId;
        private int sampleInputBufferResourceId;
        private int sampleOutputBufferResourceId;

        public override string ModuleName => "Planet GPU Shape Lab";
        public override bool HasLiveResources => evaluator.IsInitialized ||
                                                 evaluator.SampleInputBuffer != null && evaluator.SampleInputBuffer.IsAlive ||
                                                 evaluator.SampleOutputBuffer != null && evaluator.SampleOutputBuffer.IsAlive;

        public PlanetRecipe Recipe => recipe;
        public PlanetGpuShapeBuildSummary LastBuildSummary => lastBuildSummary;
        public PlanetLabDiagnostic LastDiagnostic => lastDiagnostic;
        public PlanetLabMetricsSnapshot LastSnapshot => lastSnapshot;
        public string LastAction => lastAction;
        public int LastSampleCount => lastSampleCount;
        public float LastMinDensity => lastMinDensity;
        public float LastMaxDensity => lastMaxDensity;
        public PlanetGpuShapeEvaluator ShapeEvaluator => evaluator;
        public bool IsShapeGpuInitialized => evaluator.IsInitialized;

        public override bool ValidateModule()
        {
            supportsComputeShaders = SystemInfo.supportsComputeShaders;

            if (shapeComputeShader == null)
            {
                lastDiagnostic = PlanetLabDiagnostic.Critical(
                    "Shape ComputeShader is missing",
                    "PlanetGpuShapeLab cannot evaluate density without PlanetShapeDensity.compute.",
                    "Assign Assets/Shaders/Compute/PlanetShapeDensity.compute.",
                    "shapeComputeShader=null");
                lastAction = "Validate Shape Setup failed.";
                return false;
            }

            if (!PlanetRecipeValidator.Validate(in recipe, out string recipeMessage))
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "PlanetRecipe shape values are invalid",
                    recipeMessage,
                    "Fix PlanetRecipe values before uploading shape data.",
                    BuildRecipeMetrics());
                lastAction = "Validate Shape Setup failed.";
                return false;
            }

            if (debugSampleCount <= 0)
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "Debug sample count is invalid",
                    "debugSampleCount must be greater than zero.",
                    "Use a small positive sample count for shape smoke tests.",
                    "debugSampleCount=" + debugSampleCount);
                lastAction = "Validate Shape Setup failed.";
                return false;
            }

            if (!supportsComputeShaders)
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "Compute shaders are not supported",
                    "SystemInfo.supportsComputeShaders is false in the current runtime.",
                    "Run this module on a platform/runtime that supports compute shaders.",
                    "supportsComputeShaders=false");
                lastAction = "Validate Shape Setup unsupported.";
                return false;
            }

            lastDiagnostic = PlanetLabDiagnostic.Ok("Planet GPU Shape setup is valid", BuildRecipeMetrics());
            lastAction = "Validate Shape Setup OK.";
            return true;
        }

        public override void InitModule()
        {
            InitShapeGpu();
        }

        public void ResetDemoRecipe()
        {
            ReleaseModule();
            recipe = PlanetRecipe.Default();
            debugSampleCount = 32;
            lastBuildSummary = default;
            lastDiagnostic = PlanetLabDiagnostic.Ok("Shape demo recipe reset", BuildRecipeMetrics());
            lastAction = "Shape demo recipe reset.";
            CaptureMetrics("Reset Shape Demo Recipe", 0);
        }

        public void SetRecipe(in PlanetRecipe value)
        {
            if (!PlanetRecipeValidator.Validate(in value, out string recipeMessage))
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "PlanetRecipe shape values are invalid",
                    recipeMessage,
                    "Fix the source PlanetRecipe before uploading shape data.",
                    BuildRecipeMetrics());
                lastAction = "Set Shape Recipe failed.";
                return;
            }

            ReleaseModule();
            recipe = value;
            lastBuildSummary = default;
            lastDiagnostic = PlanetLabDiagnostic.Ok("Shape recipe assigned", BuildRecipeMetrics());
            lastAction = "Set Shape Recipe finished.";
            CaptureMetrics("Set Shape Recipe", 0);
        }

        public void GenerateVoronoiCells()
        {
            stopwatch.Restart();
            if (!ValidateModule())
            {
                stopwatch.Stop();
                return;
            }

            EnsureCellBuffer();
            lastBuildSummary = PlanetGpuShapeCellBuilder.Build(in recipe, cells);
            stopwatch.Stop();
            lastDiagnostic = PlanetLabDiagnostic.Ok("Voronoi shape cells generated", lastBuildSummary.ToString());
            lastAction = "Generate Voronoi Cells finished.";
            CaptureMetrics("Generate Voronoi Cells", stopwatch.Elapsed.TotalMilliseconds);
        }

        public void InitShapeGpu()
        {
            stopwatch.Restart();
            ReleaseModule();

            if (!ValidateModule())
            {
                stopwatch.Stop();
                return;
            }

            EnsureCellBuffer();
            lastBuildSummary = PlanetGpuShapeCellBuilder.Build(in recipe, cells);
            evaluator.Initialize(shapeComputeShader, in recipe, cells, bufferMode);
            RegisterCoreBuffers();

            stopwatch.Stop();
            lastDiagnostic = PlanetLabDiagnostic.Ok(
                "Planet GPU shape initialized",
                lastBuildSummary + "\nthreadGroupSizeX=" + evaluator.ThreadGroupSizeX);
            lastAction = "Init Shape GPU finished.";
            CaptureMetrics("Init Shape GPU", stopwatch.Elapsed.TotalMilliseconds);
        }

        public void EvaluateDebugSamples()
        {
            stopwatch.Restart();

            if (!evaluator.IsInitialized)
            {
                InitShapeGpu();
            }

            if (!evaluator.IsInitialized)
            {
                stopwatch.Stop();
                return;
            }

            EnsureSampleArrays();
            FillDebugSamplePositions();
            MarkSampleResourcesIfResizeNeeded(samplePositions.Length);
            evaluator.EvaluateDensitySamples(samplePositions, sampleResults);
            RegisterSampleBuffers();
            ApplySampleSummary();

            stopwatch.Stop();
            lastDiagnostic = PlanetLabDiagnostic.Ok(
                "Shape debug samples evaluated",
                "sampleCount=" + lastSampleCount +
                "\nminDensity=" + lastMinDensity +
                "\nmaxDensity=" + lastMaxDensity +
                "\nfirstDensity=" + sampleResults[0].x +
                "\nlastDensity=" + sampleResults[lastSampleCount - 1].x);
            lastAction = "Evaluate Debug Samples finished.";
            CaptureMetrics("Evaluate Debug Samples", stopwatch.Elapsed.TotalMilliseconds);
        }

        public void RunShapeSmokeTest()
        {
            InitShapeGpu();
            EvaluateDebugSamples();
        }

        public void RunStressLow()
        {
            RunStress("Shape Stress Low", 3, 16);
        }

        public void RunStressMedium()
        {
            RunStress("Shape Stress Medium", 10, 64);
        }

        public override void RunModuleTest()
        {
            RunShapeSmokeTest();
        }

        public override void RunModuleStress()
        {
            RunStressLow();
        }

        public override void ReleaseModule()
        {
            stopwatch.Restart();
            evaluator.Release();
            cells = null;
            samplePositions = null;
            sampleResults = null;
            lastSampleCount = 0;
            lastMinDensity = 0f;
            lastMaxDensity = 0f;
            MarkReleased(ref sampleOutputBufferResourceId);
            MarkReleased(ref sampleInputBufferResourceId);
            MarkReleased(ref cellBufferResourceId);
            MarkReleased(ref parameterBufferResourceId);

            if (resourceRegistry != null)
            {
                resourceRegistry.RecalculateLiveTotals();
            }

            stopwatch.Stop();
            CaptureMetrics("Release Shape GPU", stopwatch.Elapsed.TotalMilliseconds);
        }

        public override PlanetLabMetricsSnapshot CaptureMetrics()
        {
            CaptureMetrics("Capture Shape Metrics", 0);
            return lastSnapshot;
        }

        private void RunStress(string operationName, int cycles, int requestedSampleCount)
        {
            int originalSampleCount = debugSampleCount;
            Stopwatch stressStopwatch = Stopwatch.StartNew();

            try
            {
                debugSampleCount = requestedSampleCount;
                for (int i = 0; i < cycles; i++)
                {
                    InitShapeGpu();
                    EvaluateDebugSamples();
                    ReleaseModule();
                }
            }
            finally
            {
                debugSampleCount = originalSampleCount;
                ReleaseModule();
            }

            stressStopwatch.Stop();
            CaptureMetrics(operationName, stressStopwatch.Elapsed.TotalMilliseconds);
            lastAction = operationName + " finished.";
        }

        private void EnsureCellBuffer()
        {
            if (cells == null || cells.Length != recipe.VoronoiDivision)
            {
                cells = new PlanetGpuShapeCell[recipe.VoronoiDivision];
            }
        }

        private void EnsureSampleArrays()
        {
            int count = Mathf.Max(1, debugSampleCount);
            if (samplePositions == null || samplePositions.Length != count)
            {
                samplePositions = new Vector4[count];
            }

            if (sampleResults == null || sampleResults.Length != count)
            {
                sampleResults = new Vector4[count];
            }
        }

        private void FillDebugSamplePositions()
        {
            int count = samplePositions.Length;
            float start = -recipe.GridRadius * 1.25f;
            float end = recipe.GridRadius * 1.25f;

            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                float x = Mathf.Lerp(start, end, t);
                samplePositions[i] = new Vector4(x, 0f, 0f, 1f);
            }
        }

        private void ApplySampleSummary()
        {
            lastSampleCount = samplePositions.Length;
            lastMinDensity = float.PositiveInfinity;
            lastMaxDensity = float.NegativeInfinity;

            for (int i = 0; i < lastSampleCount; i++)
            {
                float density = sampleResults[i].x;
                lastMinDensity = Mathf.Min(lastMinDensity, density);
                lastMaxDensity = Mathf.Max(lastMaxDensity, density);
            }
        }

        private void RegisterCoreBuffers()
        {
            parameterBufferResourceId = RegisterResource(evaluator.ParameterBuffer);
            cellBufferResourceId = RegisterResource(evaluator.CellBuffer);
        }

        private void RegisterSampleBuffers()
        {
            if (sampleInputBufferResourceId == 0)
            {
                sampleInputBufferResourceId = RegisterResource(evaluator.SampleInputBuffer);
            }

            if (sampleOutputBufferResourceId == 0)
            {
                sampleOutputBufferResourceId = RegisterResource(evaluator.SampleOutputBuffer);
            }
        }

        private void MarkSampleResourcesIfResizeNeeded(int requestedSampleCount)
        {
            if (evaluator.SampleInputBuffer == null || evaluator.SampleOutputBuffer == null)
            {
                return;
            }

            if (evaluator.SampleInputBuffer.ElementCount == requestedSampleCount &&
                evaluator.SampleOutputBuffer.ElementCount == requestedSampleCount)
            {
                return;
            }

            MarkReleased(ref sampleInputBufferResourceId);
            MarkReleased(ref sampleOutputBufferResourceId);
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

        private string BuildRecipeMetrics()
        {
            return "GridRadius=" + recipe.GridRadius +
                   "\nSeed=" + recipe.Seed +
                   "\nIsoLevel=" + recipe.IsoLevel +
                   "\nVoronoiDivision=" + recipe.VoronoiDivision +
                   "\nContinentCells=" + recipe.ContinentCells +
                   "\nSurfaceNoiseAmplitude=" + recipe.SurfaceNoiseAmplitude +
                   "\nSurfaceNoiseFrequency=" + recipe.SurfaceNoiseFrequency;
        }

        private void OnDisable()
        {
            ReleaseModule();
        }
    }
}
