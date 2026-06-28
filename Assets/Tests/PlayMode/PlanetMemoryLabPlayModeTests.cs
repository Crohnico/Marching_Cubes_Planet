using System.Collections;
using MarchingCubesPlanet.Lab;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MarchingCubesPlanet.Lab.Tests
{
    public sealed class PlanetMemoryLabPlayModeTests
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
        public IEnumerator MemoryLabExistsAndValidates()
        {
            yield return null;

            PlanetMemoryLab memoryLab = Object.FindFirstObjectByType<PlanetMemoryLab>();

            Assert.IsNotNull(memoryLab);
            Assert.IsTrue(memoryLab.ValidateModule());
            Assert.AreEqual(PlanetLabDiagnosticSeverity.OK, memoryLab.LastDiagnostic.severity);
        }

        [UnityTest]
        public IEnumerator SnapshotAndReleaseSmokeLeaveRegistryClean()
        {
            yield return null;

            PlanetMemoryLab memoryLab = Object.FindFirstObjectByType<PlanetMemoryLab>();
            PlanetImplementationLabController controller = Object.FindFirstObjectByType<PlanetImplementationLabController>();

            Assert.IsNotNull(memoryLab);
            Assert.IsNotNull(controller);

            memoryLab.CaptureSnapshot();
            Assert.AreEqual("Capture Snapshot", memoryLab.LastMemorySnapshot.operationName);

            memoryLab.RunRegistrySmokeTest();
            Assert.AreEqual(0, controller.ResourceRegistry.LiveResourceCount);

            memoryLab.RunReleaseSmokeTest();
            Assert.AreEqual(0, controller.ResourceRegistry.LiveResourceCount);

            Assert.DoesNotThrow(memoryLab.ReleaseModule);
            Assert.DoesNotThrow(memoryLab.ReleaseModule);
            Assert.AreEqual(0, controller.ResourceRegistry.LiveResourceCount);
        }

        [UnityTest]
        public IEnumerator StressLowAndUnavailableUnityCaptureAreDiagnosed()
        {
            yield return null;

            PlanetMemoryLab memoryLab = Object.FindFirstObjectByType<PlanetMemoryLab>();
            PlanetImplementationLabController controller = Object.FindFirstObjectByType<PlanetImplementationLabController>();

            Assert.IsNotNull(memoryLab);
            Assert.IsNotNull(controller);

            Assert.DoesNotThrow(memoryLab.RunStressLow);
            Assert.AreEqual(0, controller.ResourceRegistry.LiveResourceCount);
            Assert.AreEqual("Stress Low finished.", memoryLab.LastAction);

            memoryLab.CaptureUnityMemorySnapshot();
            Assert.AreEqual(PlanetLabDiagnosticSeverity.Warning, memoryLab.LastDiagnostic.severity);
        }
    }
}
