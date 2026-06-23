using System;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    internal readonly struct DeferredSegmentLodKey : IEquatable<DeferredSegmentLodKey>
    {
        public readonly int bucketIndex;
        public readonly int lodIndex;

        public DeferredSegmentLodKey(int bucketIndex, int lodIndex)
        {
            this.bucketIndex = bucketIndex;
            this.lodIndex = lodIndex;
        }

        public bool Equals(DeferredSegmentLodKey other)
        {
            return bucketIndex == other.bucketIndex && lodIndex == other.lodIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is DeferredSegmentLodKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (bucketIndex * 397) ^ lodIndex;
            }
        }
    }
}
