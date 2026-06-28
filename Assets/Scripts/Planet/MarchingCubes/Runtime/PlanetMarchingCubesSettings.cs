using System;
using MarchingCubesPlanet.Coordinates;
using UnityEngine.Serialization;

namespace MarchingCubesPlanet.MarchingCubes
{
    [Serializable]
    public struct PlanetMarchingCubesSettings
    {
        public const int DefaultMaxPlanetSurfaceTriangles = 1000000;

        public PlanetMarchingCubesSurfaceRange surfaceRange;
        [FormerlySerializedAs("maxValidationTriangles")]
        public int maxPlanetSurfaceTriangles;

        public static PlanetMarchingCubesSettings Default()
        {
            return new PlanetMarchingCubesSettings
            {
                surfaceRange = PlanetMarchingCubesSurfaceRange.Default(),
                maxPlanetSurfaceTriangles = DefaultMaxPlanetSurfaceTriangles
            };
        }

        public int MaxPlanetSurfaceVertices => maxPlanetSurfaceTriangles * 3;

        public long EstimatedVertexBytes => (long)MaxPlanetSurfaceVertices * PlanetMarchingCubesVertex.Stride;

        public void EnsureDefaults()
        {
            PlanetMarchingCubesSettings defaults = Default();
            if (surfaceRange.radialCubeCount <= 0 || surfaceRange.faceResolution <= 0)
            {
                surfaceRange = defaults.surfaceRange;
            }

            if (maxPlanetSurfaceTriangles <= 0)
            {
                maxPlanetSurfaceTriangles = defaults.maxPlanetSurfaceTriangles;
            }
        }

        public void EnsureSurfaceRangeCoversRecipe(in PlanetRecipe recipe)
        {
            EnsureDefaults();
            surfaceRange.ExpandToCover(in recipe);
        }

        public bool Validate(out string message)
        {
            if (!surfaceRange.Validate(out message))
            {
                return false;
            }

            if (maxPlanetSurfaceTriangles <= 0)
            {
                message = "maxPlanetSurfaceTriangles must be greater than zero.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        public override string ToString()
        {
            return surfaceRange +
                   "\nmaxPlanetSurfaceTriangles=" + maxPlanetSurfaceTriangles +
                   "\nmaxPlanetSurfaceVertices=" + MaxPlanetSurfaceVertices +
                   "\nestimatedVertexBytes=" + EstimatedVertexBytes;
        }
    }
}
