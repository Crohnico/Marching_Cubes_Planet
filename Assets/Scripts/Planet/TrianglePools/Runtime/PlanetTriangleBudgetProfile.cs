using UnityEngine;

namespace MarchingCubesPlanet.TrianglePools
{
    [CreateAssetMenu(
        fileName = "PlanetTriangleBudgetProfile",
        menuName = "Marching Cubes Planet/Triangle Budget Profile")]
    public sealed class PlanetTriangleBudgetProfile : ScriptableObject
    {
        [SerializeField] private uint artistId;
        [SerializeField] private string artistName = "Environment";
        [SerializeField] private int totalTriangleBudget = PlanetTriangleBudget.DefaultEnvironmentTriangleBudget;
        [SerializeField] private int distanceBucketCount = PlanetTriangleBudget.DefaultDistanceBucketCount;
        [SerializeField] private int maxTrackedOwners = PlanetTriangleBudget.DefaultMaxTrackedOwners;
        [SerializeField] private int maxAllocationsPerOwner = PlanetTriangleBudget.DefaultMaxAllocationsPerOwner;

        public uint ArtistId => artistId;
        public string ArtistName => string.IsNullOrWhiteSpace(artistName) ? "Unnamed" : artistName;
        public int TotalTriangleBudget => totalTriangleBudget;
        public int DistanceBucketCount => distanceBucketCount;
        public int MaxTrackedOwners => maxTrackedOwners;
        public int MaxAllocationsPerOwner => maxAllocationsPerOwner;

        public PlanetTriangleBudget ToBudget()
        {
            return new PlanetTriangleBudget(
                artistId,
                ArtistName,
                totalTriangleBudget,
                distanceBucketCount,
                maxTrackedOwners,
                maxAllocationsPerOwner);
        }
    }
}
