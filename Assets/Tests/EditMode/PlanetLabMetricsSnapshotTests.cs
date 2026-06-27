using MarchingCubesPlanet.Lab;
using NUnit.Framework;
using UnityEngine;

namespace MarchingCubesPlanet.Lab.Tests
{
    public sealed class PlanetLabMetricsSnapshotTests
    {
        [Test]
        public void SnapshotReportsCleanRegistryAsOk()
        {
            GameObject registryObject = new GameObject("Registry Test");
            PlanetLabResourceRegistry registry = registryObject.AddComponent<PlanetLabResourceRegistry>();

            PlanetLabMetricsSnapshot snapshot = PlanetLabMetricsSnapshot.Capture("Test", registry, 2.5);

            Assert.AreEqual(PlanetLabDiagnosticSeverity.OK, snapshot.diagnostic.severity);
            Assert.AreEqual(0, snapshot.liveResourceCount);
            Assert.AreEqual(2.5, snapshot.operationMs);
            Assert.Greater(snapshot.managedHeapBytes, 0);

            Object.DestroyImmediate(registryObject);
        }

        [Test]
        public void SnapshotWarnsWhenRegistryHasLiveResources()
        {
            GameObject registryObject = new GameObject("Registry Test");
            PlanetLabResourceRegistry registry = registryObject.AddComponent<PlanetLabResourceRegistry>();
            registry.RegisterResource("GPU", PlanetLabResourceType.GraphicsBuffer, "Test", 128);

            PlanetLabMetricsSnapshot snapshot = PlanetLabMetricsSnapshot.Capture("Test", registry, 0);

            Assert.AreEqual(PlanetLabDiagnosticSeverity.Warning, snapshot.diagnostic.severity);
            Assert.AreEqual(1, snapshot.liveResourceCount);

            Object.DestroyImmediate(registryObject);
        }
    }
}
