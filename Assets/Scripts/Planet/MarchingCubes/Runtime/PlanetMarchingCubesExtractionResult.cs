using System;

namespace MarchingCubesPlanet.MarchingCubes
{
    public sealed class PlanetMarchingCubesExtractionResult
    {
        public static readonly PlanetMarchingCubesExtractionResult Empty =
            new PlanetMarchingCubesExtractionResult(default, Array.Empty<PlanetMarchingCubesVertex>(), 0, 0);

        public PlanetMarchingCubesExtractionResult(
            PlanetMarchingCubesState state,
            PlanetMarchingCubesVertex[] vertices,
            int vertexCount,
            int maxValidationTriangles)
        {
            State = state;
            Vertices = vertices ?? Array.Empty<PlanetMarchingCubesVertex>();
            VertexCount = Math.Max(0, Math.Min(vertexCount, Vertices.Length));
            MaxValidationTriangles = Math.Max(0, maxValidationTriangles);
        }

        public PlanetMarchingCubesState State { get; }
        public PlanetMarchingCubesVertex[] Vertices { get; }
        public int VertexCount { get; }
        public int TriangleCount => VertexCount / 3;
        public int MaxValidationTriangles { get; }
        public bool HasOverflow => State.overflowFlag != 0u;
        public bool HasInvalidCase => State.invalidCaseFlag != 0u;
    }
}
