using System;
using MarchingCubesPlanet.Lab;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MarchingCubesPlanet.Lab.Editor
{
    public static class PlanetImplementationLabValidationRunner
    {
        private const string ScenePath = "Assets/Scenes/PlanetImplementationLab.unity";

        [MenuItem("Tools/Planet Lab/Run Basic Lab Validation")]
        public static void RunBasicValidation()
        {
            ValidatePlayModeContext();

            PlanetImplementationLabController controller = UnityEngine.Object.FindFirstObjectByType<PlanetImplementationLabController>();
            if (controller == null)
            {
                throw new InvalidOperationException("PlanetImplementationLabController was not found in the active scene.");
            }

            if (!controller.ValidateScene())
            {
                throw new InvalidOperationException("Validate Scene failed: " + controller.LastDiagnostic.title);
            }

            controller.InitLab();
            controller.InitLab();
            controller.CaptureMetrics();
            controller.ReleaseAll();
            controller.ReleaseAll();
            controller.RunSmokeTest();
            controller.RunStressLow();
            controller.RunStressExtreme();
            controller.ForceGcCheck();
            controller.ForceGpuRelease();
            controller.ResetLabState();

            PlanetLabResourceRegistry registry = controller.ResourceRegistry;
            if (registry == null)
            {
                throw new InvalidOperationException("PlanetLabResourceRegistry is not assigned after validation.");
            }

            if (registry.LiveResourceCount != 0)
            {
                throw new InvalidOperationException("Release All left live resources: " + registry.LiveResourceCount);
            }

            Debug.Log(
                "PlanetImplementationLab basic validation OK. Last action: " +
                controller.LastAction +
                ". Diagnostic: " +
                controller.LastDiagnostic.title,
                controller);
        }

        private static void ValidatePlayModeContext()
        {
            if (!Application.isPlaying)
            {
                throw new InvalidOperationException("Run Basic Lab Validation only runs in Play Mode.");
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.path != ScenePath)
            {
                throw new InvalidOperationException(
                    "Run Basic Lab Validation in Play Mode requires the active scene to be " +
                    ScenePath +
                    ". Current active scene: " +
                    activeScene.path);
            }
        }
    }
}
