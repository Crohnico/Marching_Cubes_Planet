using System;

namespace MarchingCubesPlanet.MarchingCubes
{
    [Serializable]
    public struct PlanetMarchingCubesState
    {
        public const int Stride = 32;

        public uint chunkCountCandidate;
        public uint chunkCountProcessed;
        public uint cellCountProcessed;
        public uint triangleCountAttempted;
        public uint triangleCountWritten;
        public uint vertexCountWritten;
        public uint overflowFlag;
        public uint invalidCaseFlag;
    }
}
