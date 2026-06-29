namespace MarchingCubesPlanet.TrianglePools
{
    public readonly struct PlanetTriangleDrawResult
    {
        public PlanetTriangleDrawResult(
            uint artistId,
            uint ownerId,
            int requestedTriangleCount,
            int grantedTriangleCount,
            int deniedTriangleCount,
            int reclaimedTriangleCount,
            int selectedSourceTriangleCount,
            int sourceTriangleCount,
            int worstResidentBucket,
            float worstResidentScore,
            string diagnostic)
        {
            ArtistId = artistId;
            OwnerId = ownerId;
            RequestedTriangleCount = requestedTriangleCount;
            GrantedTriangleCount = grantedTriangleCount;
            DeniedTriangleCount = deniedTriangleCount;
            ReclaimedTriangleCount = reclaimedTriangleCount;
            SelectedSourceTriangleCount = selectedSourceTriangleCount;
            SourceTriangleCount = sourceTriangleCount;
            WorstResidentBucket = worstResidentBucket;
            WorstResidentScore = worstResidentScore;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public uint ArtistId { get; }
        public uint OwnerId { get; }
        public int RequestedTriangleCount { get; }
        public int GrantedTriangleCount { get; }
        public int DeniedTriangleCount { get; }
        public int ReclaimedTriangleCount { get; }
        public int SelectedSourceTriangleCount { get; }
        public int SourceTriangleCount { get; }
        public int WorstResidentBucket { get; }
        public float WorstResidentScore { get; }
        public string Diagnostic { get; }
        public bool HasAnyGranted => GrantedTriangleCount > 0;
    }
}
