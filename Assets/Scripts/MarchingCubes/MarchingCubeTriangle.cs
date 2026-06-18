namespace MarchingCubesPlanet.MarchingCubes
{
    public readonly struct MarchingCubeTriangle
    {
        public MarchingCubeTriangle(MarchingCubeEdge edge0, MarchingCubeEdge edge1, MarchingCubeEdge edge2)
        {
            Edge0 = edge0;
            Edge1 = edge1;
            Edge2 = edge2;
        }

        public MarchingCubeEdge Edge0 { get; }
        public MarchingCubeEdge Edge1 { get; }
        public MarchingCubeEdge Edge2 { get; }
    }
}
