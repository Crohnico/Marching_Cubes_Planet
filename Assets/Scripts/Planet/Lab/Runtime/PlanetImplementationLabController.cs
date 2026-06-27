using System;
using System.Diagnostics;
using UnityEngine;

namespace MarchingCubesPlanet.Lab
{
    public sealed class PlanetImplementationLabController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlanetLabResourceRegistry resourceRegistry;
        [SerializeField] private PlanetLabModule[] modules = new PlanetLabModule[0];

        [Header("Stress Presets")]
        [SerializeField] private PlanetLabStressPreset stressLow = CreatePreset("Stress Low", 3);
        [SerializeField] private PlanetLabStressPreset stressMedium = CreatePreset("Stress Medium", 10);
        [SerializeField] private PlanetLabStressPreset stressHigh = CreatePreset("Stress High", 25);
        [SerializeField] private PlanetLabStressPreset stressExtreme = CreatePreset("Stress Extreme", 50);

        [Header("Last Result")]
        [SerializeField] private PlanetLabMetricsSnapshot lastSnapshot;
        [SerializeField] private PlanetLabDiagnostic lastDiagnostic;
        [SerializeField] private string lastAction;

        private readonly Stopwatch stopwatch = new Stopwatch();

        public PlanetLabResourceRegistry ResourceRegistry => resourceRegistry;
        public PlanetLabMetricsSnapshot LastSnapshot => lastSnapshot;
        public PlanetLabDiagnostic LastDiagnostic => lastDiagnostic;
        public string LastAction => lastAction;

        public void ForceGcCheck()
        {
            stopwatch.Restart();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            stopwatch.Stop();

            CaptureMetrics("Force GC Check", stopwatch.Elapsed.TotalMilliseconds);
        }

        public void ForceGpuRelease()
        {
            Stopwatch forceStopwatch = Stopwatch.StartNew();
            ReleaseAll();
            Resources.UnloadUnusedAssets();
            forceStopwatch.Stop();

            CaptureMetrics("Force GPU Release", forceStopwatch.Elapsed.TotalMilliseconds);
        }

        public bool ValidateScene()
        {
            if (resourceRegistry == null)
            {
                lastDiagnostic = PlanetLabDiagnostic.Critical(
                    "PlanetLabResourceRegistry is missing",
                    "The controller cannot capture ownership or release diagnostics without a registry.",
                    "Assign a PlanetLabResourceRegistry in the controller or rebuild the lab scene.",
                    "resourceRegistry=null");
                lastAction = "Validate Scene failed.";
                return false;
            }

            RefreshModules();

            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i] == null)
                {
                    lastDiagnostic = PlanetLabDiagnostic.Critical(
                        "Null module reference",
                        "The Lab module list contains an empty slot.",
                        "Remove the null entry or press Refresh Modules.",
                        "moduleIndex=" + i);
                    lastAction = "Validate Scene failed.";
                    return false;
                }
            }

            lastDiagnostic = PlanetLabDiagnostic.Ok(
                "PlanetImplementationLab scene is valid",
                "Modules: " + modules.Length + "\nRegistry resources: " + resourceRegistry.LiveResourceCount);
            lastAction = "Validate Scene OK.";
            return true;
        }

        public void InitLab()
        {
            stopwatch.Restart();
            RefreshModules();

            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i] != null)
                {
                    modules[i].InitModule();
                }
            }

            stopwatch.Stop();
            CaptureMetrics("Init Lab", stopwatch.Elapsed.TotalMilliseconds);
        }

        public void ReleaseAll()
        {
            stopwatch.Restart();
            RefreshModules();

            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i] != null)
                {
                    modules[i].ReleaseModule();
                }
            }

            if (resourceRegistry != null)
            {
                resourceRegistry.RecalculateLiveTotals();
            }

            stopwatch.Stop();
            CaptureMetrics("Release All", stopwatch.Elapsed.TotalMilliseconds);

            if (resourceRegistry != null && resourceRegistry.LiveResourceCount > 0)
            {
                lastDiagnostic = PlanetLabDiagnostic.Critical(
                    "Release All left live resources",
                    "One or more registered resources still report isAlive after every module was released.",
                    "Inspect the owning module and verify that ReleaseModule marks each handle as released.",
                    "Live resources: " + resourceRegistry.LiveResourceCount);
                lastSnapshot.diagnostic = lastDiagnostic;
            }
        }

        public void CaptureMetrics()
        {
            CaptureMetrics("Manual Capture Metrics", 0);
        }

        public void RunSmokeTest()
        {
            bool valid = ValidateScene();
            if (!valid)
            {
                CaptureMetrics("Smoke Test Failed", 0);
                return;
            }

            InitLab();
            ReleaseAll();
            lastAction = "Smoke Test finished.";
        }

        public void RunStressLow()
        {
            RunStress(stressLow);
        }

        public void RunStressMedium()
        {
            RunStress(stressMedium);
        }

        public void RunStressHigh()
        {
            RunStress(stressHigh);
        }

        public void RunStressExtreme()
        {
            RunStress(stressExtreme);
        }

        public void ResetLabState()
        {
            ReleaseAll();

            if (resourceRegistry != null)
            {
                resourceRegistry.ResetRegistry();
            }

            lastSnapshot = PlanetLabMetricsSnapshot.Capture("Reset Lab State", resourceRegistry, 0);
            lastDiagnostic = lastSnapshot.diagnostic;
            lastAction = "Lab state reset.";
        }

        public void RefreshModules()
        {
            modules = GetComponentsInChildren<PlanetLabModule>(true);
        }

        private void RunStress(PlanetLabStressPreset preset)
        {
            if (!preset.IsValid(out string message))
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "Stress preset is invalid",
                    message,
                    "Fix the preset values in the Inspector before running this stress.",
                    preset.presetName);
                lastAction = "Stress aborted.";
                return;
            }

            Stopwatch stressStopwatch = Stopwatch.StartNew();

            for (int i = 0; i < preset.cycleCount; i++)
            {
                InitLab();

                if (preset.releaseBetweenCycles)
                {
                    ReleaseAll();
                }

                if (preset.captureMetricsEachCycle)
                {
                    CaptureMetrics(preset.presetName + " cycle " + i, 0);
                }
            }

            ReleaseAll();
            stressStopwatch.Stop();
            CaptureMetrics(preset.presetName, stressStopwatch.Elapsed.TotalMilliseconds);
            lastAction = preset.presetName + " finished.";
        }

        private void CaptureMetrics(string operationName, double operationMs)
        {
            lastSnapshot = PlanetLabMetricsSnapshot.Capture(operationName, resourceRegistry, operationMs);
            lastDiagnostic = lastSnapshot.diagnostic;
            lastAction = operationName + " captured.";
        }

        private void OnDisable()
        {
            ReleaseAll();
        }

        private static PlanetLabStressPreset CreatePreset(string presetName, int cycleCount)
        {
            return new PlanetLabStressPreset
            {
                presetName = presetName,
                whatItTests = "Runs the current Lab modules repeatedly.",
                expectedResult = "No registered resources remain alive after Release All.",
                riskCovered = "Basic lifecycle duplication and release regressions.",
                cycleCount = cycleCount,
                bufferElementCount = 1,
                dispatchRepeatCount = 1,
                trianglePayloadLimit = 0,
                releaseBetweenCycles = true,
                captureMetricsEachCycle = false
            };
        }
    }
}
