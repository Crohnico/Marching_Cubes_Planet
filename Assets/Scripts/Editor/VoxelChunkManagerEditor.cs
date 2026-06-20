using MarchingCubesPlanet.VoxelEngine.Runtime;
using UnityEditor;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Editor
{
    [CustomEditor(typeof(VoxelChunkManager))]
    public sealed class VoxelChunkManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            VoxelChunkManager manager = (VoxelChunkManager)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime State", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Declared Chunks", manager.DeclaredChunkCount);
                EditorGUILayout.IntField("Desired Chunks", manager.DesiredChunkCount);
                EditorGUILayout.IntField("Queued Builds", manager.QueuedChunkBuildCount);
                EditorGUILayout.IntField("Pending Builds", manager.PendingChunkBuildCount);
                EditorGUILayout.IntField("Active Chunks", manager.ActiveChunkCount);
                EditorGUILayout.IntField("Visible Chunks", manager.VisibleChunkCount);
                EditorGUILayout.IntField("Combined Vertices", manager.CombinedVertexCount);
                EditorGUILayout.IntField("Combined Triangles", manager.CombinedTriangleCount);
                EditorGUILayout.Toggle("Render Culling Active", manager.IsRenderCullingActive);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Generate"))
            {
                manager.Generate();
            }

            if (GUILayout.Button("Clear Generated Chunks"))
            {
                manager.ClearGeneratedChunks();
            }
        }
    }
}
