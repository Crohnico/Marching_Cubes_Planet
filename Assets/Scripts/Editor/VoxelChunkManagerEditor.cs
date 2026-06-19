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
