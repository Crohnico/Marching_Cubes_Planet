using System;

namespace MarchingCubesPlanet.TrianglePools
{
    [Serializable]
    public readonly struct PlanetTriangleBudget
    {
        public const int DefaultEnvironmentTriangleBudget = 1000000;
        public const int DefaultParticlesTriangleBudget = 100000;
        public const int DefaultDistanceBucketCount = 32;
        public const int DefaultMaxTrackedOwners = 64;
        public const int DefaultMaxAllocationsPerOwner = 8192;

        public PlanetTriangleBudget(
            uint artistId,
            string artistName,
            int totalTriangleBudget,
            int distanceBucketCount,
            int maxTrackedOwners,
            int maxAllocationsPerOwner)
        {
            ArtistId = artistId;
            ArtistName = string.IsNullOrWhiteSpace(artistName) ? "Unnamed" : artistName;
            TotalTriangleBudget = Math.Max(1, totalTriangleBudget);
            DistanceBucketCount = Math.Max(1, distanceBucketCount);
            MaxTrackedOwners = Math.Max(0, maxTrackedOwners);
            MaxAllocationsPerOwner = Math.Max(0, maxAllocationsPerOwner);
        }

        public uint ArtistId { get; }
        public string ArtistName { get; }
        public int TotalTriangleBudget { get; }
        public int DistanceBucketCount { get; }
        public int MaxTrackedOwners { get; }
        public int MaxAllocationsPerOwner { get; }

        public static PlanetTriangleBudget EnvironmentDefault()
        {
            return new PlanetTriangleBudget(
                PlanetTriangleArtistId.EnvironmentValue,
                "Environment",
                DefaultEnvironmentTriangleBudget,
                DefaultDistanceBucketCount,
                DefaultMaxTrackedOwners,
                DefaultMaxAllocationsPerOwner);
        }

        public static PlanetTriangleBudget ParticlesDefault()
        {
            return new PlanetTriangleBudget(
                PlanetTriangleArtistId.ParticlesValue,
                "Particles",
                DefaultParticlesTriangleBudget,
                DefaultDistanceBucketCount,
                DefaultMaxTrackedOwners,
                DefaultMaxAllocationsPerOwner);
        }

        public PlanetTriangleBudget WithTotalTriangleBudget(int totalTriangleBudget)
        {
            return new PlanetTriangleBudget(
                ArtistId,
                ArtistName,
                totalTriangleBudget,
                DistanceBucketCount,
                MaxTrackedOwners,
                MaxAllocationsPerOwner);
        }
    }
}
