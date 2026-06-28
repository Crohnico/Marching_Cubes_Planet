using MarchingCubesPlanet.Lab;
using NUnit.Framework;
using System.IO;
using UnityEngine;

namespace MarchingCubesPlanet.Lab.Tests
{
    public sealed class PlanetMemorySnapshotTests
    {
        private GameObject registryObject;
        private PlanetLabResourceRegistry registry;

        [SetUp]
        public void SetUp()
        {
            registryObject = new GameObject("Memory Snapshot Registry");
            registry = registryObject.AddComponent<PlanetLabResourceRegistry>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(registryObject);
        }

        [Test]
        public void CaptureIncludesOwnedBytesAndLargestResource()
        {
            registry.RegisterResource("Small CPU", PlanetLabResourceType.CpuBuffer, "Test", 64);
            registry.RegisterResource("Large GPU", PlanetLabResourceType.GraphicsBuffer, "Test", 128);

            PlanetMemorySnapshot snapshot = PlanetMemorySnapshot.Capture(
                "Capture",
                registry,
                PlanetMemoryBudget.CreateDefault(),
                null,
                1.25,
                false);

            Assert.AreEqual(64, snapshot.ownedCpuEstimatedBytes);
            Assert.AreEqual(128, snapshot.ownedGpuEstimatedBytes);
            Assert.AreEqual(192, snapshot.ownedCombinedEstimatedBytes);
            Assert.AreEqual("Large GPU", snapshot.largestSingleResourceName);
            Assert.AreEqual(1.25, snapshot.operationMs);
        }

        [Test]
        public void ComparisonReportsOwnedDeltas()
        {
            PlanetMemorySnapshot before = new PlanetMemorySnapshot
            {
                operationName = "Before",
                ownedCpuEstimatedBytes = 10,
                ownedGpuEstimatedBytes = 20,
                ownedCombinedEstimatedBytes = 30,
                liveResourceCount = 1
            };

            PlanetMemorySnapshot after = new PlanetMemorySnapshot
            {
                operationName = "After",
                ownedCpuEstimatedBytes = 7,
                ownedGpuEstimatedBytes = 9,
                ownedCombinedEstimatedBytes = 16,
                liveResourceCount = 0
            };

            PlanetMemorySnapshotComparison comparison = PlanetMemorySnapshotComparison.Compare(
                "Compare",
                before,
                after,
                PlanetMemoryBudget.CreateDefault(),
                true);

            Assert.AreEqual(-3, comparison.ownedCpuDeltaBytes);
            Assert.AreEqual(-11, comparison.ownedGpuDeltaBytes);
            Assert.AreEqual(-14, comparison.ownedCombinedDeltaBytes);
            Assert.AreEqual(PlanetLabDiagnosticSeverity.OK, comparison.diagnostic.severity);
        }

        [Test]
        public void ExportSnapshotWritesInsideProjectTemp()
        {
            PlanetMemorySnapshot snapshot = PlanetMemorySnapshot.Capture(
                "Export Test",
                registry,
                PlanetMemoryBudget.CreateDefault(),
                null,
                0,
                false);

            string path = PlanetMemorySnapshotExporter.ExportSnapshot(
                snapshot,
                PlanetMemoryBudget.CreateDefault(),
                registry,
                string.Empty);

            try
            {
                Assert.IsTrue(path.StartsWith(Directory.GetCurrentDirectory()));
                Assert.IsTrue(path.Contains(Path.Combine("Temp", "PlanetLabMemory", "Reports")));
                Assert.IsFalse(path.Contains(Path.Combine("Assets", string.Empty)));
                Assert.IsTrue(File.Exists(path));
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
