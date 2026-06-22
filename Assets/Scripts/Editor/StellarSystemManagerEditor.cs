using MarchingCubesPlanet.VoxelEngine.Runtime;
using UnityEditor;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Editor
{
    [CustomEditor(typeof(StellarSystemManager))]
    public sealed class StellarSystemManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            StellarSystemManager manager = (StellarSystemManager)target;

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Planets", manager.PlanetManagers != null ? manager.PlanetManagers.Length : 0);
            }

            if (GUILayout.Button("Refresh Planet List"))
            {
                Undo.RecordObject(manager, "Refresh Planet List");
                manager.RefreshPlanetManagers();
                manager.SortPlanetsByDistanceToPlayer();
                EditorUtility.SetDirty(manager);
            }

            if (GUILayout.Button("Sort By Player Distance"))
            {
                Undo.RecordObject(manager, "Sort Planets By Player Distance");
                manager.SortPlanetsByDistanceToPlayer();
                EditorUtility.SetDirty(manager);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Clear System Data"))
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "Clear System Data",
                    $"Delete persistent data for system '{manager.SystemId}'?",
                    "Delete",
                    "Cancel");
                if (confirmed)
                {
                    manager.DeleteSystemData();
                }
            }
        }
    }
}
