using System;
using MarchingCubesPlanet.Lab;
using UnityEditor;
using UnityEngine;

namespace MarchingCubesPlanet.Lab.Editor
{
    [CustomEditor(typeof(PlanetComputeShaderRunnerLab))]
    public sealed class PlanetComputeShaderLabEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PlanetComputeShaderRunnerLab lab = (PlanetComputeShaderRunnerLab)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Compute Shader Lab Commands", EditorStyles.boldLabel);

            if (GUILayout.Button("Validate Module"))
            {
                lab.ValidateModule();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Init Module"))
            {
                lab.InitModule();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Create GraphicsBuffer Test"))
            {
                lab.CreateGraphicsBufferTest();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Create ComputeBuffer Test"))
            {
                lab.CreateComputeBufferTest();
                MarkDirty(lab);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Visible Output", EditorStyles.boldLabel);

            if (GUILayout.Button("Create RenderTexture Debug"))
            {
                lab.CreateRenderTextureDebugTest();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Create Mesh Debug"))
            {
                lab.CreateMeshDebugTest();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Create Visible Output Debug"))
            {
                lab.CreateVisibleOutputDebugTest();
                MarkDirty(lab);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Dispatch", EditorStyles.boldLabel);

            if (GUILayout.Button("Dispatch Once"))
            {
                lab.DispatchOnce();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Dispatch 100x"))
            {
                lab.Dispatch100();
                MarkDirty(lab);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Stress", EditorStyles.boldLabel);

            if (GUILayout.Button("Run GraphicsBuffer Stress"))
            {
                RunManualStress(lab, "Run GraphicsBuffer Stress", lab.RunGraphicsBufferStress);
            }

            if (GUILayout.Button("Run ComputeBuffer Stress"))
            {
                RunManualStress(lab, "Run ComputeBuffer Stress", lab.RunComputeBufferStress);
            }

            if (GUILayout.Button("Run Stress Low"))
            {
                RunManualStress(lab, "Run Stress Low", lab.RunStressLow);
            }

            if (GUILayout.Button("Run Stress Medium"))
            {
                RunManualStress(lab, "Run Stress Medium", lab.RunStressMedium);
            }

            if (GUILayout.Button("Run Stress High"))
            {
                RunManualStress(lab, "Run Stress High", lab.RunStressHigh);
            }

            GUI.backgroundColor = new Color(1f, 0.82f, 0.55f);
            if (GUILayout.Button("Run Stress VeryHigh"))
            {
                if (EditorUtility.DisplayDialog(
                        "Run Stress VeryHigh",
                        "VeryHigh creates a 1M element buffer and runs repeated dispatches. Run it only when you are ready to inspect memory and release behavior.",
                        "Run",
                        "Cancel"))
                {
                    RunManualStress(lab, "Run Stress VeryHigh", lab.RunStressVeryHigh);
                }
            }

            GUI.backgroundColor = new Color(1f, 0.62f, 0.55f);
            if (GUILayout.Button("Run Stress Extreme"))
            {
                if (EditorUtility.DisplayDialog(
                        "Run Stress Extreme",
                        "Extreme creates the largest Compute Shader Lab workload for this phase. It must be launched manually and followed by Release validation.",
                        "Run",
                        "Cancel"))
                {
                    RunManualStress(lab, "Run Stress Extreme", lab.RunStressExtreme);
                }
            }

            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Run Comparison"))
            {
                lab.RunComparison();
                MarkDirty(lab);
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Capture Module Metrics"))
            {
                lab.CaptureMetrics();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Release Module"))
            {
                lab.ReleaseModule();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Reset Module State"))
            {
                lab.ResetModuleState();
                MarkDirty(lab);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Last Diagnostic", EditorStyles.boldLabel);
            PlanetLabDiagnostic diagnostic = lab.LastDiagnostic;
            EditorGUILayout.LabelField("Severity", diagnostic.severity.ToString());
            EditorGUILayout.LabelField("Title", diagnostic.title);
            EditorGUILayout.LabelField("Last Manual Evidence", lab.LastManualStressEvidencePath);
            EditorGUILayout.HelpBox(diagnostic.probableCause + "\n\n" + diagnostic.recommendedAction, MessageType.Info);
        }

        private static void RunManualStress(PlanetComputeShaderRunnerLab lab, string actionName, Action action)
        {
            try
            {
                action();
                SaveManualStressEvidence(lab, actionName, true, string.Empty);
            }
            catch (Exception exception)
            {
                SaveManualStressEvidence(
                    lab,
                    actionName,
                    false,
                    exception.GetType().Name + ": " + exception.Message);
                throw;
            }
            finally
            {
                MarkDirty(lab);
                AssetDatabase.Refresh();
            }
        }

        private static void SaveManualStressEvidence(
            PlanetComputeShaderRunnerLab lab,
            string actionName,
            bool executedWithoutException,
            string exception)
        {
            string path = lab.SaveManualStressEvidence(actionName, executedWithoutException, exception);
            Debug.Log("Compute manual stress evidence saved: " + path, lab);
        }

        private static void MarkDirty(UnityEngine.Object targetObject)
        {
            EditorUtility.SetDirty(targetObject);
            if (!Application.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(((Component)targetObject).gameObject.scene);
            }
        }
    }
}
