using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace MarchingCubesPlanet.MarchingCubes
{
    [Serializable]
    public struct PlanetMarchingCubesPaintSettings
    {
        public const int DefaultMaxPaintedTriangles = 1000000;

        public PlanetMarchingCubesPaintColorMode colorMode;
        public int maxPaintedTriangles;
        [FormerlySerializedAs("solidDebugColor")]
        public Color solidColor;

        public static PlanetMarchingCubesPaintSettings Default()
        {
            return new PlanetMarchingCubesPaintSettings
            {
                colorMode = PlanetMarchingCubesPaintColorMode.PlanetSurfaceAtlas,
                maxPaintedTriangles = DefaultMaxPaintedTriangles,
                solidColor = new Color(0.15f, 0.9f, 1f, 1f)
            };
        }

        public int MaxPaintedVertices => maxPaintedTriangles * 3;

        public bool Validate(out string message)
        {
            if (!Enum.IsDefined(typeof(PlanetMarchingCubesPaintColorMode), colorMode))
            {
                message = "colorMode must be a defined PlanetMarchingCubesPaintColorMode value.";
                return false;
            }

            if (maxPaintedTriangles <= 0)
            {
                message = "maxPaintedTriangles must be greater than zero.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        public override string ToString()
        {
            return "colorMode=" + colorMode +
                   "\nmaxPaintedTriangles=" + maxPaintedTriangles +
                   "\nmaxPaintedVertices=" + MaxPaintedVertices;
        }
    }
}
