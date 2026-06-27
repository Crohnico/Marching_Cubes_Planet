using System;
using System.Collections;
using System.IO;
using MarchingCubesPlanet.Lab;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MarchingCubesPlanet.Lab.Tests
{
    public sealed class PlanetImplementationLabEvidencePlayModeTests
    {
        private const string SceneName = "PlanetImplementationLab";
        private const string ReportDirectory = "Resources/PlanetLabReports";
        private const string ReportFileName = "PlanetImplementationLab_SmokeEvidence.json";

        [UnityTest]
        public IEnumerator SmokeStressReleaseEvidenceIsSavedAndClean()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            Assert.IsNotNull(load, "PlanetImplementationLab must be included in Build Settings for PlayMode tests.");

            while (!load.isDone)
            {
                yield return null;
            }

            PlanetImplementationLabController controller =
                UnityEngine.Object.FindFirstObjectByType<PlanetImplementationLabController>();

            Assert.IsNotNull(controller);

            PlanetLabEvidenceReport report = new PlanetLabEvidenceReport
            {
                sceneName = SceneName,
                createdAtUtc = DateTime.UtcNow.ToString("O"),
                records = new PlanetLabEvidenceRecord[15]
            };

            int index = 0;
            report.records[index++] = RunAndRecord(
                "Validate Scene and Capture Metrics",
                controller,
                () =>
                {
                    bool valid = controller.ValidateScene();
                    if (valid)
                    {
                        controller.CaptureMetrics();
                    }

                    return valid;
                });
            report.records[index++] = RunAndRecord("Run Smoke Test", controller, controller.RunSmokeTest);
            report.records[index++] = RunAndRecord("Release All after Smoke", controller, controller.ReleaseAll);
            report.records[index++] = RunAndRecord("Run Stress Low", controller, controller.RunStressLow);
            report.records[index++] = RunAndRecord("Release All after Stress Low", controller, controller.ReleaseAll);
            report.records[index++] = RunAndRecord("Run Stress Medium", controller, controller.RunStressMedium);
            report.records[index++] = RunAndRecord("Release All after Stress Medium", controller, controller.ReleaseAll);
            report.records[index++] = RunAndRecord("Run Stress High", controller, controller.RunStressHigh);
            report.records[index++] = RunAndRecord("Release All after Stress High", controller, controller.ReleaseAll);
            report.records[index++] = RunAndRecord("Run Stress Extreme", controller, controller.RunStressExtreme);
            report.records[index++] = RunAndRecord("Release All after Stress Extreme", controller, controller.ReleaseAll);
            report.records[index++] = RunAndRecord("Force GC Check", controller, controller.ForceGcCheck);
            report.records[index++] = RunAndRecord("Release All after Force GC Check", controller, controller.ReleaseAll);
            report.records[index++] = RunAndRecord("Force GPU Release", controller, controller.ForceGpuRelease);
            report.records[index++] = RunAndRecord("Reset Lab State", controller, controller.ResetLabState);

            string reportPath = SaveReport(report);
            string savedJson = File.ReadAllText(reportPath);
            Assert.IsTrue(savedJson.Contains("\"sceneName\": \"PlanetImplementationLab\""));

            for (int i = 0; i < report.records.Length; i++)
            {
                PlanetLabEvidenceRecord record = report.records[i];
                Assert.IsTrue(record.executedWithoutException, record.actionName + " threw: " + record.exception);
                Assert.AreEqual(PlanetLabDiagnosticSeverity.OK.ToString(), record.severity, record.actionName);
                Assert.AreEqual(0, record.liveResourceCount, record.actionName);
                Assert.AreEqual(0, record.ownedCpuEstimatedBytes, record.actionName);
                Assert.AreEqual(0, record.ownedGpuEstimatedBytes, record.actionName);
            }

            Debug.Log("PlanetImplementationLab evidence saved: " + reportPath, controller);
        }

        private static PlanetLabEvidenceRecord RunAndRecord(
            string actionName,
            PlanetImplementationLabController controller,
            Action action)
        {
            try
            {
                action();
                return CaptureRecord(actionName, controller, true, string.Empty);
            }
            catch (Exception exception)
            {
                return CaptureRecord(actionName, controller, false, exception.GetType().Name + ": " + exception.Message);
            }
        }

        private static PlanetLabEvidenceRecord RunAndRecord(
            string actionName,
            PlanetImplementationLabController controller,
            Func<bool> action)
        {
            try
            {
                bool result = action();
                return CaptureRecord(actionName, controller, result, result ? string.Empty : "Action returned false.");
            }
            catch (Exception exception)
            {
                return CaptureRecord(actionName, controller, false, exception.GetType().Name + ": " + exception.Message);
            }
        }

        private static PlanetLabEvidenceRecord CaptureRecord(
            string actionName,
            PlanetImplementationLabController controller,
            bool executedWithoutException,
            string exception)
        {
            PlanetLabMetricsSnapshot snapshot = controller.LastSnapshot;
            PlanetLabDiagnostic diagnostic = controller.LastDiagnostic;

            return new PlanetLabEvidenceRecord
            {
                actionName = actionName,
                lastAction = controller.LastAction,
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

        private static string SaveReport(PlanetLabEvidenceReport report)
        {
            string directory = Path.Combine(Application.dataPath, ReportDirectory);
            Directory.CreateDirectory(directory);

            string reportPath = Path.Combine(directory, ReportFileName);
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            return reportPath;
        }

        [Serializable]
        private sealed class PlanetLabEvidenceReport
        {
            public string sceneName;
            public string createdAtUtc;
            public PlanetLabEvidenceRecord[] records;
        }

        [Serializable]
        private sealed class PlanetLabEvidenceRecord
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
