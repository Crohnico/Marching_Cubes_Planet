using UnityEngine;

namespace MarchingCubesPlanet.Preview
{
    public readonly struct PlanetSpherePayloadBuildResult
    {
        public PlanetSpherePayloadBuildResult(int vertexCount, int triangleCount, int indexCount, Bounds localBounds)
        {
            VertexCount = vertexCount;
            TriangleCount = triangleCount;
            IndexCount = indexCount;
            LocalBounds = localBounds;
        }

        public int VertexCount { get; }
        public int TriangleCount { get; }
        public int IndexCount { get; }
        public Bounds LocalBounds { get; }
    }
}
