using MarchingCubesPlanet.Lab;
using NUnit.Framework;
using UnityEngine;

namespace MarchingCubesPlanet.Lab.Tests
{
    public sealed class PlanetLabResourceRegistryTests
    {
        private GameObject registryObject;
        private PlanetLabResourceRegistry registry;

        [SetUp]
        public void SetUp()
        {
            registryObject = new GameObject("Registry Test");
            registry = registryObject.AddComponent<PlanetLabResourceRegistry>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(registryObject);
        }

        [Test]
        public void RegisterResourceAddsOwnedBytesAndCounts()
        {
            registry.RegisterResource("CPU", PlanetLabResourceType.CpuBuffer, "Test", 64);
            registry.RegisterResource("GPU", PlanetLabResourceType.GraphicsBuffer, "Test", 128);

            Assert.AreEqual(64, registry.OwnedCpuEstimatedBytes);
            Assert.AreEqual(128, registry.OwnedGpuEstimatedBytes);
            Assert.AreEqual(2, registry.LiveResourceCount);
            Assert.AreEqual(1, registry.LiveGraphicsBuffers);
        }

        [Test]
        public void MarkReleasedReturnsCountersToZero()
        {
            int resourceId = registry.RegisterResource("GPU", PlanetLabResourceType.GraphicsBuffer, "Test", 128);

            Assert.IsTrue(registry.MarkReleased(resourceId));
            Assert.AreEqual(0, registry.OwnedGpuEstimatedBytes);
            Assert.AreEqual(0, registry.LiveResourceCount);
            Assert.AreEqual(0, registry.LiveGraphicsBuffers);
        }

        [Test]
        public void ReleasingTwiceDoesNotCreateNegativeCounters()
        {
            int resourceId = registry.RegisterResource("GPU", PlanetLabResourceType.GraphicsBuffer, "Test", 128);

            Assert.IsTrue(registry.MarkReleased(resourceId));
            Assert.IsFalse(registry.MarkReleased(resourceId));
            Assert.AreEqual(0, registry.OwnedGpuEstimatedBytes);
            Assert.AreEqual(0, registry.LiveResourceCount);
        }

        [Test]
        public void TransferOwnershipChangesOwner()
        {
            int resourceId = registry.RegisterResource("GPU", PlanetLabResourceType.GraphicsBuffer, "Old", 128);

            Assert.IsTrue(registry.TransferOwnership(resourceId, "New"));
            Assert.IsTrue(registry.TryGetResource(resourceId, out PlanetLabResourceRecord record));
            Assert.AreEqual("New", record.ownerModule);
        }

        [Test]
        public void CopyLiveRecordsWritesOnlyAliveRecords()
        {
            int releasedId = registry.RegisterResource("Released", PlanetLabResourceType.CpuBuffer, "Test", 64);
            registry.RegisterResource("Alive", PlanetLabResourceType.ManagedArray, "Test", 128);
            registry.MarkReleased(releasedId);

            PlanetLabResourceRecord[] buffer = new PlanetLabResourceRecord[4];
            int count = registry.CopyLiveRecords(buffer);

            Assert.AreEqual(1, count);
            Assert.AreEqual("Alive", buffer[0].resourceName);
        }
    }
}
