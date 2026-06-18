using MarchingCubesPlanet.Noise;
using UnityEditor;

namespace MarchingCubesPlanet.Editor
{
    [CustomEditor(typeof(NoiseProfile))]
    public sealed class NoiseProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawProperty("enabledNoise", "Activa o desactiva el detalle de ruido del perfil.");
            DrawProperty("amplitude", "Cuanto puede mover la superficie respecto al radio. 0.1 significa hasta un 10% del radio.");
            DrawProperty("frequency", "Tamano del patron de detalle. Bajo = manchas grandes y suaves. Alto = manchas pequenas y repetidas.");

            EditorGUILayout.HelpBox("Despues de modificar este asset, pulsa Generate en el MarchingCubesSphereGenerator para reconstruir la malla.", MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawProperty(string propertyName, string description)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            EditorGUILayout.PropertyField(property);
            EditorGUILayout.HelpBox(description, MessageType.None);
        }
    }
}
