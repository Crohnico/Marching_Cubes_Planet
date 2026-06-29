namespace MarchingCubesPlanet.TrianglePools
{
    public struct PlanetTrianglePoolMetrics
    {
        public uint artistId;
        public int totalTriangleBudget;
        public int freeTriangleSlots;
        public int usedTriangleSlots;
        public long requestedTriangleCount;
        public long grantedTriangleCount;
        public long deniedTriangleCount;
        public long reclaimedTriangleCount;
        public int allocationCount;
        public int ownerCount;
        public int distanceBucketCount;
        public int worstResidentBucket;
        public float worstResidentScore;
        public string lastDiagnostic;
    }
}
