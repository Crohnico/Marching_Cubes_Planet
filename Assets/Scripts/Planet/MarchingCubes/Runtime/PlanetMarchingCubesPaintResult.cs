namespace MarchingCubesPlanet.MarchingCubes
{
    public readonly struct PlanetMarchingCubesPaintResult
    {
        public PlanetMarchingCubesPaintResult(
            int sourceTriangleCount,
            int paintedTriangleCount,
            int paintedVertexCount,
            long meshEstimatedBytes,
            PlanetMarchingCubesPaintColorMode colorMode)
            : this(
                sourceTriangleCount,
                paintedTriangleCount,
                paintedVertexCount,
                meshEstimatedBytes,
                0,
                0,
                0L,
                colorMode)
        {
        }

        public PlanetMarchingCubesPaintResult(
            int sourceTriangleCount,
            int paintedTriangleCount,
            int paintedVertexCount,
            long meshEstimatedBytes,
            int waterTriangleCount,
            int waterVertexCount,
            long waterMeshEstimatedBytes,
            PlanetMarchingCubesPaintColorMode colorMode)
        {
            SourceTriangleCount = sourceTriangleCount;
            PaintedTriangleCount = paintedTriangleCount;
            PaintedVertexCount = paintedVertexCount;
            MeshEstimatedBytes = meshEstimatedBytes;
            WaterTriangleCount = waterTriangleCount;
            WaterVertexCount = waterVertexCount;
            WaterMeshEstimatedBytes = waterMeshEstimatedBytes;
            ColorMode = colorMode;
        }

        public int SourceTriangleCount { get; }
        public int PaintedTriangleCount { get; }
        public int PaintedVertexCount { get; }
        public long MeshEstimatedBytes { get; }
        public int WaterTriangleCount { get; }
        public int WaterVertexCount { get; }
        public long WaterMeshEstimatedBytes { get; }
        public long TotalMeshEstimatedBytes => MeshEstimatedBytes + WaterMeshEstimatedBytes;
        public PlanetMarchingCubesPaintColorMode ColorMode { get; }
        public bool HasVisibleMesh => PaintedTriangleCount > 0 && PaintedVertexCount > 0 ||
                                      WaterTriangleCount > 0 && WaterVertexCount > 0;

        public static long CalculateMeshEstimatedBytes(int vertexCount, int triangleCount)
        {
            if (vertexCount <= 0 || triangleCount <= 0)
            {
                return 0L;
            }

            long vertexBytes = vertexCount * 36L;
            int indexStride = vertexCount > ushort.MaxValue ? 4 : 2;
            long indexBytes = triangleCount * 3L * indexStride;
            return vertexBytes + indexBytes;
        }
    }
}
