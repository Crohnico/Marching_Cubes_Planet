using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MarchingCubesPlanet.Preview.Editor
{
    [CustomEditor(typeof(PlanetRecipePayloadPreview))]
    public sealed class PlanetRecipePayloadPreviewEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PlanetRecipePayloadPreview preview = (PlanetRecipePayloadPreview)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Derived Payload", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("WorldRadius", preview.DerivedWorldRadius.ToString("0.###"));
            EditorGUILayout.LabelField("IsoLevel", preview.IsoLevel.ToString("0.###"));
            EditorGUILayout.LabelField("surface radius", preview.DerivedSurfaceRadius.ToString("0.###"));
            EditorGUILayout.LabelField("PlanetWorldCenter", preview.TransformPlanetWorldCenter.ToString("0.###"));
            EditorGUILayout.LabelField("requested triangles", preview.RequestedTrianglePayload.ToString());
            EditorGUILayout.LabelField("icosphere frequency", preview.DerivedGeodesicFrequency.ToString());
            EditorGUILayout.LabelField("triangles", preview.DerivedTriangleCount.ToString());
            EditorGUILayout.LabelField("vertices", preview.DerivedVertexCount.ToString());
            EditorGUILayout.LabelField("indices", preview.DerivedIndexCount.ToString());
            EditorGUILayout.LabelField("mesh live", preview.HasLiveMesh ? "yes" : "no");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Payload Presets", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Payload 126k"))
                {
                    Apply(preview, p => p.ApplyPayload126k());
                }

                if (GUILayout.Button("Apply Payload 250k"))
                {
                    Apply(preview, p => p.ApplyPayload250k());
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Payload 500k"))
                {
                    Apply(preview, p => p.ApplyPayload500k());
                }

                if (GUILayout.Button("Apply Payload 1M"))
                {
                    Apply(preview, p => p.ApplyPayload1M());
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Payload 2M"))
                {
                    Apply(preview, p => p.ApplyPayload2M());
                }

                if (GUILayout.Button("Apply Payload 5M"))
                {
                    Apply(preview, p => p.ApplyPayload5M());
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Commands", EditorStyles.boldLabel);

            if (GUILayout.Button("Generate"))
            {
                Apply(preview, p => p.Generate());
            }

            if (GUILayout.Button("Release"))
            {
                Apply(preview, p => p.Release());
            }

            if (GUILayout.Button("Reset Demo Recipe"))
            {
                Apply(preview, p => p.ResetDemoRecipe());
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Last Diagnostic", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(preview.LastDiagnostic ?? string.Empty, MessageType.Info);
        }

        private static void Apply(PlanetRecipePayloadPreview preview, System.Action<PlanetRecipePayloadPreview> action)
        {
            Undo.RecordObject(preview, "Planet Recipe Payload Preview");
            action(preview);
            EditorUtility.SetDirty(preview);

            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(preview.gameObject.scene);
            }
        }
    }
}
