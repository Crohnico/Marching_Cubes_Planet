using MarchingCubesPlanet.VoxelEngine.Runtime;
using UnityEditor;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Editor
{
    [CustomEditor(typeof(StellarSystem))]
    public sealed class StellarSystemEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            StellarSystem system = (StellarSystem)target;
            if (GUILayout.Button("Clear System Data"))
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "Clear System Data",
                    $"Delete persistent data at {system.SystemDataUrl}?",
                    "Clear",
                    "Cancel");
                if (confirmed)
                {
                    system.ClearSystemData();
                }
            }
        }
    }
}
