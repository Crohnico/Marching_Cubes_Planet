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
    public sealed class PlanetComputeShaderLabEvidencePlayModeTests
    {
        private const string SceneName = "PlanetImplementationLab";
        private const string ReportDirectory = "Resources/PlanetLabReports";
        private const string ReportFileName = "PlanetComputeShaderLab_SmokeEvidence.json";

        [UnityTest]
        public IEnumerator ComputeSmokeStressEvidenceIsSavedAndReleaseValidated()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            Assert.IsNotNull(load, "PlanetImplementationLab must be included in Build Settings for PlayMode tests.");

            while (!load.isDone)
            {
                yield return null;
            }

            PlanetComputeShaderRunnerLab computeLab =
                UnityEngine.Object.FindFirstObjectByType<PlanetComputeShaderRunnerLab>();

            Assert.IsNotNull(computeLab);

            PlanetComputeEvidenceReport report = new PlanetComputeEvidenceReport
            {
                sceneName = SceneName,
                moduleName = computeLab.ModuleName,
                createdAtUtc = DateTime.UtcNow.ToString("O"),
                supportsComputeShaders = SystemInfo.supportsComputeShaders,
                supportsInstancing = SystemInfo.supportsInstancing,
                records = new PlanetComputeEvidenceRecord[17]
            };

            int index = 0;
            report.records[index++] = RunAndRecord(
                "Validate Module",
                computeLab,
                () => { computeLab.ValidateModule(); },
                SystemInfo.supportsComputeShaders);

            if (SystemInfo.supportsComputeShaders)
            {
                report.records[index++] = RunAndRecord("Create GraphicsBuffer Test", computeLab, computeLab.CreateGraphicsBufferTest, false);
                report.records[index++] = RunAndRecord("Dispatch Once GraphicsBuffer", computeLab, computeLab.DispatchOnce, false);
                report.records[index++] = RunAndRecord("Release Module after GraphicsBuffer", computeLab, computeLab.ReleaseModule, true);

                report.records[index++] = RunAndRecord("Create ComputeBuffer Test", computeLab, computeLab.CreateComputeBufferTest, false);
                report.records[index++] = RunAndRecord("Dispatch Once ComputeBuffer", computeLab, computeLab.DispatchOnce, false);
                report.records[index++] = RunAndRecord("Release Module after ComputeBuffer", computeLab, computeLab.ReleaseModule, true);

                report.records[index++] = RunAndRecord("Dispatch 100x", computeLab, computeLab.Dispatch100, false);
                report.records[index++] = RunAndRecord("Release Module after Dispatch 100x", computeLab, computeLab.ReleaseModule, true);

                report.records[index++] = RunAndRecord("Run GraphicsBuffer Stress", computeLab, computeLab.RunGraphicsBufferStress, true);
                report.records[index++] = RunAndRecord("Run ComputeBuffer Stress", computeLab, computeLab.RunComputeBufferStress, true);
                report.records[index++] = RunAndRecord("Run Stress Low", computeLab, computeLab.RunStressLow, true);
                report.records[index++] = RunAndRecord("Run Stress Medium", computeLab, computeLab.RunStressMedium, true);
                report.records[index++] = RunAndRecord("Run Comparison", computeLab, computeLab.RunComparison, true);
                report.records[index++] = RunAndRecord("Reset Module State", computeLab, computeLab.ResetModuleState, true);
            }
            else
            {
                report.records[index++] = RunAndRecord("Release Module unsupported runtime", computeLab, computeLab.ReleaseModule, true);
            }

            report.records[index++] = RunAndRecord("Final Release Module", computeLab, computeLab.ReleaseModule, true);
            report.records[index++] = RunAndRecord("Final Capture Metrics", computeLab, () => computeLab.CaptureMetrics(), true);
            TrimUnusedRecords(report, index);

            string reportPath = SaveReport(report);
            string savedJson = File.ReadAllText(reportPath);
            Assert.IsTrue(savedJson.Contains("\"moduleName\": \"Compute Shader Lab\""));

            for (int i = 0; i < report.records.Length; i++)
            {
                PlanetComputeEvidenceRecord record = report.records[i];
                Assert.IsTrue(record.executedWithoutException, record.actionName + " threw: " + record.exception);

                if (record.expectedCleanAfterAction)
                {
                    Assert.AreEqual(0, record.liveResourceCount, record.actionName);
                    Assert.AreEqual(0, record.ownedGpuEstimatedBytes, record.actionName);
                    Assert.AreEqual(PlanetLabDiagnosticSeverity.OK.ToString(), record.severity, record.actionName);
                }
            }

            Debug.Log("PlanetComputeShaderLab evidence saved: " + reportPath, computeLab);
        }

        private static PlanetComputeEvidenceRecord RunAndRecord(
            string actionName,
            PlanetComputeShaderRunnerLab computeLab,
            Action action,
            bool expectedCleanAfterAction)
        {
            try
            {
                action();
                return CaptureRecord(actionName, computeLab, true, string.Empty, expectedCleanAfterAction);
            }
            catch (Exception exception)
            {
                return CaptureRecord(
                    actionName,
                    computeLab,
                    false,
                    exception.GetType().Name + ": " + exception.Message,
                    expectedCleanAfterAction);
            }
        }

        private static PlanetComputeEvidenceRecord RunAndRecord(
            string actionName,
            PlanetComputeShaderRunnerLab computeLab,
            Func<bool> action,
            bool expectedCleanAfterAction)
        {
            try
            {
                bool result = action();
                return CaptureRecord(
                    actionName,
                    computeLab,
                    result,
                    result ? string.Empty : "Action returned false.",
                    expectedCleanAfterAction);
            }
            catch (Exception exception)
            {
                return CaptureRecord(
                    actionName,
                    computeLab,
                    false,
                    exception.GetType().Name + ": " + exception.Message,
                    expectedCleanAfterAction);
            }
        }

        private static PlanetComputeEvidenceRecord RunAndRecord(
            string actionName,
            PlanetComputeShaderRunnerLab computeLab,
            Func<PlanetLabMetricsSnapshot> action,
            bool expectedCleanAfterAction)
        {
            try
            {
                action();
                return CaptureRecord(actionName, computeLab, true, string.Empty, expectedCleanAfterAction);
            }
            catch (Exception exception)
            {
                return CaptureRecord(
                    actionName,
                    computeLab,
                    false,
                    exception.GetType().Name + ": " + exception.Message,
                    expectedCleanAfterAction);
            }
        }

        private static PlanetComputeEvidenceRecord CaptureRecord(
            string actionName,
            PlanetComputeShaderRunnerLab computeLab,
            bool executedWithoutException,
            string exception,
            bool expectedCleanAfterAction)
        {
            PlanetLabMetricsSnapshot snapshot = computeLab.LastSnapshot;
            PlanetLabDiagnostic diagnostic = computeLab.LastDiagnostic;

            return new PlanetComputeEvidenceRecord
            {
                actionName = actionName,
                lastAction = computeLab.LastAction,
                expectedCleanAfterAction = expectedCleanAfterAction,
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

        private static void TrimUnusedRecords(PlanetComputeEvidenceReport report, int count)
        {
            if (count == report.records.Length)
            {
                return;
            }

            PlanetComputeEvidenceRecord[] trimmed = new PlanetComputeEvidenceRecord[count];
            Array.Copy(report.records, trimmed, count);
            report.records = trimmed;
        }

        private static string SaveReport(PlanetComputeEvidenceReport report)
        {
            string directory = Path.Combine(Application.dataPath, ReportDirectory);
            Directory.CreateDirectory(directory);

            string reportPath = Path.Combine(directory, ReportFileName);
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            return reportPath;
        }

        [Serializable]
        private sealed class PlanetComputeEvidenceReport
        {
            public string sceneName;
            public string moduleName;
            public string createdAtUtc;
            public bool supportsComputeShaders;
            public bool supportsInstancing;
            public PlanetComputeEvidenceRecord[] records;
        }

        [Serializable]
        private sealed class PlanetComputeEvidenceRecord
        {
            public string actionName;
            public string lastAction;
            public bool expectedCleanAfterAction;
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
