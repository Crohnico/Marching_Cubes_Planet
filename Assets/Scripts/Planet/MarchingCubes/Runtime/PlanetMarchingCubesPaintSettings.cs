using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace MarchingCubesPlanet.MarchingCubes
{
    [Serializable]
    public struct PlanetMarchingCubesPaintSettings
    {
        public const int DefaultMeshTriangleCapacity = 1000000;

        public PlanetMarchingCubesPaintColorMode colorMode;
        [FormerlySerializedAs("maxPaintedTriangles")]
        public int meshTriangleCapacity;
        [FormerlySerializedAs("solidDebugColor")]
        public Color solidColor;

        public static PlanetMarchingCubesPaintSettings Default()
        {
            return new PlanetMarchingCubesPaintSettings
            {
                colorMode = PlanetMarchingCubesPaintColorMode.PlanetSurfaceAtlas,
                meshTriangleCapacity = DefaultMeshTriangleCapacity,
                solidColor = new Color(0.15f, 0.9f, 1f, 1f)
            };
        }

        public int MeshVertexCapacity => meshTriangleCapacity * 3;

        public bool Validate(out string message)
        {
            if (!Enum.IsDefined(typeof(PlanetMarchingCubesPaintColorMode), colorMode))
            {
                message = "colorMode must be a defined PlanetMarchingCubesPaintColorMode value.";
                return false;
            }

            if (meshTriangleCapacity <= 0)
            {
                message = "meshTriangleCapacity must be greater than zero.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        public override string ToString()
        {
            return "colorMode=" + colorMode +
                   "\nmeshTriangleCapacity=" + meshTriangleCapacity +
                   "\nmeshVertexCapacity=" + MeshVertexCapacity;
        }
    }
}
