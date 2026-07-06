using System;

namespace MarchingCubesPlanet.MarchingCubes
{
    [Serializable]
    public readonly struct PlanetGridCoordinates : IEquatable<PlanetGridCoordinates>
    {
        public readonly int x;
        public readonly int y;
        public readonly int z;

        public PlanetGridCoordinates(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static PlanetGridCoordinates FromChunkOrigin(PlanetMarchingCubesChunkOrigin origin, int chunkSize)
        {
            return new PlanetGridCoordinates(origin.x / chunkSize, origin.y / chunkSize, origin.z / chunkSize);
        }

        public PlanetMarchingCubesChunkOrigin ToChunkOrigin(PlanetChunkLod lod)
        {
            int chunkSize = PlanetChunkLodUtility.GetChunkSizeForLod(lod);
            return new PlanetMarchingCubesChunkOrigin(x * chunkSize, y * chunkSize, z * chunkSize);
        }

        public bool Equals(PlanetGridCoordinates other)
        {
            return x == other.x && y == other.y && z == other.z;
        }

        public override bool Equals(object obj)
        {
            return obj is PlanetGridCoordinates other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = x;
                hash = (hash * 397) ^ y;
                hash = (hash * 397) ^ z;
                return hash;
            }
        }

        public override string ToString()
        {
            return x + "_" + y + "_" + z;
        }
    }
}
