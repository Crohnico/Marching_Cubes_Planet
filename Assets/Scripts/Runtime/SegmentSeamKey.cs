using System;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    internal readonly struct SegmentSeamKey : IEquatable<SegmentSeamKey>
    {
        public readonly int segmentA;
        public readonly int segmentB;
        public readonly int axis;
        public readonly int lodA;
        public readonly int lodB;

        public SegmentSeamKey(int segmentA, int segmentB, int axis, int lodA, int lodB)
        {
            this.segmentA = segmentA;
            this.segmentB = segmentB;
            this.axis = axis;
            this.lodA = lodA;
            this.lodB = lodB;
        }

        public bool Equals(SegmentSeamKey other)
        {
            return segmentA == other.segmentA
                && segmentB == other.segmentB
                && axis == other.axis
                && lodA == other.lodA
                && lodB == other.lodB;
        }

        public override bool Equals(object obj)
        {
            return obj is SegmentSeamKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = segmentA;
                hash = (hash * 397) ^ segmentB;
                hash = (hash * 397) ^ axis;
                hash = (hash * 397) ^ lodA;
                hash = (hash * 397) ^ lodB;
                return hash;
            }
        }

        public override string ToString()
        {
            return $"{segmentA:00}_{segmentB:00}_A{axis}_LOD_{lodA}_{lodB}";
        }
    }
}
