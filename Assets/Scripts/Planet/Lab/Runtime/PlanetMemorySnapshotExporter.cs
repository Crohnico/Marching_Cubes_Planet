using System;
using System.IO;
using UnityEngine;

namespace MarchingCubesPlanet.Lab
{
    public static class PlanetMemorySnapshotExporter
    {
        public static string ExportSnapshot(
            PlanetMemorySnapshot snapshot,
            PlanetMemoryBudget budget,
            PlanetLabResourceRegistry registry,
            string unityMemorySnapshotPath)
        {
            PlanetMemorySnapshotComparison emptyComparison = new PlanetMemorySnapshotComparison();
            return Export(snapshot.operationName, snapshot, emptyComparison, false, budget, registry, unityMemorySnapshotPath);
        }

        public static string ExportComparison(
            PlanetMemorySnapshotComparison comparison,
            PlanetMemoryBudget budget,
            PlanetLabResourceRegistry registry,
            string unityMemorySnapshotPath)
        {
            return Export(comparison.comparisonName, comparison.after, comparison, true, budget, registry, unityMemorySnapshotPath);
        }

        private static string Export(
            string operationName,
            PlanetMemorySnapshot snapshot,
            PlanetMemorySnapshotComparison comparison,
            bool hasComparison,
            PlanetMemoryBudget budget,
            PlanetLabResourceRegistry registry,
            string unityMemorySnapshotPath)
        {
            PlanetMemoryLabPaths.EnsureReportDirectory();
            string directory = PlanetMemoryLabPaths.GetReportDirectory();

            PlanetLabResourceRecord[] liveResources = CopyLiveResources(registry);
            PlanetMemorySnapshotExport export = new PlanetMemorySnapshotExport
            {
                summary = BuildSummary(snapshot),
                details = new PlanetMemorySnapshotExportDetails
                {
                    createdAtUtc = DateTime.UtcNow.ToString("O"),
                    unityVersion = Application.unityVersion,
                    platform = Application.platform.ToString(),
                    budgets = budget != null ? budget.Clone() : null,
                    snapshot = snapshot,
                    comparison = hasComparison ? comparison : new PlanetMemorySnapshotComparison(),
                    liveResources = liveResources,
                    profilerCounters = snapshot.profilerCounters,
                    unityMemorySnapshotPath = unityMemorySnapshotPath
                }
            };

            string fileName = "PlanetMemory_" +
                              DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") +
                              "_" +
                              MakeSafeFilePart(operationName) +
                              ".planet-memory.json";
            string path = Path.Combine(directory, fileName);
            File.WriteAllText(path, JsonUtility.ToJson(export, true));
            return path;
        }

        private static PlanetMemorySnapshotExportSummary BuildSummary(PlanetMemorySnapshot snapshot)
        {
            PlanetLabDiagnostic diagnostic = snapshot.diagnostic;
            string status = diagnostic.severity.ToString();
            string riskLevel = diagnostic.severity == PlanetLabDiagnosticSeverity.Critical
                ? "High"
                : diagnostic.severity == PlanetLabDiagnosticSeverity.Warning ? "Medium" : "Low";

            return new PlanetMemorySnapshotExportSummary
            {
                status = status,
                riskLevel = riskLevel,
                operationName = snapshot.operationName,
                budgetResult = diagnostic.severity == PlanetLabDiagnosticSeverity.OK ? "Within budget" : diagnostic.title,
                releaseResult = snapshot.liveResourceCount == 0 ? "Clean" : "Live resources remain",
                gcResult = snapshot.unityGcAllocFrameBytes > 0 || snapshot.unityGcAllocFrameCount > 0 ? "GC detected" : "No GC counter hit or unavailable",
                topIssue = diagnostic.title,
                recommendedAction = diagnostic.recommendedAction
            };
        }

        private static PlanetLabResourceRecord[] CopyLiveResources(PlanetLabResourceRegistry registry)
        {
            if (registry == null || registry.LiveResourceCount == 0)
            {
                return Array.Empty<PlanetLabResourceRecord>();
            }

            PlanetLabResourceRecord[] liveResources = new PlanetLabResourceRecord[registry.LiveResourceCount];
            int count = registry.CopyLiveRecords(liveResources);
            if (count == liveResources.Length)
            {
                return liveResources;
            }

            PlanetLabResourceRecord[] trimmed = new PlanetLabResourceRecord[count];
            Array.Copy(liveResources, trimmed, count);
            return trimmed;
        }

        private static string MakeSafeFilePart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Snapshot";
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            string compact = value.Replace(" ", string.Empty);
            for (int i = 0; i < invalidChars.Length; i++)
            {
                compact = compact.Replace(invalidChars[i].ToString(), string.Empty);
            }

            return string.IsNullOrWhiteSpace(compact) ? "Snapshot" : compact;
        }
    }
}
