using System;
using Unity.Mathematics;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    internal sealed partial class PlanetChunkBehaviour
    {
        public void Tick(
            bool skipVisibilityUpdate,
            bool shouldCull,
            Camera cullingCamera,
            Action<int3> markCombinedMeshDirty,
            Action onVisibilityChanged)
        {
            bool visibilityChanged = runtime.UpdateVisibilityIfNeeded(
                skipVisibilityUpdate,
                shouldCull,
                cullingCamera,
                markCombinedMeshDirty);
            if (visibilityChanged)
            {
                onVisibilityChanged();
            }
        }
    }
}
