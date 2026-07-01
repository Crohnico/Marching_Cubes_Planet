using System;
using System.Diagnostics;
using System.IO;
using MarchingCubesPlanet.Compute;
using UnityEngine;

namespace MarchingCubesPlanet.Lab
{
    public sealed class PlanetComputeShaderRunnerLab : PlanetLabModule
    {
        private const string KernelName = "CS_DebugWrite";
        private const string OwnerName = "PlanetComputeShaderRunnerLab";
        private const string EvidenceDirectory = "Resources/PlanetLabReports";
        private const string LatestManualStressEvidenceFileName = "PlanetComputeShaderLab_ManualStressEvidence_Latest.json";

        [Header("References")]
        [SerializeField] private ComputeShader computeShader;
        [SerializeField] private PlanetLabResourceRegistry resourceRegistry;

        [Header("Settings")]
        [SerializeField] private PlanetGpuBufferMode bufferMode = PlanetGpuBufferMode.GraphicsBuffer;
        [SerializeField] private PlanetComputeLabSettings settings = PlanetComputeLabSettings.Default();
        [SerializeField] private uint seed = 12345;

        [Header("State")]
        [SerializeField] private bool supportsComputeShaders;
        [SerializeField] private bool supportsInstancing;
        [SerializeField] private uint kernelThreadGroupSizeX;
        [SerializeField] private string lastAction;
        [SerializeField] private string lastManualStressEvidencePath;
        [SerializeField] private PlanetLabMetricsSnapshot lastSnapshot;
        [SerializeField] private PlanetLabDiagnostic lastDiagnostic;

        private readonly Stopwatch stopwatch = new Stopwatch();
        private readonly PlanetComputeShaderRunner runner = new PlanetComputeShaderRunner();

        private PlanetGpuBufferHandle activeBuffer;
        private RenderTexture outputTexture;
        private uint dispatchIndex;

        private int bufferResourceId;
        private int outputTextureResourceId;

        public override string ModuleName => "Compute Shader Lab";
        public override bool HasLiveResources => activeBuffer != null && activeBuffer.IsAlive ||
                                                 outputTexture != null;

        public PlanetLabDiagnostic LastDiagnostic => lastDiagnostic;
        public PlanetLabMetricsSnapshot LastSnapshot => lastSnapshot;
        public string LastAction => lastAction;
        public string LastManualStressEvidencePath => lastManualStressEvidencePath;
        public PlanetComputeLabSettings Settings => settings;
        public uint KernelThreadGroupSizeX => kernelThreadGroupSizeX;
        public RenderTexture OutputTexture => outputTexture;

        public override bool ValidateModule()
        {
            supportsComputeShaders = SystemInfo.supportsComputeShaders;
            supportsInstancing = SystemInfo.supportsInstancing;

            if (computeShader == null)
            {
                lastDiagnostic = PlanetLabDiagnostic.Critical(
                    "ComputeShader is missing",
                    "PlanetComputeShaderRunnerLab cannot execute the debug kernel without a ComputeShader asset.",
                    "Assign Assets/Shaders/Compute/PlanetComputeDebug.compute.",
                    "computeShader=null");
                lastAction = "Validate Module failed.";
                return false;
            }

            if (!settings.IsValid(out string settingsMessage))
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "Compute Lab settings are invalid",
                    settingsMessage,
                    "Fix the settings in the Inspector before running the module.",
                    "bufferElementCount=" + settings.bufferElementCount);
                lastAction = "Validate Module failed.";
                return false;
            }

