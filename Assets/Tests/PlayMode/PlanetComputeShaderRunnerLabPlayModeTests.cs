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

            PlanetComputeLabResultView resultView =
                Object.FindFirstObjectByType<PlanetComputeLabResultView>();

            Assert.IsNotNull(resultView);
            Assert.IsNotNull(resultView.MeshFilter);
            Assert.IsNotNull(resultView.MeshRenderer);
            Assert.IsNotNull(resultView.MeshFilter.sharedMesh);
            Assert.AreEqual(4, resultView.MeshFilter.sharedMesh.vertexCount);
            Assert.IsNotNull(resultView.MeshRenderer.sharedMaterial);

            Material resultMaterial = resultView.MeshRenderer.sharedMaterial;
            bool hasOutputTexture =
                resultMaterial.HasProperty("_BaseMap") &&
                resultMaterial.GetTexture("_BaseMap") == computeLab.OutputTexture;

            if (!hasOutputTexture && resultMaterial.HasProperty("_MainTex"))
            {
                hasOutputTexture = resultMaterial.GetTexture("_MainTex") == computeLab.OutputTexture;
            }

            Assert.IsTrue(hasOutputTexture);

            Assert.IsNotNull(controller.ResourceRegistry);
            controller.ResourceRegistry.RecalculateLiveTotals();
            Assert.AreEqual(1, controller.ResourceRegistry.LiveRenderTextures);
            Assert.AreEqual(1, controller.ResourceRegistry.LiveRuntimeMeshes);

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
        public IEnumerator VisibleOutputButtonsCreateRenderTextureAndMeshRoutes()
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

            Assert.DoesNotThrow(computeLab.CreateMeshDebugTest);
            PlanetComputeLabResultView meshOnlyView =
                Object.FindFirstObjectByType<PlanetComputeLabResultView>();
            Assert.IsNotNull(meshOnlyView);
            Assert.IsNotNull(meshOnlyView.MeshFilter.sharedMesh);
            Assert.AreEqual(4, meshOnlyView.MeshFilter.sharedMesh.vertexCount);
            controller.ResourceRegistry.RecalculateLiveTotals();
            Assert.AreEqual(0, controller.ResourceRegistry.LiveRenderTextures);
            Assert.AreEqual(1, controller.ResourceRegistry.LiveRuntimeMeshes);
            Assert.DoesNotThrow(computeLab.ReleaseModule);

            Assert.DoesNotThrow(computeLab.CreateVisibleOutputDebugTest);
            PlanetComputeLabResultView visibleOutputView =
                Object.FindFirstObjectByType<PlanetComputeLabResultView>();
            Assert.IsNotNull(visibleOutputView);
            Assert.IsNotNull(computeLab.OutputTexture);
            Assert.IsNotNull(visibleOutputView.MeshRenderer.sharedMaterial);
            Assert.IsTrue(MaterialUsesTexture(visibleOutputView.MeshRenderer.sharedMaterial, computeLab.OutputTexture));
            controller.ResourceRegistry.RecalculateLiveTotals();
            Assert.AreEqual(1, controller.ResourceRegistry.LiveRenderTextures);
            Assert.AreEqual(1, controller.ResourceRegistry.LiveRuntimeMeshes);
            Assert.DoesNotThrow(computeLab.ReleaseModule);
            Assert.AreEqual(0, controller.ResourceRegistry.LiveResourceCount);
        }

        private static bool MaterialUsesTexture(Material material, Texture texture)
        {
            if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") == texture)
            {
                return true;
            }

            return material.HasProperty("_MainTex") && material.GetTexture("_MainTex") == texture;
        }
    }
}
