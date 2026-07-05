using System;
using UnityEngine;

namespace MarchingCubesPlanet.TrianglePools
{
    [Serializable]
    public struct PlanetTriangleGpuVertex
    {
        public const int Stride = 64;

        public Vector4 positionAndActive;
        public Vector4 normalAndFlags;
        public Vector4 uvAndMaterial;
        public Vector4 color;

        public PlanetTriangleGpuVertex(
            Vector3 positionWorld,
            Vector3 normalWorld,
            Vector2 uv,
            Color32 vertexColor,
            bool active)
        {
            positionAndActive = new Vector4(positionWorld.x, positionWorld.y, positionWorld.z, active ? 1f : 0f);
            normalAndFlags = new Vector4(normalWorld.x, normalWorld.y, normalWorld.z, 0f);
            uvAndMaterial = new Vector4(uv.x, uv.y, 0f, 0f);
            color = new Vector4(
                vertexColor.r / 255f,
                vertexColor.g / 255f,
                vertexColor.b / 255f,
                vertexColor.a / 255f);
        }
    }
}
