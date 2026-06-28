using System;

namespace MarchingCubesPlanet.Lab
{
    [Serializable]
    public sealed class PlanetMemorySnapshotExport
    {
        public PlanetMemorySnapshotExportSummary summary;
        public PlanetMemorySnapshotExportDetails details;
    }

    [Serializable]
    public struct PlanetMemorySnapshotExportSummary
    {
        public string status;
        public string riskLevel;
        public string operationName;
        public string budgetResult;
        public string releaseResult;
        public string gcResult;
        public string topIssue;
        public string recommendedAction;
    }

    [Serializable]
    public sealed class PlanetMemorySnapshotExportDetails
    {
        public string createdAtUtc;
        public string unityVersion;
        public string platform;
        public PlanetMemoryBudget budgets;
        public PlanetMemorySnapshot snapshot;
        public PlanetMemorySnapshotComparison comparison;
        public PlanetLabResourceRecord[] liveResources;
        public PlanetMemoryProfilerCounterValue[] profilerCounters;
        public string unityMemorySnapshotPath;
    }
}
