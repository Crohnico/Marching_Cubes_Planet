using MarchingCubesPlanet.Lab;
using UnityEditor;
using UnityEngine;

namespace MarchingCubesPlanet.Lab.Editor
{
    [CustomEditor(typeof(PlanetRecipeLab))]
    public sealed class PlanetRecipeLabEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PlanetRecipeLab lab = (PlanetRecipeLab)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Recipe Lab Commands", EditorStyles.boldLabel);

            if (GUILayout.Button("Validate Recipe"))
            {
                lab.ValidateRecipe();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Reset Demo Recipe"))
            {
                lab.ResetDemoRecipe();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Show Derived Values"))
            {
                lab.ShowDerivedValues();
                MarkDirty(lab);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Conversions", EditorStyles.boldLabel);

            if (GUILayout.Button("Convert Grid To World"))
            {
                lab.ConvertGridToWorld();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Convert World To Grid"))
            {
                lab.ConvertWorldToGrid();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Run Conversion Smoke Test"))
            {
                lab.RunConversionSmokeTest();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Run Boundary Test"))
            {
                lab.RunBoundaryTest();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Run Invalid Values Test"))
            {
                lab.RunInvalidValuesTest();
                MarkDirty(lab);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Grid Cell Query", EditorStyles.boldLabel);

            if (GUILayout.Button("Run Sphere Cell Query"))
            {
                lab.RunSphereCellQuery();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Change Query Mode"))
            {
                lab.ChangeQueryMode();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Show Query Bounds"))
            {
                lab.ShowQueryBounds();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Clear Query Buffer"))
            {
                lab.ClearQueryBuffer();
                MarkDirty(lab);
            }

            if (GUILayout.Button("Run Query Overflow Test"))
            {
                lab.RunQueryOverflowTest();
                MarkDirty(lab);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Last Diagnostic", EditorStyles.boldLabel);
            PlanetLabDiagnostic diagnostic = lab.LastDiagnostic;
            EditorGUILayout.LabelField("Severity", diagnostic.severity.ToString());
            EditorGUILayout.LabelField("Title", diagnostic.title);
            EditorGUILayout.HelpBox(diagnostic.probableCause + "\n\n" + diagnostic.recommendedAction, MessageType.Info);
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
