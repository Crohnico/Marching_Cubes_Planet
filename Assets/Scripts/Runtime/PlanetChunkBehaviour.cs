using System.Collections.Generic;
using MarchingCubesPlanet.VoxelEngine.Data;
using Unity.Mathematics;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    internal sealed partial class PlanetChunkBehaviour
    {
        private readonly PlanetChunkRuntime runtime = new PlanetChunkRuntime();

        public Dictionary<int3, VoxelChunkState> ActiveChunks => runtime.ActiveChunks;
        public Dictionary<int3, DesiredChunkState> DesiredChunkStates => runtime.DesiredChunkStates;
        public HashSet<int3> DeclaredChunks => runtime.DeclaredChunks;
        public HashSet<int3> DesiredChunks => runtime.DesiredChunks;
        public List<int3> ScratchChunkCoords => runtime.ScratchChunkCoords;
        public List<VoxelCellBuildRequest> CellRequestBuffer => runtime.CellRequestBuffer;
        public Plane[] CullingFrustumPlanes => runtime.CullingFrustumPlanes;
        public bool VisibilityDirty
        {
            get => runtime.VisibilityDirty;
            set => runtime.VisibilityDirty = value;
        }

        public int DeclaredChunkCount => runtime.DeclaredChunkCount;
        public int DesiredChunkCount => runtime.DesiredChunkCount;
        public int ActiveChunkCount => runtime.ActiveChunkCount;
    }
}
