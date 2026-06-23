using MarchingCubesPlanet.VoxelEngine.Runtime;
using UnityEditor;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Editor
{
    [CustomEditor(typeof(PlanetManager))]
    public sealed class PlanetManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PlanetManager manager = (PlanetManager)target;

            EditorGUILayout.Space();
            if (GUILayout.Button("Clear Planet Data"))
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "Clear Planet Data",
                    $"Delete persistent data at {manager.GetPlanetStorageUrl()}?",
                    "Clear",
                    "Cancel");
                if (confirmed)
                {
                    manager.ClearPlanetData();
                }
            }
        }
    }
}
