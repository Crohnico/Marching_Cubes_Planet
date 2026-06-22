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
            EditorGUILayout.LabelField("Runtime State", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Declared Chunks", manager.DeclaredChunkCount);
                EditorGUILayout.IntField("Desired Chunks", manager.DesiredChunkCount);
                EditorGUILayout.IntField("Active Chunks", manager.ActiveChunkCount);
                EditorGUILayout.IntField("Visible Chunks", manager.VisibleChunkCount);
                EditorGUILayout.IntField("Combined Vertices", manager.CombinedVertexCount);
                EditorGUILayout.IntField("Combined Triangles", manager.CombinedTriangleCount);
                EditorGUILayout.Toggle("Far Generated", manager.FarGenerated);
                EditorGUILayout.Toggle("Render Culling Active", manager.IsRenderCullingActive);
                EditorGUILayout.Toggle("Far Bridge Active", manager.IsFarBridgeActive);
                EditorGUILayout.Toggle("Near Rendering Active", manager.IsNearCombinedRenderingActive);
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
