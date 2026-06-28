using System;

namespace MarchingCubesPlanet.MarchingCubes
{
    [Serializable]
    public struct PlanetMarchingCubesState
    {
        public const int Stride = 32;

        public uint triangleCountAttempted;
        public uint triangleCountWritten;
        public uint vertexCountWritten;
        public uint overflowFlag;
        public uint invalidCaseFlag;
        public uint processedCubeCount;
        public uint reserved0;
        public uint reserved1;
    }
}
