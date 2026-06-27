using MarchingCubesPlanet.Lab;
using NUnit.Framework;
using UnityEngine;

namespace MarchingCubesPlanet.Lab.Tests
{
    public sealed class PlanetLabModuleLifecycleTests
    {
        private GameObject moduleObject;
        private TestPlanetLabModule module;

        [SetUp]
        public void SetUp()
        {
            moduleObject = new GameObject("Module Test");
            module = moduleObject.AddComponent<TestPlanetLabModule>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(moduleObject);
        }

        [Test]
        public void RunModuleTestValidatesInitializesAndCapturesMetrics()
        {
            module.RunModuleTest();

            Assert.AreEqual(1, module.validateCount);
            Assert.AreEqual(1, module.initCount);
            Assert.AreEqual(1, module.captureCount);
            Assert.AreEqual(0, module.releaseCount);
        }

        [Test]
        public void RunModuleStressCyclesInitReleaseAndCapturesMetrics()
        {
            module.RunModuleStress();

            Assert.AreEqual(3, module.initCount);
            Assert.AreEqual(3, module.releaseCount);
            Assert.AreEqual(1, module.captureCount);
        }

        private sealed class TestPlanetLabModule : PlanetLabModule
        {
            public int validateCount;
            public int initCount;
            public int releaseCount;
            public int captureCount;

            public override string ModuleName => "Test Module";
            public override bool HasLiveResources => initCount > releaseCount;

            public override void InitModule()
            {
                initCount++;
            }

            public override void ReleaseModule()
            {
                releaseCount++;
            }

            public override bool ValidateModule()
            {
                validateCount++;
                return true;
            }

            public override PlanetLabMetricsSnapshot CaptureMetrics()
            {
                captureCount++;
                return new PlanetLabMetricsSnapshot();
            }
        }
    }
}
