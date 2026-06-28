using MarchingCubesPlanet.Lab;
using NUnit.Framework;

namespace MarchingCubesPlanet.Lab.Tests
{
    public sealed class PlanetMemoryBudgetTests
    {
        [Test]
        public void DefaultBudgetMatchesQuest3LabLimits()
        {
            PlanetMemoryBudget budget = PlanetMemoryBudget.CreateDefault();

            Assert.AreEqual(384, budget.ownedCpuSoftMiB);
            Assert.AreEqual(512, budget.ownedCpuHardMiB);
            Assert.AreEqual(384, budget.ownedGpuSoftMiB);
            Assert.AreEqual(512, budget.ownedGpuHardMiB);
            Assert.AreEqual(768, budget.ownedCombinedSoftMiB);
            Assert.AreEqual(1024, budget.ownedCombinedHardMiB);
            Assert.AreEqual(128, budget.singleResourceSoftMiB);
            Assert.AreEqual(256, budget.singleResourceHardMiB);
            Assert.IsTrue(budget.IsValid(out _));
        }

        [Test]
        public void InvalidBudgetRejectsSoftAboveHard()
        {
            PlanetMemoryBudget budget = PlanetMemoryBudget.CreateDefault();
            budget.ownedGpuSoftMiB = 600;
            budget.ownedGpuHardMiB = 512;

            Assert.IsFalse(budget.IsValid(out string message));
            Assert.IsNotEmpty(message);
        }

        [Test]
        public void MiBConversionUsesBinaryMegabytes()
        {
            Assert.AreEqual(134217728L, PlanetMemoryBudget.MiBToBytes(128));
        }
    }
}
