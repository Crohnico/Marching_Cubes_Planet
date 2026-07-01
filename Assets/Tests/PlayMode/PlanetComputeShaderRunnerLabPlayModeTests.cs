using System.Collections;
using MarchingCubesPlanet.Lab;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MarchingCubesPlanet.Lab.Tests
{
    public sealed class PlanetComputeShaderRunnerLabPlayModeTests
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
        public IEnumerator ComputeShaderRunnerLabExistsInScene()
        {
            yield return null;

            PlanetComputeShaderRunnerLab computeLab =
                Object.FindFirstObjectByType<PlanetComputeShaderRunnerLab>();

            Assert.IsNotNull(computeLab);
        }

        [UnityTest]
        public IEnumerator ValidateModuleDoesNotThrow()
        {
            yield return null;

            PlanetComputeShaderRunnerLab computeLab =
                Object.FindFirstObjectByType<PlanetComputeShaderRunnerLab>();

            Assert.IsNotNull(computeLab);
            Assert.DoesNotThrow(() => computeLab.ValidateModule());
        }

        [UnityTest]
        public IEnumerator InitDispatchAndReleaseLeaveRegistryClean()
        {
            yield return null;

            PlanetComputeShaderRunnerLab computeLab =
                Object.FindFirstObjectByType<PlanetComputeShaderRunnerLab>();
            PlanetImplementationLabController controller =
                Object.FindFirstObjectByType<PlanetImplementationLabController>();

            Assert.IsNotNull(computeLab);
            Assert.IsNotNull(controller);

            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.DoesNotThrow(computeLab.ReleaseModule);
                yield break;
            }

            Assert.DoesNotThrow(computeLab.InitModule);
            Assert.DoesNotThrow(computeLab.DispatchOnce);
            Assert.IsNotNull(computeLab.OutputTexture);

            Assert.IsNotNull(controller.ResourceRegistry);
            controller.ResourceRegistry.RecalculateLiveTotals();
            Assert.AreEqual(1, controller.ResourceRegistry.LiveRenderTextures);
            Assert.AreEqual(0, controller.ResourceRegistry.LiveRuntimeMeshes);

            Assert.DoesNotThrow(computeLab.ReleaseModule);
            Assert.DoesNotThrow(computeLab.ReleaseModule);

            Assert.AreEqual(0, controller.ResourceRegistry.LiveResourceCount);
        }

        [UnityTest]
        public IEnumerator InitReleaseInitWorks()
        {
            yield return null;

            PlanetComputeShaderRunnerLab computeLab =
                Object.FindFirstObjectByType<PlanetComputeShaderRunnerLab>();

            Assert.IsNotNull(computeLab);

            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.DoesNotThrow(computeLab.ReleaseModule);
                yield break;
            }

            Assert.DoesNotThrow(computeLab.InitModule);
            Assert.DoesNotThrow(computeLab.ReleaseModule);
            Assert.DoesNotThrow(computeLab.InitModule);
            Assert.DoesNotThrow(computeLab.ReleaseModule);
        }

        [UnityTest]
        public IEnumerator RenderTextureDebugButtonCreatesOnlyRenderTextureRoute()
        {
            yield return null;

            PlanetComputeShaderRunnerLab computeLab =
                Object.FindFirstObjectByType<PlanetComputeShaderRunnerLab>();
            PlanetImplementationLabController controller =
                Object.FindFirstObjectByType<PlanetImplementationLabController>();

            Assert.IsNotNull(computeLab);
            Assert.IsNotNull(controller);

            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.DoesNotThrow(computeLab.ReleaseModule);
                yield break;
            }

            Assert.DoesNotThrow(computeLab.CreateRenderTextureDebugTest);
            Assert.IsNotNull(computeLab.OutputTexture);
            controller.ResourceRegistry.RecalculateLiveTotals();
            Assert.AreEqual(1, controller.ResourceRegistry.LiveRenderTextures);
            Assert.AreEqual(0, controller.ResourceRegistry.LiveRuntimeMeshes);
            Assert.DoesNotThrow(computeLab.ReleaseModule);
            Assert.AreEqual(0, controller.ResourceRegistry.LiveResourceCount);
        }
    }
}
