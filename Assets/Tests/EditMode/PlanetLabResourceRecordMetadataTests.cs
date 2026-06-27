using MarchingCubesPlanet.Lab;
using NUnit.Framework;
using UnityEngine;

namespace MarchingCubesPlanet.Lab.Tests
{
    public sealed class PlanetLabResourceRecordMetadataTests
    {
        [Test]
        public void RegisterResourceStoresElementCountAndStride()
        {
            GameObject registryObject = new GameObject("Registry Test");
            PlanetLabResourceRegistry registry = registryObject.AddComponent<PlanetLabResourceRegistry>();

            registry.RegisterResource("GPU", PlanetLabResourceType.GraphicsBuffer, "Test", 256, 16, 16);

            Assert.AreEqual(16, registry.Records[0].elementCount);
            Assert.AreEqual(16, registry.Records[0].stride);

            Object.DestroyImmediate(registryObject);
        }
    }
}
