using System.Collections;
using MarchingCubesPlanet.Lab;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MarchingCubesPlanet.Lab.Tests
{
    public sealed class PlanetImplementationLabScenePlayModeTests
    {
        private const string SceneName = "PlanetImplementationLab";

        [UnitySetUp]
        public IEnumerator LoadLabScene()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            Assert.IsNotNull(load, "PlanetImplementationLab must be included in Build Settings for PlayMode tests.");

            while (!load.isDone)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator SceneLoadsAndHasController()
        {
            yield return null;

            PlanetImplementationLabController controller =
                Object.FindFirstObjectByType<PlanetImplementationLabController>();

            Assert.IsNotNull(controller);
        }

        [UnityTest]
        public IEnumerator ValidateInitReleaseLifecycleWorks()
        {
            yield return null;

            PlanetImplementationLabController controller =
                Object.FindFirstObjectByType<PlanetImplementationLabController>();

            Assert.IsNotNull(controller);
            Assert.IsTrue(controller.ValidateScene());

            Assert.DoesNotThrow(controller.InitLab);
            Assert.DoesNotThrow(controller.ReleaseAll);
            Assert.DoesNotThrow(controller.InitLab);
            Assert.DoesNotThrow(controller.ReleaseAll);
            Assert.DoesNotThrow(controller.ReleaseAll);

            Assert.IsNotNull(controller.ResourceRegistry);
            Assert.AreEqual(0, controller.ResourceRegistry.LiveResourceCount);
        }

        [UnityTest]
        public IEnumerator SmokeTestLeavesRegistryClean()
        {
            yield return null;

            PlanetImplementationLabController controller =
                Object.FindFirstObjectByType<PlanetImplementationLabController>();

            Assert.IsNotNull(controller);

            controller.RunSmokeTest();

            Assert.IsNotNull(controller.ResourceRegistry);
            Assert.AreEqual(0, controller.ResourceRegistry.LiveResourceCount);
            Assert.AreEqual(PlanetLabDiagnosticSeverity.OK, controller.LastDiagnostic.severity);
        }

        [UnityTest]
        public IEnumerator StressPresetsLeaveRegistryClean()
        {
            yield return null;

            PlanetImplementationLabController controller =
                Object.FindFirstObjectByType<PlanetImplementationLabController>();

            Assert.IsNotNull(controller);

            Assert.DoesNotThrow(controller.RunStressLow);
            Assert.AreEqual(0, controller.ResourceRegistry.LiveResourceCount);
            Assert.AreEqual("Stress Low finished.", controller.LastAction);

            Assert.DoesNotThrow(controller.RunStressExtreme);
            Assert.AreEqual(0, controller.ResourceRegistry.LiveResourceCount);
            Assert.AreEqual("Stress Extreme finished.", controller.LastAction);
        }

        [UnityTest]
        public IEnumerator ForceCommandsCaptureMetricsAndLeaveRegistryClean()
        {
            yield return null;

            PlanetImplementationLabController controller =
                Object.FindFirstObjectByType<PlanetImplementationLabController>();

            Assert.IsNotNull(controller);

            Assert.DoesNotThrow(controller.ForceGcCheck);
            Assert.AreEqual("Force GC Check", controller.LastSnapshot.operationName);

            Assert.DoesNotThrow(controller.ForceGpuRelease);
            Assert.IsNotNull(controller.ResourceRegistry);
            Assert.AreEqual(0, controller.ResourceRegistry.LiveResourceCount);
            Assert.AreEqual("Force GPU Release", controller.LastSnapshot.operationName);
        }
    }
}