            if (!supportsComputeShaders)
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "Compute shaders are not supported",
                    "SystemInfo.supportsComputeShaders is false in the current runtime.",
                    "Run this module on a platform/runtime that supports compute shaders.",
                    "supportsComputeShaders=false");
                lastAction = "Validate Module unsupported.";
                return false;
            }

            runner.Configure(computeShader, KernelName);
            kernelThreadGroupSizeX = runner.ThreadGroupSizeX;

            lastDiagnostic = PlanetLabDiagnostic.Ok(
                "Compute Shader Lab is valid",
                "supportsComputeShaders=" + supportsComputeShaders +
                "\nsupportsInstancing=" + supportsInstancing +
                "\nthreadGroupSizeX=" + kernelThreadGroupSizeX);
            lastAction = "Validate Module OK.";
            return true;
        }

        public override void InitModule()
        {
            CreateResources(bufferMode, settings);
        }

        public void CreateGraphicsBufferTest()
        {
            CreateResources(PlanetGpuBufferMode.GraphicsBuffer, settings);
        }

        public void CreateComputeBufferTest()
        {
            CreateResources(PlanetGpuBufferMode.ComputeBuffer, settings);
        }

        public void CreateRenderTextureDebugTest()
        {
            stopwatch.Restart();
            ReleaseModule();

            if (!ValidateModule())
            {
                stopwatch.Stop();
                return;
            }

            outputTexture = CreateOutputTexture(settings);
            outputTextureResourceId = RegisterResource(
                "Compute Debug RenderTexture",
                PlanetLabResourceType.RenderTexture,
                settings.EstimatedTextureBytes,
                settings.outputTextureWidth * settings.outputTextureHeight,
                PlanetComputeLabSettings.Argb32BytesPerPixel);

            stopwatch.Stop();
            CaptureMetrics("Create RenderTexture Debug", stopwatch.Elapsed.TotalMilliseconds);
        }

        public void DispatchOnce()
        {
            DispatchRepeated(1, "Dispatch Once");
        }

        public void Dispatch100()
        {
            DispatchRepeated(100, "Dispatch 100x");
        }

        public void RunGraphicsBufferStress()
        {
            RunStress("GraphicsBuffer Stress", PlanetGpuBufferMode.GraphicsBuffer, settings, 10);
        }

        public void RunComputeBufferStress()
        {
            RunStress("ComputeBuffer Stress", PlanetGpuBufferMode.ComputeBuffer, settings, 10);
        }

        public void RunStressLow()
        {
            RunStress("Stress Low", PlanetGpuBufferMode.GraphicsBuffer, StressLow(), 10);
        }

        public void RunStressMedium()
        {
            RunStress("Stress Medium", PlanetGpuBufferMode.GraphicsBuffer, StressMedium(), 25);
        }

        public void RunStressHigh()
        {
            RunStress("Stress High", PlanetGpuBufferMode.GraphicsBuffer, StressHigh(), 50);
        }

        public void RunStressVeryHigh()
        {
            RunStress("Stress VeryHigh", PlanetGpuBufferMode.GraphicsBuffer, StressVeryHigh(), 75);
        }

        public void RunStressExtreme()
        {
            RunStress("Stress Extreme", PlanetGpuBufferMode.GraphicsBuffer, StressExtreme(), 100);
        }

        public void RunComparison()
        {
            Stopwatch comparisonStopwatch = Stopwatch.StartNew();

            try
            {
                CreateResources(PlanetGpuBufferMode.GraphicsBuffer, settings);
                DispatchRepeated(settings.dispatchRepeatCount, "GraphicsBuffer Comparison");
                ReleaseModule();

                CreateResources(PlanetGpuBufferMode.ComputeBuffer, settings);
                DispatchRepeated(settings.dispatchRepeatCount, "ComputeBuffer Comparison");
            }
            finally
            {
                ReleaseModule();
            }

            comparisonStopwatch.Stop();
            CaptureMetrics("Run Comparison", comparisonStopwatch.Elapsed.TotalMilliseconds);
            lastAction = "Run Comparison finished.";
        }

        public void ResetModuleState()
        {
            ReleaseModule();
            dispatchIndex = 0;
            CaptureMetrics("Reset Module State", 0);
            lastAction = "Compute module state reset.";
        }

        public string SaveManualStressEvidence(
            string actionName,
            bool executedWithoutException,
            string exception)
        {
            ManualStressEvidenceReport report = new ManualStressEvidenceReport
            {
                sceneName = gameObject.scene.name,
                moduleName = ModuleName,
                createdAtUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                supportsComputeShaders = SystemInfo.supportsComputeShaders,
                supportsInstancing = SystemInfo.supportsInstancing,
                kernelThreadGroupSizeX = kernelThreadGroupSizeX,
                record = CaptureManualStressEvidenceRecord(actionName, executedWithoutException, exception)
            };

            string directory = Path.Combine(Application.dataPath, EvidenceDirectory);
            Directory.CreateDirectory(directory);

            string safeActionName = MakeSafeFilePart(actionName);
            string historyFileName = "PlanetComputeShaderLab_ManualStressEvidence_" +
                                    DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") +
                                    "_" +
                                    safeActionName +
                                    ".json";

            string historyPath = Path.Combine(directory, historyFileName);
            string latestPath = Path.Combine(directory, LatestManualStressEvidenceFileName);
            string json = JsonUtility.ToJson(report, true);

            File.WriteAllText(historyPath, json);
            File.WriteAllText(latestPath, json);

            lastManualStressEvidencePath = historyPath;
            lastAction = "Manual stress evidence saved.";
            return historyPath;
        }

        public override void ReleaseModule()
        {
            stopwatch.Restart();

            if (activeBuffer != null)
            {
                activeBuffer.Release();
                activeBuffer = null;
                MarkReleased(ref bufferResourceId);
            }

            if (outputTexture != null)
            {
                outputTexture.Release();
                DestroyUnityObject(outputTexture);
                outputTexture = null;
                MarkReleased(ref outputTextureResourceId);
            }

            if (resourceRegistry != null)
            {
                resourceRegistry.RecalculateLiveTotals();
            }

            stopwatch.Stop();
            CaptureMetrics("Release Module", stopwatch.Elapsed.TotalMilliseconds);
        }

        public override PlanetLabMetricsSnapshot CaptureMetrics()
        {
            CaptureMetrics("Capture Module Metrics", 0);
            return lastSnapshot;
        }

        public override void RunModuleTest()
        {
            InitModule();
            DispatchOnce();
            CaptureMetrics("Run Module Test", 0);
        }

        public override void RunModuleStress()
        {
            RunStressLow();
        }

        private void CreateResources(PlanetGpuBufferMode mode, PlanetComputeLabSettings requestedSettings)
        {
            stopwatch.Restart();
            ReleaseModule();

            if (!ValidateModule())
            {
                stopwatch.Stop();
                return;
            }

            if (!requestedSettings.IsValid(out string settingsMessage))
            {
                stopwatch.Stop();
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "Compute Lab settings are invalid",
                    settingsMessage,
                    "Fix the settings before creating compute resources.",
                    "bufferElementCount=" + requestedSettings.bufferElementCount);
                lastAction = "Create resources aborted.";
                return;
            }

            bufferMode = mode;
            activeBuffer = mode == PlanetGpuBufferMode.GraphicsBuffer
                ? PlanetGpuBufferHandle.CreateGraphicsBuffer("Compute Debug GraphicsBuffer", requestedSettings.bufferElementCount, PlanetComputeLabSettings.Float4Stride)
                : PlanetGpuBufferHandle.CreateComputeBuffer("Compute Debug ComputeBuffer", requestedSettings.bufferElementCount, PlanetComputeLabSettings.Float4Stride);

            PlanetLabResourceType bufferResourceType = mode == PlanetGpuBufferMode.GraphicsBuffer
                ? PlanetLabResourceType.GraphicsBuffer
                : PlanetLabResourceType.ComputeBuffer;

            bufferResourceId = RegisterResource(
                activeBuffer.DebugName,
                bufferResourceType,
                activeBuffer.EstimatedBytes,
                activeBuffer.ElementCount,
                activeBuffer.Stride);

            outputTexture = CreateOutputTexture(requestedSettings);
            outputTextureResourceId = RegisterResource(
                "Compute Debug RenderTexture",
                PlanetLabResourceType.RenderTexture,
                requestedSettings.EstimatedTextureBytes,
                requestedSettings.outputTextureWidth * requestedSettings.outputTextureHeight,
                PlanetComputeLabSettings.Argb32BytesPerPixel);

            stopwatch.Stop();
            CaptureMetrics("Create " + mode + " Test", stopwatch.Elapsed.TotalMilliseconds);
        }

        private void DispatchRepeated(int repeatCount, string operationName)
        {
            if (activeBuffer == null || !activeBuffer.IsAlive || outputTexture == null)
            {
                CreateResources(bufferMode, settings);
            }

            if (activeBuffer == null || outputTexture == null)
            {
                return;
            }

            stopwatch.Restart();

            for (int i = 0; i < repeatCount; i++)
            {
                runner.DispatchDebugWrite(
                    activeBuffer,
                    outputTexture,
                    activeBuffer.ElementCount,
                    outputTexture.width,
                    outputTexture.height,
                    seed,
                    dispatchIndex++);
            }

            stopwatch.Stop();
            CaptureMetrics(operationName, stopwatch.Elapsed.TotalMilliseconds);
        }

        private void RunStress(
            string stressName,
            PlanetGpuBufferMode mode,
            PlanetComputeLabSettings stressSettings,
            int cycleCount)
        {
            if (cycleCount > stressSettings.maxCycleCount)
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "Compute stress exceeds cycle limit",
                    "The requested stress cycle count is greater than maxCycleCount.",
                    "Lower the stress preset or increase the Lab limit deliberately.",
                    "cycleCount=" + cycleCount + "\nmaxCycleCount=" + stressSettings.maxCycleCount);
                lastAction = stressName + " aborted.";
                return;
            }

            Stopwatch stressStopwatch = Stopwatch.StartNew();

            try
            {
                for (int i = 0; i < cycleCount; i++)
                {
                    CreateResources(mode, stressSettings);
                    DispatchRepeated(stressSettings.dispatchRepeatCount, stressName + " Dispatch");

                    if (stressSettings.releaseBetweenCycles)
                    {
                        ReleaseModule();
                    }

                    if (stressSettings.captureMetricsEachCycle)
                    {
                        CaptureMetrics(stressName + " cycle " + i, 0);
                    }
                }
            }
            finally
            {
                ReleaseModule();
            }

            stressStopwatch.Stop();
            CaptureMetrics(stressName, stressStopwatch.Elapsed.TotalMilliseconds);
            lastAction = stressName + " finished.";
        }

        private RenderTexture CreateOutputTexture(PlanetComputeLabSettings requestedSettings)
        {
            RenderTexture texture = new RenderTexture(
                requestedSettings.outputTextureWidth,
                requestedSettings.outputTextureHeight,
                0,
                RenderTextureFormat.ARGB32)
            {
                name = "Compute Debug OutputTexture",
                enableRandomWrite = true,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };

            texture.Create();
            return texture;
        }

        private int RegisterResource(
            string resourceName,
            PlanetLabResourceType resourceType,
            long estimatedBytes,
            int elementCount,
            int stride)
        {
            if (resourceRegistry == null)
            {
                return 0;
            }

            return resourceRegistry.RegisterResource(resourceName, resourceType, OwnerName, estimatedBytes, elementCount, stride);
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
            lastDiagnostic = lastSnapshot.diagnostic;
            lastAction = operationName + " captured.";
        }

        private ManualStressEvidenceRecord CaptureManualStressEvidenceRecord(
            string actionName,
            bool executedWithoutException,
            string exception)
        {
            PlanetLabMetricsSnapshot snapshot = lastSnapshot;
            PlanetLabDiagnostic diagnostic = lastDiagnostic;

            return new ManualStressEvidenceRecord
            {
                actionName = actionName,
                lastAction = lastAction,
                executedWithoutException = executedWithoutException,
                exception = exception,
                operationName = snapshot.operationName,
                frame = snapshot.frame,
                realtimeSinceStartup = snapshot.realtimeSinceStartup,
                frameDeltaMs = snapshot.frameDeltaMs,
                fpsApprox = snapshot.fpsApprox,
                managedHeapBytes = snapshot.managedHeapBytes,
                ownedCpuEstimatedBytes = snapshot.ownedCpuEstimatedBytes,
                ownedGpuEstimatedBytes = snapshot.ownedGpuEstimatedBytes,
                liveGraphicsBuffers = snapshot.liveGraphicsBuffers,
                liveComputeBuffers = snapshot.liveComputeBuffers,
                liveRenderTextures = snapshot.liveRenderTextures,
                liveRuntimeMeshes = snapshot.liveRuntimeMeshes,
                liveRuntimeTextures = snapshot.liveRuntimeTextures,
                liveRuntimeMaterials = snapshot.liveRuntimeMaterials,
                liveResourceCount = snapshot.liveResourceCount,
                operationMs = snapshot.operationMs,
                severity = diagnostic.severity.ToString(),
                diagnosticTitle = diagnostic.title,
                probableCause = diagnostic.probableCause,
                recommendedAction = diagnostic.recommendedAction,
                relatedMetrics = diagnostic.relatedMetrics
            };
        }

        private static string MakeSafeFilePart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "ManualStress";
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            string compact = value.Replace(" ", string.Empty);

            for (int i = 0; i < invalidChars.Length; i++)
            {
                compact = compact.Replace(invalidChars[i].ToString(), string.Empty);
            }

            return string.IsNullOrWhiteSpace(compact) ? "ManualStress" : compact;
        }

        private static void DestroyUnityObject(UnityEngine.Object unityObject)
        {
            if (unityObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(unityObject);
                return;
            }

            DestroyImmediate(unityObject);
        }

        private void OnDisable()
        {
            ReleaseModule();
        }

        private static PlanetComputeLabSettings StressLow()
        {
            return PlanetComputeLabSettings.StressPreset(16 * 1024, 256, 256, 1, 2_000_000, 2048, 100, 100);
        }

        private static PlanetComputeLabSettings StressMedium()
        {
            return PlanetComputeLabSettings.StressPreset(128 * 1024, 512, 512, 10, 2_000_000, 2048, 100, 100);
        }

        private static PlanetComputeLabSettings StressHigh()
        {
            return PlanetComputeLabSettings.StressPreset(512 * 1024, 1024, 1024, 50, 2_000_000, 2048, 100, 100);
        }

        private static PlanetComputeLabSettings StressVeryHigh()
        {
            return PlanetComputeLabSettings.StressPreset(1_000_000, 1024, 1024, 75, 2_000_000, 2048, 100, 100);
        }

        private static PlanetComputeLabSettings StressExtreme()
        {
            return PlanetComputeLabSettings.StressPreset(2_000_000, 2048, 2048, 100, 2_000_000, 2048, 100, 100);
        }

        [Serializable]
        private sealed class ManualStressEvidenceReport
        {
            public string sceneName;
            public string moduleName;
            public string createdAtUtc;
            public string unityVersion;
            public string platform;
            public bool supportsComputeShaders;
            public bool supportsInstancing;
            public uint kernelThreadGroupSizeX;
            public ManualStressEvidenceRecord record;
        }

        [Serializable]
        private sealed class ManualStressEvidenceRecord
        {
            public string actionName;
            public string lastAction;
            public bool executedWithoutException;
            public string exception;
            public string operationName;
            public int frame;
            public float realtimeSinceStartup;
            public float frameDeltaMs;
            public float fpsApprox;
            public long managedHeapBytes;
            public long ownedCpuEstimatedBytes;
            public long ownedGpuEstimatedBytes;
            public int liveGraphicsBuffers;
            public int liveComputeBuffers;
            public int liveRenderTextures;
            public int liveRuntimeMeshes;
            public int liveRuntimeTextures;
            public int liveRuntimeMaterials;
            public int liveResourceCount;
            public double operationMs;
            public string severity;
            public string diagnosticTitle;
            public string probableCause;
            public string recommendedAction;
            public string relatedMetrics;
        }
    }
}
