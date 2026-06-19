using MarchingCubesPlanet.VoxelEngine.Runtime;
using UnityEditor;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Editor
{
    [CustomEditor(typeof(VoxelSphereGenerator))]
    public sealed class VoxelSphereGeneratorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            VoxelSphereGenerator generator = (VoxelSphereGenerator)target;

            EditorGUILayout.Space();
            if (GUILayout.Button("Generate"))
            {
                generator.Generate();
            }
        }
    }
}
