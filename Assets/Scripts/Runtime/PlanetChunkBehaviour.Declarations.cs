using MarchingCubesPlanet.VoxelEngine.Data;
using Unity.Mathematics;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    internal sealed partial class PlanetChunkBehaviour
    {
        public void ApplyPlanetDataToDeclaredChunks(PlanetData planetData)
        {
            runtime.ApplyPlanetDataToDeclaredChunks(planetData);
        }

        public void DeclareChunk(int3 chunkCoord)
        {
            runtime.DeclareChunk(chunkCoord);
        }

        public void ReleaseChunk(int3 chunkCoord)
        {
            runtime.ReleaseChunk(chunkCoord);
        }

        public void ClearDeclaredChunks()
        {
            runtime.ClearDeclaredChunks();
        }

        public void DeclareChunkBounds(int3 minInclusive, int3 maxInclusive)
        {
            runtime.DeclareChunkBounds(minInclusive, maxInclusive);
        }

        public bool IsChunkDeclared(int3 chunkCoord)
        {
            return runtime.IsChunkDeclared(chunkCoord);
        }

        public GameObject GetChunkObjectOrNull(int3 chunkCoord)
        {
            return runtime.GetChunkObjectOrNull(chunkCoord);
        }
    }
}
