using MarchingCubesPlanet.VoxelEngine.Data;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    internal sealed class ChunkBuild
    {
        public int3 chunkCoord;
        public int cellSize;
        public int3 detailFocusKey;
        public int3 chunkOrigin;
        public int3 chunkSize;
        public NativeArray<VoxelCellBuildRequest> requests;
        public NativeArray<VoxelCell> cells;
        public NativeList<float3> vertices;
        public NativeList<float3> normals;
        public NativeList<float2> uvs;
        public NativeList<int> interiorIndices;
        public NativeList<int> transitionIndices;
        public NativeList<int> surfaceIndices;
        public JobHandle jobHandle;
    }
}
