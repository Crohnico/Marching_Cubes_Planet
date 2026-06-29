using System;
using UnityEngine.Serialization;

namespace MarchingCubesPlanet.MarchingCubes
{
    [Serializable]
    public struct PlanetMarchingCubesSettings
    {
        public const int DefaultTemporaryOutputTriangleCapacity = 1000000;

        [FormerlySerializedAs("surfaceRange")]
        public PlanetMarchingCubesChunkRange chunkRange;
        [FormerlySerializedAs("maxPlanetSurfaceTriangles")]
        [FormerlySerializedAs("maxValidationTriangles")]
        public int temporaryOutputTriangleCapacity;

        public static PlanetMarchingCubesSettings Default()
        {
            return new PlanetMarchingCubesSettings
            {
                chunkRange = PlanetMarchingCubesChunkRange.Default(),
                temporaryOutputTriangleCapacity = DefaultTemporaryOutputTriangleCapacity
            };
        }

        public int TemporaryOutputVertexCapacity => temporaryOutputTriangleCapacity * 3;

        public long EstimatedVertexBytes => (long)TemporaryOutputVertexCapacity * PlanetMarchingCubesVertex.Stride;

        public void EnsureDefaults()
        {
            PlanetMarchingCubesSettings defaults = Default();
            if (chunkRange.chunkSize <= 0 && chunkRange.cellSizeGrid <= 0 && chunkRange.safetyMargin == 0 && chunkRange.maxCandidateChunks == 0)
            {
                chunkRange = defaults.chunkRange;
            }

            chunkRange.EnsureDefaults();

            if (temporaryOutputTriangleCapacity <= 0)
            {
                temporaryOutputTriangleCapacity = defaults.temporaryOutputTriangleCapacity;
            }
        }

        public bool Validate(out string message)
        {
            if (!chunkRange.Validate(out message))
            {
                return false;
            }

            if (temporaryOutputTriangleCapacity <= 0)
            {
                message = "temporaryOutputTriangleCapacity must be greater than zero.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        public override string ToString()
        {
            return chunkRange +
                   "\ntemporaryOutputTriangleCapacity=" + temporaryOutputTriangleCapacity +
                   "\ntemporaryOutputVertexCapacity=" + TemporaryOutputVertexCapacity +
                   "\nestimatedVertexBytes=" + EstimatedVertexBytes;
        }
    }
}
