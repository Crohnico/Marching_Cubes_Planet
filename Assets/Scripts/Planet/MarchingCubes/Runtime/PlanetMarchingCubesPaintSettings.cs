using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace MarchingCubesPlanet.MarchingCubes
{
    [Serializable]
    public struct PlanetMarchingCubesPaintSettings
    {
        public const int DefaultMeshVertexCapacity = 3000000;

        public PlanetMarchingCubesPaintColorMode colorMode;
        public int meshVertexCapacity;
        [FormerlySerializedAs("solidDebugColor")]
        public Color solidColor;

        public static PlanetMarchingCubesPaintSettings Default()
        {
            return new PlanetMarchingCubesPaintSettings
            {
                colorMode = PlanetMarchingCubesPaintColorMode.PlanetSurfaceAtlas,
                meshVertexCapacity = DefaultMeshVertexCapacity,
                solidColor = new Color(0.15f, 0.9f, 1f, 1f)
            };
        }

        public bool Validate(out string message)
        {
            if (!Enum.IsDefined(typeof(PlanetMarchingCubesPaintColorMode), colorMode))
            {
                message = "colorMode must be a defined PlanetMarchingCubesPaintColorMode value.";
                return false;
            }

            if (meshVertexCapacity <= 0)
            {
                message = "meshVertexCapacity must be greater than zero.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        public override string ToString()
        {
            return "colorMode=" + colorMode +
                   "\nmeshVertexCapacity=" + meshVertexCapacity;
        }
    }
}
