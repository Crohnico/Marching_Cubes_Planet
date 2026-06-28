using MarchingCubesPlanet.Lab;
using NUnit.Framework;

namespace MarchingCubesPlanet.Lab.Tests
{
    public sealed class PlanetMemoryDiagnosticsTests
    {
        [Test]
        public void SnapshotCriticalWhenReleaseRequiresCleanRegistry()
        {
            PlanetMemorySnapshot snapshot = new PlanetMemorySnapshot
            {
                operationName = "Release",
                liveResourceCount = 1
            };

            PlanetLabDiagnostic diagnostic = PlanetMemoryDiagnostics.Evaluate(
                snapshot,
                PlanetMemoryBudget.CreateDefault(),
                true);

            Assert.AreEqual(PlanetLabDiagnosticSeverity.Critical, diagnostic.severity);
        }

        [Test]
        public void SnapshotWarnsWhenSoftBudgetExceeded()
        {
            PlanetMemoryBudget budget = PlanetMemoryBudget.CreateDefault();
            PlanetMemorySnapshot snapshot = new PlanetMemorySnapshot
            {
                operationName = "Soft Budget",
                ownedGpuEstimatedBytes = budget.OwnedGpuSoftBytes + 1,
                ownedCombinedEstimatedBytes = budget.OwnedGpuSoftBytes + 1
            };

            PlanetLabDiagnostic diagnostic = PlanetMemoryDiagnostics.Evaluate(snapshot, budget, false);

            Assert.AreEqual(PlanetLabDiagnosticSeverity.Warning, diagnostic.severity);
        }

        [Test]
        public void SnapshotCriticalWhenHardBudgetExceeded()
        {
            PlanetMemoryBudget budget = PlanetMemoryBudget.CreateDefault();
            PlanetMemorySnapshot snapshot = new PlanetMemorySnapshot
            {
                operationName = "Hard Budget",
                ownedGpuEstimatedBytes = budget.OwnedGpuHardBytes + 1,
                ownedCombinedEstimatedBytes = budget.OwnedGpuHardBytes + 1
            };

            PlanetLabDiagnostic diagnostic = PlanetMemoryDiagnostics.Evaluate(snapshot, budget, false);

            Assert.AreEqual(PlanetLabDiagnosticSeverity.Critical, diagnostic.severity);
        }
    }
}
