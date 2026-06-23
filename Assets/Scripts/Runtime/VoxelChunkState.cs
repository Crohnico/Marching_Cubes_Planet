using MarchingCubesPlanet.VoxelEngine.Data;
using Unity.Mathematics;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    internal sealed class VoxelChunkState
    {
        public int3 chunkCoord;
        public int cellSize;
        public int3 detailFocusKey;
        public GameObject owner;
        public Mesh mesh;
        public VoxelChunkAltIndices altIndices;
        public int3 chunkOrigin;
        public Bounds chunkBounds;
        public ChunkVisibility visible;
        public int frustumLastPlaneIndex;
        public bool generated;
        public bool dirty;
    }
}
