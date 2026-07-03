using System;

namespace MarchingCubesPlanet.MarchingCubes
{
    [Serializable]
    public struct PlanetMarchingCubesChunkOrigin
    {
        public const int Stride = 16;

        public int x;
        public int y;
        public int z;
        public int reserved;

        public PlanetMarchingCubesChunkOrigin(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            reserved = 0;
        }

        public override string ToString()
        {
            return "(" + x + ", " + y + ", " + z + ")";
        }
    }
}
