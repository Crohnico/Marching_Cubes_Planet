using System;
using Unity.Mathematics;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    internal struct DesiredChunkState : IEquatable<DesiredChunkState>
    {
        public int cellSize;
        public int3 detailFocusKey;

        public DesiredChunkState(int cellSize, int3 detailFocusKey)
        {
            this.cellSize = cellSize;
            this.detailFocusKey = detailFocusKey;
        }

        public bool Equals(DesiredChunkState other)
        {
            return cellSize == other.cellSize
                && detailFocusKey.Equals(other.detailFocusKey);
        }
    }
}
