using System;
using Unity.Mathematics;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    internal sealed partial class PlanetChunkBehaviour
    {
        public int CountVisibleChunks(bool shouldCull)
        {
            return runtime.CountVisibleChunks(shouldCull);
        }

        public void MarkAllChunksDirty()
        {
            runtime.MarkAllChunksDirty();
        }

        public void MarkVisibilityDirty()
        {
            runtime.MarkVisibilityDirty();
        }

        public void RemoveUndesiredChunks(Action<int3> markCombinedMeshDirty)
        {
            runtime.RemoveUndesiredChunks(markCombinedMeshDirty);
        }

        public void ClearChunks()
        {
            runtime.ClearChunks();
        }
    }
}
