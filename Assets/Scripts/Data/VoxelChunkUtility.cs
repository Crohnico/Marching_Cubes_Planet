using Unity.Mathematics;

namespace MarchingCubesPlanet.VoxelEngine.Data
{
    public static class VoxelChunkUtility
    {
        public static int3 GetChunkCoords(float3 worldPosition, int3 chunkSize)
        {
            return (int3)math.floor(worldPosition / new float3(chunkSize.x, chunkSize.y, chunkSize.z));
        }

        public static int3 GetChunkOrigin(int3 chunkCoords, int3 chunkSize)
        {
            return chunkCoords * chunkSize;
        }
    }
}
