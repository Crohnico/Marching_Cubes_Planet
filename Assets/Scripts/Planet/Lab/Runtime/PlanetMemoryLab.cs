using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace MarchingCubesPlanet.Lab
{
    public sealed class PlanetMemoryLab : PlanetLabModule
    {
        private const string OwnerName = "PlanetMemoryLab";

        [Header("References")]
        [SerializeField] private PlanetLabResourceRegistry resourceRegistry;
        [SerializeField] private PlanetImplementationLabController labController;

        [Header("Budget")]
        [SerializeField] private PlanetMemoryBudget budgetDefaults = PlanetMemoryBudget.CreateDefault();
        [SerializeField] private PlanetMemoryBudget runtimeBudget = PlanetMemoryBudget.CreateDefault();
        [SerializeField] private bool useExternalTestOverride = true;
        [SerializeField] private string overrideFilePath;
        [SerializeField] private string lastBudgetLoadMessage;

        [Header("Last Result")]
        [SerializeField] private PlanetMemorySnapshot beforeSnapshot;
        [SerializeField] private PlanetMemorySnapshot afterSnapshot;
        [SerializeField] private PlanetMemorySnapshot lastSnapshot;
        [SerializeField] private PlanetMemorySnapshotComparison lastComparison;
        [SerializeField] private PlanetUnityMemoryProfilerCaptureResult lastUnityMemoryCapture;
        [SerializeField] private PlanetLabDiagnostic lastDiagnostic;
        [SerializeField] private string lastAction;
        [SerializeField] private string lastOwnSnapshotExportPath;

        private readonly Stopwatch stopwatch = new Stopwatch();
        private readonly List<int> mockResourceIds = new List<int>(64);
        private PlanetMemoryProfilerRecorderSet recorderSet;
        private byte[] lastAllocationSmokeBuffer;

        public override string ModuleName => "Memory Lab";
        public override bool HasLiveResources => mockResourceIds.Count > 0;

        public PlanetMemoryBudget RuntimeBudget => runtimeBudget;
        public PlanetMemorySnapshot LastMemorySnapshot => lastSnapshot;
        public PlanetMemorySnapshotComparison LastComparison => lastComparison;
        public PlanetLabDiagnostic LastDiagnostic => lastDiagnostic;
        public string LastAction => lastAction;
        public string LastOwnSnapshotExportPath => lastOwnSnapshotExportPath;
        public string OverrideFilePath => overrideFilePath;

        public override bool ValidateModule()
        {
            ResolveReferences();
            EnsureRecorderSet();
            ReloadTestBudget();

            if (resourceRegistry == null)
            {
                lastDiagnostic = PlanetLabDiagnostic.Critical(
                    "Memory Lab registry is missing",
                    "PlanetMemoryLab cannot validate ownership without PlanetLabResourceRegistry.",
                    "Assign the scene registry or rebuild PlanetImplementationLab.",
                    "resourceRegistry=null");
                lastAction = "Validate Memory Setup failed.";
                return false;
            }

            if (runtimeBudget == null)
            {
                lastDiagnostic = PlanetLabDiagnostic.Critical(
                    "Memory budget is missing",
                    "PlanetMemoryLab has no resolved runtime budget.",
                    "Fix the serialized budget defaults or reload the test budget.",
                    "overrideFilePath=" + overrideFilePath);
                lastAction = "Validate Memory Setup failed.";
                return false;
            }

            if (!runtimeBudget.IsValid(out string budgetMessage))
            {
                lastDiagnostic = PlanetLabDiagnostic.Critical(
                    "Memory budget is invalid",
                    budgetMessage,
                    "Fix the serialized budget defaults or external override file.",
                    "overrideFilePath=" + overrideFilePath);
                lastAction = "Validate Memory Setup failed.";
                return false;
            }

            lastDiagnostic = PlanetLabDiagnostic.Ok(
                "Memory Lab setup is valid",
                "budgetProfile=" + runtimeBudget.profileName +
                "\noverrideFilePath=" + overrideFilePath +
                "\nprofilerCounters=" + recorderSet.Count +
                "\nprofilerCountersAvailable=" + recorderSet.AvailableCount +
                "\nprofilerCountersUnavailable=" + recorderSet.UnavailableCount);
            lastAction = "Validate Memory Setup OK.";
            return true;
        }

        public override void InitModule()
        {
            stopwatch.Restart();
            ValidateModule();
            stopwatch.Stop();
            CaptureSnapshot("Init Memory Lab", stopwatch.Elapsed.TotalMilliseconds, false);
        }

        public override void ReleaseModule()
        {
            stopwatch.Restart();
            ReleaseMockResources();
            lastAllocationSmokeBuffer = null;
            stopwatch.Stop();
            CaptureSnapshot("Release Memory Lab", stopwatch.Elapsed.TotalMilliseconds, true);
        }

        public override PlanetLabMetricsSnapshot CaptureMetrics()
        {
            CaptureSnapshot("Capture Memory Snapshot", 0, false);
            return PlanetLabMetricsSnapshot.Capture("Capture Memory Snapshot", resourceRegistry, 0);
        }

        public void CaptureSnapshot()
        {
            CaptureSnapshot("Capture Snapshot", 0, false);
        }

        public void CaptureBefore()
        {
            beforeSnapshot = CaptureSnapshot("Capture Before", 0, false);
            lastAction = "Before snapshot captured.";
        }

        public void CaptureAfter()
        {
            afterSnapshot = CaptureSnapshot("Capture After", 0, false);
            lastAction = "After snapshot captured.";
        }

        public void CompareLastSnapshots()
        {
            lastComparison = PlanetMemorySnapshotComparison.Compare(
                "Compare Last Snapshots",
                beforeSnapshot,
                afterSnapshot,
                runtimeBudget,
                false);
            lastDiagnostic = lastComparison.diagnostic;
            lastAction = "Snapshot comparison captured.";
        }

        public void CaptureUnityMemorySnapshot()
        {
            stopwatch.Restart();
            lastUnityMemoryCapture = PlanetUnityMemoryProfilerCapture.Capture("Capture Unity Memory Snapshot");
            stopwatch.Stop();
            lastUnityMemoryCapture.operationMs = stopwatch.Elapsed.TotalMilliseconds;
            lastDiagnostic = lastUnityMemoryCapture.diagnostic;
            lastAction = "Unity Memory Snapshot request finished.";
        }

        public void CaptureUnityMemoryBefore()
        {
            lastUnityMemoryCapture = PlanetUnityMemoryProfilerCapture.Capture("Capture Unity Memory Before");
            lastDiagnostic = lastUnityMemoryCapture.diagnostic;
            lastAction = "Unity Memory Before request finished.";
        }

        public void CaptureUnityMemoryAfter()
        {
            lastUnityMemoryCapture = PlanetUnityMemoryProfilerCapture.Capture("Capture Unity Memory After");
            lastDiagnostic = lastUnityMemoryCapture.diagnostic;
            lastAction = "Unity Memory After request finished.";
        }

        public void ExportLastOwnSnapshot()
        {
            lastOwnSnapshotExportPath = PlanetMemorySnapshotExporter.ExportSnapshot(
                lastSnapshot,
                runtimeBudget,
                resourceRegistry,
                lastUnityMemoryCapture.snapshotPath);
            lastAction = "Own memory snapshot exported.";
        }

        public void ExportLastOwnSnapshotComparison()
        {
            lastOwnSnapshotExportPath = PlanetMemorySnapshotExporter.ExportComparison(
                lastComparison,
                runtimeBudget,
                resourceRegistry,
                lastUnityMemoryCapture.snapshotPath);
            lastAction = "Own memory comparison exported.";
        }

        public void ReloadTestBudget()
        {
            if (budgetDefaults == null)
            {
                budgetDefaults = PlanetMemoryBudget.CreateDefault();
            }

            runtimeBudget = budgetDefaults.Clone();
            PlanetMemoryLabPaths.EnsureOutputDirectory();
            overrideFilePath = PlanetMemoryLabPaths.GetBudgetOverridePath();

            if (!useExternalTestOverride)
            {
                lastBudgetLoadMessage = "External override disabled. Using serialized defaults.";
                return;
            }

            if (!File.Exists(overrideFilePath))
            {
                lastBudgetLoadMessage = "No external override found. Using serialized defaults.";
                return;
            }

            try
            {
                string json = File.ReadAllText(overrideFilePath);
                PlanetMemoryBudget overrideBudget = JsonUtility.FromJson<PlanetMemoryBudget>(json);
                if (overrideBudget == null)
                {
                    lastBudgetLoadMessage = "External override could not be parsed. Using defaults.";
                    return;
                }

                if (overrideBudget.IsValid(out string validationMessage))
                {
                    runtimeBudget = overrideBudget;
                    lastBudgetLoadMessage = "External override loaded: " + overrideBudget.profileName;
                    return;
                }

                lastBudgetLoadMessage = "External override invalid. Using defaults. " + validationMessage;
            }
            catch (Exception exception)
            {
                lastBudgetLoadMessage = "External override read failed. Using defaults. " + exception.Message;
            }
        }

        public void RunAllocationSmokeTest()
        {
            stopwatch.Restart();
            lastAllocationSmokeBuffer = new byte[4096];
            lastAllocationSmokeBuffer[0] = 1;
            stopwatch.Stop();
            CaptureSnapshot("Run Allocation Smoke Test", stopwatch.Elapsed.TotalMilliseconds, false);
            lastDiagnostic = PlanetLabDiagnostic.Warning(
                "Allocation smoke test executed",
                "This button deliberately creates a small managed allocation to prove diagnostics can report allocation-related risk.",
                "Use this only as a manual diagnostic. Do not copy this pattern into runtime hot paths.",
                lastSnapshot.diagnostic.relatedMetrics);
            lastSnapshot.diagnostic = lastDiagnostic;
            lastAction = "Allocation smoke test finished.";
        }

        public void RunRegistrySmokeTest()
        {
            stopwatch.Restart();
            ReleaseMockResources();
            int cpu = RegisterMock("Registry Smoke CPU Buffer", PlanetLabResourceType.CpuBuffer, 64 * 1024, 1024, 64);
            int gpu = RegisterMock("Registry Smoke GraphicsBuffer", PlanetLabResourceType.GraphicsBuffer, 128 * 1024, 2048, 64);
            bool registered = cpu != 0 && gpu != 0 && resourceRegistry != null && resourceRegistry.LiveResourceCount >= 2;
            ReleaseMockResources();
            stopwatch.Stop();

            CaptureSnapshot("Run Registry Smoke Test", stopwatch.Elapsed.TotalMilliseconds, true);
            if (!registered)
            {
                lastDiagnostic = PlanetLabDiagnostic.Critical(
                    "Registry smoke test failed",
                    "The Memory Lab could not register and observe mock resources.",
                    "Check the registry reference before continuing with memory validation.",
                    "cpuId=" + cpu + "\ngpuId=" + gpu);
                lastSnapshot.diagnostic = lastDiagnostic;
            }

            lastAction = "Registry smoke test finished.";
        }

        public void RunReleaseSmokeTest()
        {
            stopwatch.Restart();
            ReleaseMockResources();
            RegisterMock("Release Smoke Mesh", PlanetLabResourceType.Mesh, 256 * 1024, 0, 0);
            RegisterMock("Release Smoke Material", PlanetLabResourceType.RuntimeMaterial, 1, 1, 0);
            ReleaseMockResources();
            stopwatch.Stop();
            CaptureSnapshot("Run Release Smoke Test", stopwatch.Elapsed.TotalMilliseconds, true);
            lastAction = "Release smoke test finished.";
        }

        public void RunStressLow()
        {
            RunMemoryStress("Stress Low", 10, 1 * 1024 * 1024L, 1 * 1024 * 1024L);
        }

        public void RunStressMedium()
        {
            RunMemoryStress("Stress Medium", 25, 8 * 1024 * 1024L, 8 * 1024 * 1024L);
        }

        public void RunStressHigh()
        {
            RunMemoryStress("Stress High", 50, 32 * 1024 * 1024L, 32 * 1024 * 1024L);
        }

        public void RunStressExtreme()
        {
            RunMemoryStress("Stress Extreme", 100, 129 * 1024 * 1024L, 129 * 1024 * 1024L);
        }

        public void ReleaseAll()
        {
            if (labController != null)
            {
                labController.ReleaseAll();
                lastSnapshot = PlanetMemorySnapshot.Capture(
                    "Release All",
                    resourceRegistry,
                    runtimeBudget,
                    recorderSet,
                    0,
                    true);
                lastDiagnostic = lastSnapshot.diagnostic;
                lastAction = "Release All requested through controller.";
                return;
            }

            ReleaseModule();
        }

        public void ShowLiveResources()
        {
            CaptureSnapshot("Show Live Resources", 0, false);
        }

        public void ResetMemoryLabState()
        {
            ReleaseModule();
            beforeSnapshot = new PlanetMemorySnapshot();
            afterSnapshot = new PlanetMemorySnapshot();
            lastComparison = new PlanetMemorySnapshotComparison();
            lastUnityMemoryCapture = new PlanetUnityMemoryProfilerCaptureResult();
            lastOwnSnapshotExportPath = string.Empty;
            CaptureSnapshot("Reset Memory Lab State", 0, true);
            lastAction = "Memory Lab state reset.";
        }

        public override void RunModuleTest()
        {
            RunRegistrySmokeTest();
        }

        public override void RunModuleStress()
        {
            RunStressLow();
        }

        private PlanetMemorySnapshot CaptureSnapshot(string operationName, double operationMs, bool requireCleanRelease)
        {
            EnsureRecorderSet();

            lastSnapshot = PlanetMemorySnapshot.Capture(
                operationName,
                resourceRegistry,
                runtimeBudget,
                recorderSet,
                operationMs,
                requireCleanRelease);
            lastDiagnostic = lastSnapshot.diagnostic;
            lastAction = operationName + " captured.";
            return lastSnapshot;
        }

        private void RunMemoryStress(string stressName, int cycleCount, long cpuBytes, long gpuBytes)
        {
            stopwatch.Restart();

            for (int i = 0; i < cycleCount; i++)
            {
                ReleaseMockResources();
                RegisterMock(stressName + " CPU " + i, PlanetLabResourceType.ManagedArray, cpuBytes, 1, (int)Math.Min(cpuBytes, int.MaxValue));
                RegisterMock(stressName + " GPU " + i, PlanetLabResourceType.GraphicsBuffer, gpuBytes, 1, (int)Math.Min(gpuBytes, int.MaxValue));
                ReleaseMockResources();
            }

            stopwatch.Stop();
            CaptureSnapshot(stressName, stopwatch.Elapsed.TotalMilliseconds, true);
            lastAction = stressName + " finished.";
        }

        private int RegisterMock(string resourceName, PlanetLabResourceType resourceType, long estimatedBytes, int elementCount, int stride)
        {
            if (resourceRegistry == null)
            {
                return 0;
            }

            int resourceId = resourceRegistry.RegisterResource(resourceName, resourceType, OwnerName, estimatedBytes, elementCount, stride);
            mockResourceIds.Add(resourceId);
            return resourceId;
        }

        private void ReleaseMockResources()
        {
            if (resourceRegistry != null)
            {
                for (int i = 0; i < mockResourceIds.Count; i++)
                {
                    resourceRegistry.MarkReleased(mockResourceIds[i]);
                }

                resourceRegistry.RecalculateLiveTotals();
            }

            mockResourceIds.Clear();
        }

        private void ResolveReferences()
        {
            if (resourceRegistry == null)
            {
                resourceRegistry = GetComponentInParent<PlanetLabResourceRegistry>();
            }

            if (labController == null)
            {
                labController = GetComponentInParent<PlanetImplementationLabController>();
            }
        }

        private void EnsureRecorderSet()
        {
            if (recorderSet != null)
            {
                return;
            }

            recorderSet = new PlanetMemoryProfilerRecorderSet();
            recorderSet.InitializeDefaultCounters();
        }

        private void OnDisable()
        {
            ReleaseModule();
            if (recorderSet != null)
            {
                recorderSet.Dispose();
                recorderSet = null;
            }
        }
    }
}
