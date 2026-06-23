using System;
using System.Collections;
using MarchingCubesPlanet.VoxelEngine.Data;
using MarchingCubesPlanet.VoxelEngine.MarchingCubes;
using Unity.Mathematics;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    internal sealed partial class PlanetChunkBehaviour
    {
        public void RebuildDesiredChunkSet(
            int3 detailFocusKey,
            Func<int3, int> resolveCellSize,
            Action<int3> markCombinedMeshDirty,
            Action onVisibilityDirty)
        {
            runtime.RebuildDesiredChunkSet(detailFocusKey, resolveCellSize, markCombinedMeshDirty);
            onVisibilityDirty();
        }

        public bool TryRebuildDesiredChunkSetFromPlanetData(
            PlanetData planetData,
            Action<int3> markCombinedMeshDirty,
            Action onVisibilityDirty)
        {
            bool rebuilt = runtime.TryRebuildDesiredChunkSetFromPlanetData(planetData, markCombinedMeshDirty);
            if (rebuilt)
            {
                onVisibilityDirty();
            }

            return rebuilt;
        }

        public IEnumerator BuildDesiredChunksBudgeted(
            int3 centerChunk,
            int maxChunksPerFrame,
            int3 chunkSize,
            ScalarFieldSettings scalarField,
            MarchingCubesCaseTable caseTable,
            GameObject owner,
            Action<int3> markCombinedMeshDirty,
            Action onVisibilityDirty)
        {
            return runtime.BuildDesiredChunksBudgeted(
                centerChunk,
                maxChunksPerFrame,
                chunkSize,
                scalarField,
                caseTable,
                owner,
                markCombinedMeshDirty,
                onVisibilityDirty);
        }

        public void BuildDesiredChunksSynchronously(
            int3 centerChunk,
            int3 chunkSize,
            ScalarFieldSettings scalarField,
            MarchingCubesCaseTable caseTable,
            GameObject owner,
            Action<int3> markCombinedMeshDirty,
            Action onVisibilityDirty)
        {
            runtime.BuildDesiredChunksSynchronously(
                centerChunk,
                chunkSize,
                scalarField,
                caseTable,
                owner,
                markCombinedMeshDirty,
                onVisibilityDirty);
        }
    }
}
