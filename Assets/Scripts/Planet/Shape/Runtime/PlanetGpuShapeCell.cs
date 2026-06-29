using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MarchingCubesPlanet.Shape
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct PlanetGpuShapeCell
    {
        public const int Stride = 32;

        public Vector4 directionAndFlag;
        public Vector4 offsetRoughnessHash;

        public Vector3 Direction => new Vector3(directionAndFlag.x, directionAndFlag.y, directionAndFlag.z);
        public bool IsContinent => directionAndFlag.w >= 0.5f;
        public float BaseOffset => offsetRoughnessHash.x;
        public float RoughnessModifier => offsetRoughnessHash.y;
        public float HeightModifier => offsetRoughnessHash.z;

        public static PlanetGpuShapeCell Create(
            Vector3 direction,
            bool isContinent,
            float baseOffset,
            float roughnessModifier,
            float heightModifier)
        {
            Vector3 normalizedDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.up;
            return new PlanetGpuShapeCell
            {
                directionAndFlag = new Vector4(
                    normalizedDirection.x,
                    normalizedDirection.y,
                    normalizedDirection.z,
                    isContinent ? 1f : 0f),
                offsetRoughnessHash = new Vector4(baseOffset, roughnessModifier, heightModifier, 0f)
            };
        }
    }
}
