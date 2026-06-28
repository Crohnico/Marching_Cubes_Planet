using System;

namespace MarchingCubesPlanet.Lab
{
    [Serializable]
    public struct PlanetMemorySnapshotComparison
    {
        public string comparisonName;
        public PlanetMemorySnapshot before;
        public PlanetMemorySnapshot after;
        public long ownedCpuDeltaBytes;
        public long ownedGpuDeltaBytes;
        public long ownedCombinedDeltaBytes;
        public int liveResourceDelta;
        public PlanetLabDiagnostic diagnostic;

        public static PlanetMemorySnapshotComparison Compare(
            string comparisonName,
            PlanetMemorySnapshot before,
            PlanetMemorySnapshot after,
            PlanetMemoryBudget budget,
            bool requireCleanRelease)
        {
            PlanetMemorySnapshotComparison comparison = new PlanetMemorySnapshotComparison
            {
                comparisonName = comparisonName,
                before = before,
                after = after,
                ownedCpuDeltaBytes = after.ownedCpuEstimatedBytes - before.ownedCpuEstimatedBytes,
                ownedGpuDeltaBytes = after.ownedGpuEstimatedBytes - before.ownedGpuEstimatedBytes,
                ownedCombinedDeltaBytes = after.ownedCombinedEstimatedBytes - before.ownedCombinedEstimatedBytes,
                liveResourceDelta = after.liveResourceCount - before.liveResourceCount
            };

            comparison.diagnostic = PlanetMemoryDiagnostics.EvaluateComparison(comparison, budget, requireCleanRelease);
            return comparison;
        }
    }
}
