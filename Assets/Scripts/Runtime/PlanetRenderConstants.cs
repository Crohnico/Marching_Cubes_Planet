using MarchingCubesPlanet.VoxelEngine.Data;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    internal static class PlanetRenderConstants
    {
        public const int InteriorSubMesh = 0;
        public const int TransitionSubMesh = 1;
        public const int SurfaceSubMesh = 2;
        public const int MaxCombinedMeshBucketCount = VoxelEngineConfig.MaxCombinedMeshBucketCount;
        public const int SegmentLodCount = 3;
    }
}
