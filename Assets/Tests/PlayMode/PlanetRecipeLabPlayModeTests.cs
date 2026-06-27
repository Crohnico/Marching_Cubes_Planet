using System.Collections;
using MarchingCubesPlanet.Lab;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MarchingCubesPlanet.Lab.Tests
{
    public sealed class PlanetRecipeLabPlayModeTests
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
        public IEnumerator PlanetRecipeLabExistsInScene()
        {
            yield return null;

            PlanetRecipeLab recipeLab = Object.FindFirstObjectByType<PlanetRecipeLab>();

            Assert.IsNotNull(recipeLab);
        }

        [UnityTest]
        public IEnumerator ValidateAndResetDemoRecipeWork()
        {
            yield return null;

            PlanetRecipeLab recipeLab = Object.FindFirstObjectByType<PlanetRecipeLab>();

            Assert.IsNotNull(recipeLab);
            Assert.DoesNotThrow(recipeLab.ResetDemoRecipe);
            Assert.DoesNotThrow(() => recipeLab.ValidateRecipe());
            Assert.AreEqual(1000, recipeLab.Recipe.GridRadius);
            Assert.AreEqual(4f, recipeLab.Recipe.WorldScale);
            Assert.AreEqual(4000f, recipeLab.Recipe.WorldRadius);
            Assert.AreEqual(0f, recipeLab.Recipe.IsoLevel);
        }

        [UnityTest]
        public IEnumerator ConversionButtonsDoNotCreateResources()
        {
            yield return null;

            PlanetRecipeLab recipeLab = Object.FindFirstObjectByType<PlanetRecipeLab>();
            PlanetImplementationLabController controller = Object.FindFirstObjectByType<PlanetImplementationLabController>();

            Assert.IsNotNull(recipeLab);
            Assert.IsNotNull(controller);
            Assert.IsNotNull(controller.ResourceRegistry);

            controller.ReleaseAll();
            Assert.AreEqual(0, controller.ResourceRegistry.LiveResourceCount);

            Assert.DoesNotThrow(recipeLab.ConvertGridToWorld);
            Assert.DoesNotThrow(recipeLab.ConvertWorldToGrid);
            Assert.DoesNotThrow(recipeLab.RunConversionSmokeTest);
            Assert.DoesNotThrow(recipeLab.RunBoundaryTest);
            Assert.DoesNotThrow(recipeLab.RunSphereCellQuery);
            Assert.DoesNotThrow(recipeLab.RunQueryOverflowTest);

            controller.ResourceRegistry.RecalculateLiveTotals();
            Assert.AreEqual(0, controller.ResourceRegistry.LiveResourceCount);
            Assert.IsFalse(recipeLab.HasLiveResources);
        }
    }
}
