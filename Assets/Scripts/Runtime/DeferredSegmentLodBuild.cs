namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    internal struct DeferredSegmentLodBuild
    {
        public bool active;
        public DeferredSegmentLodKey key;
        public int nextChunkIndex;
        public int cellSize;
    }
}
