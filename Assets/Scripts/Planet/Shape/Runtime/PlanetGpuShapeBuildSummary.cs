using System;

namespace MarchingCubesPlanet.Shape
{
    [Serializable]
    public struct PlanetGpuShapeBuildSummary
    {
        public int cellCount;
        public int continentCellCount;
        public int oceanCellCount;
        public int mountainBiomeCellCount;
        public float minBaseOffset;
        public float maxBaseOffset;
        public float minRoughness;
        public float maxRoughness;
        public long estimatedBytes;

        public override string ToString()
        {
            return "cellCount=" + cellCount +
                   "\ncontinentCellCount=" + continentCellCount +
                   "\noceanCellCount=" + oceanCellCount +
                   "\nmountainBiomeCellCount=" + mountainBiomeCellCount +
                   "\nminBaseOffset=" + minBaseOffset +
                   "\nmaxBaseOffset=" + maxBaseOffset +
                   "\nminRoughness=" + minRoughness +
                   "\nmaxRoughness=" + maxRoughness +
                   "\nestimatedBytes=" + estimatedBytes;
        }
    }
}
