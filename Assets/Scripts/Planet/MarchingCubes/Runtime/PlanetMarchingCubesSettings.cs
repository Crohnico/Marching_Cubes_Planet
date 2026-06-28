using System;

namespace MarchingCubesPlanet.MarchingCubes
{
    [Serializable]
    public struct PlanetMarchingCubesSettings
    {
        public PlanetMarchingCubesRange range;
        public int maxValidationTriangles;

        public static PlanetMarchingCubesSettings Default()
        {
            return new PlanetMarchingCubesSettings
            {
                range = PlanetMarchingCubesRange.Default(),
                maxValidationTriangles = 65536
            };
        }

        public int MaxValidationVertices => maxValidationTriangles * 3;

        public long EstimatedVertexBytes => (long)MaxValidationVertices * PlanetMarchingCubesVertex.Stride;

        public bool Validate(out string message)
        {
            if (!range.Validate(out message))
            {
                return false;
            }

            if (maxValidationTriangles <= 0)
            {
                message = "maxValidationTriangles must be greater than zero.";
                return false;
            }

            if (range.CubeCount > int.MaxValue)
            {
                message = "The validation patch cube count must fit in a compute dispatch int.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        public override string ToString()
        {
            return range +
                   "\nmaxValidationTriangles=" + maxValidationTriangles +
                   "\nmaxValidationVertices=" + MaxValidationVertices +
                   "\nestimatedVertexBytes=" + EstimatedVertexBytes;
        }
    }
}
