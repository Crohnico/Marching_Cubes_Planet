using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    internal static class NativeListCopyUtility
    {
        public static void CopyToVector3List(NativeList<float3> source, List<Vector3> destination)
        {
            EnsureListCapacity(destination, source.Length);
            destination.Clear();
            for (int i = 0; i < source.Length; i++)
            {
                destination.Add(VoxelRuntimeMath.ToVector3(source[i]));
            }
        }

        public static void CopyToVector2List(NativeList<float2> source, List<Vector2> destination)
        {
            EnsureListCapacity(destination, source.Length);
            destination.Clear();
            for (int i = 0; i < source.Length; i++)
            {
                float2 value = source[i];
                destination.Add(new Vector2(value.x, value.y));
            }
        }

        public static void CopyToIntList(NativeList<int> source, List<int> destination)
        {
            EnsureListCapacity(destination, source.Length);
            destination.Clear();
            for (int i = 0; i < source.Length; i++)
            {
                destination.Add(source[i]);
            }
        }

        public static void EnsureListCapacity<T>(List<T> list, int requiredCapacity)
        {
            if (list.Capacity < requiredCapacity)
            {
                list.Capacity = requiredCapacity;
            }
        }
    }
}
