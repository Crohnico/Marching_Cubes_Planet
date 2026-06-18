using MarchingCubesPlanet.Generators;
using UnityEditor;
using UnityEngine;

namespace MarchingCubesPlanet.Editor
{
    [CustomEditor(typeof(MarchingCubesSphereGenerator))]
    public sealed class MarchingCubesSphereGeneratorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();

            SerializedProperty profileProperty = serializedObject.FindProperty("noiseProfile");
            SerializedProperty continentsProperty = serializedObject.FindProperty("useContinents");
            bool continentsEnabled = continentsProperty != null && continentsProperty.boolValue;
            if (profileProperty != null && profileProperty.objectReferenceValue == null && !continentsEnabled)
            {
                EditorGUILayout.HelpBox("Sin Noise Profile y sin continentes activos, la esfera se genera perfecta y la seed no cambia nada.", MessageType.Warning);
            }
            else if (profileProperty != null && profileProperty.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Sin Noise Profile asignado, solo se aplicara la capa de continentes.", MessageType.Info);
            }

            GUILayout.Space(8f);
            DrawActionButtons();
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawActionButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate"))
                {
                    ForEachTarget(generator => generator.GenerateSphere());
                }

                if (GUILayout.Button("Clear"))
                {
                    ForEachTarget(generator => generator.ClearGeneratedMesh());
                }
            }
        }

        private void ForEachTarget(System.Action<MarchingCubesSphereGenerator> action)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                MarchingCubesSphereGenerator generator = (MarchingCubesSphereGenerator)targets[i];
                Undo.RecordObject(generator, "Marching Cubes Sphere Generator");
                action.Invoke(generator);
                EditorUtility.SetDirty(generator);
            }
        }
    }
}
