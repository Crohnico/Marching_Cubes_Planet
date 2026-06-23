using Unity.Mathematics;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    internal static class VoxelRuntimeMath
    {
        public static Vector3 ToVector3(int3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        public static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        public static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        public static int CompareInt3(int3 a, int3 b)
        {
            int xComparison = a.x.CompareTo(b.x);
            if (xComparison != 0)
            {
                return xComparison;
            }

            int yComparison = a.y.CompareTo(b.y);
            if (yComparison != 0)
            {
                return yComparison;
            }

            return a.z.CompareTo(b.z);
        }
    }
}
