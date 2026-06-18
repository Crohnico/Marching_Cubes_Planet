namespace MarchingCubesPlanet.MarchingCubes
{
    public readonly struct MarchingCubeEdge
    {
        public MarchingCubeEdge(int fromCorner, int toCorner)
        {
            FromCorner = fromCorner;
            ToCorner = toCorner;
        }

        public int FromCorner { get; }
        public int ToCorner { get; }
    }
}
