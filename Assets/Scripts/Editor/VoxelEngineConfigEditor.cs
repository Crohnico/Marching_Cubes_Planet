using UnityEditor;
using UnityEngine;
using MarchingCubesPlanet.VoxelEngine.Data;

namespace MarchingCubesPlanet.VoxelEngine.Editor
{
    [CustomEditor(typeof(VoxelEngineConfig))]
    public sealed class VoxelEngineConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            serializedObject.Update();
            SerializedProperty chunkSize = serializedObject.FindProperty("chunkSize");
            SerializedProperty cellSizes = serializedObject.FindProperty("cellSizes");
            SerializedProperty lodDistances = serializedObject.FindProperty("lodDistances");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Resolved Segment LODs", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Distances are world units from PlayerChunkTracker. Segment meshes are baked on a uniform grid for each configured cell size.", MessageType.Info);
            int cellSizeCount = cellSizes != null ? cellSizes.arraySize : 0;
            int distanceCount = lodDistances != null ? lodDistances.arraySize : 0;
            if (cellSizeCount != distanceCount)
            {
                EditorGUILayout.HelpBox("Cell Sizes and LOD Distances should have the same length. Only valid pairs are used.", MessageType.Warning);
            }

            cellSizeCount = Mathf.Min(cellSizeCount, distanceCount);
            for (int i = 0; i < cellSizeCount; i++)
            {
                int cellSize = Mathf.Max(1, cellSizes.GetArrayElementAtIndex(i).intValue);
                float maxDistance = Mathf.Max(0f, lodDistances.GetArrayElementAtIndex(i).floatValue);
                int normalizedCellSize = NormalizeCellSize(chunkSize.vector3IntValue, cellSize);
                EditorGUILayout.LabelField(
                    $"<= {maxDistance:0.##}u",
                    $"Cell {normalizedCellSize}");
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static int NormalizeCellSize(Vector3Int chunkSize, int cellSize)
        {
            int requested = Mathf.Min(
                Mathf.Max(1, cellSize),
                Mathf.Min(Mathf.Max(1, chunkSize.x), Mathf.Min(Mathf.Max(1, chunkSize.y), Mathf.Max(1, chunkSize.z))));
            for (int size = requested; size >= 1; size--)
            {
                if (chunkSize.x % size == 0
                    && chunkSize.y % size == 0
                    && chunkSize.z % size == 0)
                {
                    return size;
                }
            }

            return 1;
        }
    }
}
