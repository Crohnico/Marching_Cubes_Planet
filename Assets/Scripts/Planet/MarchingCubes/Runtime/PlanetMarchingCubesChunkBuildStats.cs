using System;

namespace MarchingCubesPlanet.MarchingCubes
{
    [Serializable]
    public readonly struct PlanetMarchingCubesChunkBuildStats
    {
        public PlanetMarchingCubesChunkBuildStats(
            int candidateChunkCount,
            long scannedChunkCount,
            float innerRadius,
            float outerRadius,
            int chunkSize,
            bool reachedCandidateLimit)
        {
            CandidateChunkCount = candidateChunkCount;
            ScannedChunkCount = scannedChunkCount;
            InnerRadius = innerRadius;
            OuterRadius = outerRadius;
            ChunkSize = chunkSize;
            ReachedCandidateLimit = reachedCandidateLimit;
        }

        public int CandidateChunkCount { get; }
        public long ScannedChunkCount { get; }
        public float InnerRadius { get; }
        public float OuterRadius { get; }
        public int ChunkSize { get; }
        public bool ReachedCandidateLimit { get; }
        public long CandidateCellCount => (long)CandidateChunkCount * ChunkSize * ChunkSize * ChunkSize;

        public override string ToString()
        {
            return "chunkCountCandidate=" + CandidateChunkCount +
                   "\nchunkCountScanned=" + ScannedChunkCount +
                   "\ncellCountCandidate=" + CandidateCellCount +
                   "\ninnerRadius=" + InnerRadius.ToString("0.###") +
                   "\nouterRadius=" + OuterRadius.ToString("0.###") +
                   "\nreachedCandidateLimit=" + ReachedCandidateLimit;
        }
    }
}
