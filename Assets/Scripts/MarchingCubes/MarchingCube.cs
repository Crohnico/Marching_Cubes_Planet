using System;

namespace MarchingCubesPlanet.MarchingCubes
{
    public readonly struct MarchingCube
    {
        public MarchingCube(
            MarchingCubeCorner corner0,
            MarchingCubeCorner corner1,
            MarchingCubeCorner corner2,
            MarchingCubeCorner corner3,
            MarchingCubeCorner corner4,
            MarchingCubeCorner corner5,
            MarchingCubeCorner corner6,
            MarchingCubeCorner corner7)
        {
            Corner0 = corner0;
            Corner1 = corner1;
            Corner2 = corner2;
            Corner3 = corner3;
            Corner4 = corner4;
            Corner5 = corner5;
            Corner6 = corner6;
            Corner7 = corner7;
        }

        public MarchingCubeCorner Corner0 { get; }
        public MarchingCubeCorner Corner1 { get; }
        public MarchingCubeCorner Corner2 { get; }
        public MarchingCubeCorner Corner3 { get; }
        public MarchingCubeCorner Corner4 { get; }
        public MarchingCubeCorner Corner5 { get; }
        public MarchingCubeCorner Corner6 { get; }
        public MarchingCubeCorner Corner7 { get; }

        public MarchingCubeCorner GetCorner(int index)
        {
            switch (index)
            {
                case 0:
                    return Corner0;
                case 1:
                    return Corner1;
                case 2:
                    return Corner2;
                case 3:
                    return Corner3;
                case 4:
                    return Corner4;
                case 5:
                    return Corner5;
                case 6:
                    return Corner6;
                case 7:
                    return Corner7;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index), index, "A marching cube has exactly 8 corners.");
            }
        }
    }
}
