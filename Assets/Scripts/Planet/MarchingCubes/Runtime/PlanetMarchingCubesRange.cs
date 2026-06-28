using System;

namespace MarchingCubesPlanet.MarchingCubes
{
    [Serializable]
    public struct PlanetMarchingCubesRange
    {
        public int radialStartOffset;
        public int radialCubeCount;
        public int tangentHalfExtent;
        public int tangentCubeCount;
        public float cubeSizeGrid;

        public static PlanetMarchingCubesRange Default()
        {
            return new PlanetMarchingCubesRange
            {
                radialStartOffset = -512,
                radialCubeCount = 1024,
                tangentHalfExtent = 8,
                tangentCubeCount = 16,
                cubeSizeGrid = 1f
            };
        }

        public long CubeCount => (long)radialCubeCount * tangentCubeCount * tangentCubeCount;

        public bool Validate(out string message)
        {
            if (radialCubeCount <= 0)
            {
                message = "radialCubeCount must be greater than zero.";
                return false;
            }

            if (tangentHalfExtent <= 0)
            {
                message = "tangentHalfExtent must be greater than zero.";
                return false;
            }

            if (tangentCubeCount <= 0)
            {
                message = "tangentCubeCount must be greater than zero.";
                return false;
            }

            if (cubeSizeGrid != 1f)
            {
                message = "cubeSizeGrid must remain 1 for the validation patch.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        public override string ToString()
        {
            return "radialStartOffset=" + radialStartOffset +
                   "\nradialCubeCount=" + radialCubeCount +
                   "\ntangentHalfExtent=" + tangentHalfExtent +
                   "\ntangentCubeCount=" + tangentCubeCount +
                   "\ncubeSizeGrid=" + cubeSizeGrid +
                   "\ncubeCount=" + CubeCount;
        }
    }
}
