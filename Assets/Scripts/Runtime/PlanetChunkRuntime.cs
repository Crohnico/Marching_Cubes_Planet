using System;
using System.Collections.Generic;
using MarchingCubesPlanet.VoxelEngine.Data;
using Unity.Mathematics;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    internal sealed class PlanetChunkRuntime
    {
        private readonly Dictionary<int3, VoxelChunkState> activeChunks = new Dictionary<int3, VoxelChunkState>();
        private readonly Dictionary<int3, int> declaredChunkRefCounts = new Dictionary<int3, int>();
        private readonly Dictionary<int3, DesiredChunkState> desiredChunkStates = new Dictionary<int3, DesiredChunkState>();
        private readonly HashSet<int3> declaredChunks = new HashSet<int3>();
        private readonly HashSet<int3> desiredChunks = new HashSet<int3>();
        private readonly List<int3> scratchChunkCoords = new List<int3>();
        private readonly List<VoxelCellBuildRequest> cellRequestBuffer = new List<VoxelCellBuildRequest>(32768);
        private readonly Plane[] cullingFrustumPlanes = new Plane[6];

        public Dictionary<int3, VoxelChunkState> ActiveChunks => activeChunks;
        public Dictionary<int3, DesiredChunkState> DesiredChunkStates => desiredChunkStates;
        public HashSet<int3> DeclaredChunks => declaredChunks;
        public HashSet<int3> DesiredChunks => desiredChunks;
        public List<int3> ScratchChunkCoords => scratchChunkCoords;
        public List<VoxelCellBuildRequest> CellRequestBuffer => cellRequestBuffer;
        public Plane[] CullingFrustumPlanes => cullingFrustumPlanes;
        public bool VisibilityDirty { get; set; } = true;

        public int DeclaredChunkCount => declaredChunks.Count;
        public int DesiredChunkCount => desiredChunkStates.Count;
        public int ActiveChunkCount => activeChunks.Count;

        public void ApplyPlanetDataToDeclaredChunks(PlanetData planetData)
        {
            declaredChunkRefCounts.Clear();
            declaredChunks.Clear();

            foreach (int3 chunkCoord in planetData.declaredChunks)
            {
                declaredChunks.Add(chunkCoord);
                declaredChunkRefCounts[chunkCoord] = 1;
            }

            if (declaredChunks.Count != 0)
            {
                return;
            }

            for (int i = 0; i < planetData.chunks.Count; i++)
            {
                int3 chunkCoord = planetData.chunks[i].coord;
                declaredChunks.Add(chunkCoord);
                declaredChunkRefCounts[chunkCoord] = 1;
            }
        }

        public void HydrateFromPlanetData(
            PlanetData planetData,
            GameObject owner,
            Func<int3, int, string> chunkNameFactory)
        {
            desiredChunks.Clear();
            desiredChunkStates.Clear();

            for (int i = 0; i < planetData.chunks.Count; i++)
            {
                PlanetChunkBuildData chunk = planetData.chunks[i];
                desiredChunks.Add(chunk.coord);
                desiredChunkStates[chunk.coord] = new DesiredChunkState(
                    chunk.cellSize,
                    chunk.detailFocusKey);

                if (chunk.mesh == null || chunk.mesh.vertices.Count == 0)
                {
                    continue;
                }

                Mesh mesh = MeshCrafter.ToChunkUnityMesh(
                    chunk.mesh,
                    chunkNameFactory(chunk.coord, chunk.cellSize));
                activeChunks[chunk.coord] = new VoxelChunkState
                {
                    chunkCoord = chunk.coord,
                    cellSize = chunk.cellSize,
                    detailFocusKey = chunk.detailFocusKey,
                    owner = owner,
                    mesh = mesh,
                    altIndices = new VoxelChunkAltIndices(
                        chunk.mesh.interiorIndices.Count,
                        chunk.mesh.transitionIndices.Count,
                        chunk.mesh.surfaceIndices.Count),
                    chunkOrigin = chunk.origin,
                    chunkBounds = new Bounds(VoxelRuntimeMath.ToVector3(chunk.boundsCenter), VoxelRuntimeMath.ToVector3(chunk.boundsSize)),
                    visible = ChunkVisibility.Visible,
                    generated = true,
                    dirty = false
                };
            }

            MarkVisibilityDirty();
        }

        public void DeclareChunk(int3 chunkCoord)
        {
            if (declaredChunkRefCounts.TryGetValue(chunkCoord, out int refCount))
            {
                declaredChunkRefCounts[chunkCoord] = refCount + 1;
                return;
            }

            declaredChunkRefCounts.Add(chunkCoord, 1);
            declaredChunks.Add(chunkCoord);
        }

        public void ReleaseChunk(int3 chunkCoord)
        {
            if (!declaredChunkRefCounts.TryGetValue(chunkCoord, out int refCount))
            {
                return;
            }

            if (refCount > 1)
            {
                declaredChunkRefCounts[chunkCoord] = refCount - 1;
                return;
            }

            declaredChunkRefCounts.Remove(chunkCoord);
            declaredChunks.Remove(chunkCoord);
        }

        public void ClearDeclaredChunks()
        {
            declaredChunkRefCounts.Clear();
            declaredChunks.Clear();
        }

        public bool CompleteChunkBuild(
            ChunkBuild chunkBuild,
            GameObject owner,
            Func<int3, int, string> chunkNameFactory,
            out int3 changedChunkCoord)
        {
            chunkBuild.jobHandle.Complete();
            changedChunkCoord = chunkBuild.chunkCoord;

            try
            {
                DesiredChunkState completedState = new DesiredChunkState(
                    chunkBuild.cellSize,
                    chunkBuild.detailFocusKey);
                bool isStillDesired = desiredChunkStates.TryGetValue(chunkBuild.chunkCoord, out DesiredChunkState desiredState)
                    && desiredState.Equals(completedState);
                if (!isStillDesired)
                {
                    return false;
                }

                Mesh mesh = MeshCrafter.BuildChunkMesh(
                    chunkNameFactory(chunkBuild.chunkCoord, chunkBuild.cellSize),
                    chunkBuild.vertices,
                    chunkBuild.normals,
                    chunkBuild.uvs,
                    chunkBuild.interiorIndices,
                    chunkBuild.transitionIndices,
                    chunkBuild.surfaceIndices,
                    out VoxelChunkAltIndices altIndices);

                VoxelChunkState nextState = new VoxelChunkState
                {
                    chunkCoord = chunkBuild.chunkCoord,
                    cellSize = chunkBuild.cellSize,
                    detailFocusKey = chunkBuild.detailFocusKey,
                    owner = owner,
                    mesh = mesh,
                    altIndices = altIndices,
                    chunkOrigin = chunkBuild.chunkOrigin,
                    chunkBounds = BuildChunkBounds(chunkBuild.chunkOrigin, chunkBuild.chunkSize),
                    visible = ChunkVisibility.Visible,
                    generated = true,
                    dirty = false
                };

                if (activeChunks.TryGetValue(chunkBuild.chunkCoord, out VoxelChunkState oldState))
                {
                    DestroyChunk(oldState);
                    activeChunks[chunkBuild.chunkCoord] = nextState;
                }
                else
                {
                    activeChunks.Add(chunkBuild.chunkCoord, nextState);
                }

                MarkVisibilityDirty();
                return true;
            }
            finally
            {
                DisposeChunkBuild(chunkBuild);
            }
        }

        public void MarkAllChunksDirty()
        {
            foreach (VoxelChunkState state in activeChunks.Values)
            {
                state.dirty = true;
            }
        }

        public void MarkVisibilityDirty()
        {
            VisibilityDirty = true;
        }

        public void RemoveUndesiredChunks(Action<int3> markCombinedMeshDirty)
        {
            scratchChunkCoords.Clear();
            foreach (KeyValuePair<int3, VoxelChunkState> pair in activeChunks)
            {
                if (!desiredChunks.Contains(pair.Key))
                {
                    scratchChunkCoords.Add(pair.Key);
                }
            }

            for (int i = 0; i < scratchChunkCoords.Count; i++)
            {
                int3 chunkCoord = scratchChunkCoords[i];
                DestroyChunk(activeChunks[chunkCoord]);
                activeChunks.Remove(chunkCoord);
                MarkVisibilityDirty();
                markCombinedMeshDirty(chunkCoord);
            }
        }

        public void ClearChunks()
        {
            foreach (VoxelChunkState state in activeChunks.Values)
            {
                DestroyChunk(state);
            }

            activeChunks.Clear();
            desiredChunks.Clear();
            desiredChunkStates.Clear();
            MarkVisibilityDirty();
        }

        public bool UpdateVisibility(
            bool shouldCull,
            Camera camera,
            Action<int3> markCombinedMeshDirty)
        {
            bool visibilityChanged = false;
            if (shouldCull)
            {
                GeometryUtility.CalculateFrustumPlanes(camera, cullingFrustumPlanes);
            }

            foreach (VoxelChunkState state in activeChunks.Values)
            {
                bool inRange = true;
                bool inFrustum = true;
                int lastPlaneIndex = state.frustumLastPlaneIndex;
                if (shouldCull)
                {
                    inRange = IsChunkInCameraRange(state.chunkBounds, camera);
                    inFrustum = TestAabbAgainstFrustumCoherent(
                        state.chunkBounds,
                        cullingFrustumPlanes,
                        state.frustumLastPlaneIndex,
                        out lastPlaneIndex);
                }

                if (state.visible.inRange != inRange
                    || state.visible.inFrustum != inFrustum
                    || state.frustumLastPlaneIndex != lastPlaneIndex)
                {
                    state.visible = new ChunkVisibility
                    {
                        inRange = inRange,
                        inFrustum = inFrustum
                    };
                    state.frustumLastPlaneIndex = lastPlaneIndex;
                    visibilityChanged = true;
                    markCombinedMeshDirty(state.chunkCoord);
                }
            }

            return visibilityChanged;
        }

        public int CountVisibleChunks(bool shouldCull)
        {
            if (!shouldCull)
            {
                return activeChunks.Count;
            }

            int count = 0;
            foreach (VoxelChunkState state in activeChunks.Values)
            {
                if (state.visible.IsVisible)
                {
                    count++;
                }
            }

            return count;
        }

        public static Bounds BuildChunkBounds(int3 chunkOrigin, int3 chunkSize)
        {
            Vector3 size = VoxelRuntimeMath.ToVector3(chunkSize);
            return new Bounds(VoxelRuntimeMath.ToVector3(chunkOrigin) + size * 0.5f, size);
        }

        public static bool IsChunkInCameraRange(Bounds bounds, Camera camera)
        {
            Vector3 point = camera.transform.position;
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            float dx = Mathf.Max(Mathf.Abs(point.x - center.x) - extents.x, 0f);
            float dy = Mathf.Max(Mathf.Abs(point.y - center.y) - extents.y, 0f);
            float dz = Mathf.Max(Mathf.Abs(point.z - center.z) - extents.z, 0f);
            float farClip = camera.farClipPlane;
            return dx * dx + dy * dy + dz * dz <= farClip * farClip;
        }

        public static bool TestAabbAgainstFrustumCoherent(
            Bounds bounds,
            Plane[] frustumPlanes,
            int startPlaneIndex,
            out int lastPlaneIndex)
        {
            int planeCount = frustumPlanes.Length;
            int safeStart = planeCount == 0 ? 0 : Mathf.Clamp(startPlaneIndex, 0, planeCount - 1);
            for (int offset = 0; offset < planeCount; offset++)
            {
                int planeIndex = (safeStart + offset) % planeCount;
                if (IsAabbOutsidePlane(bounds, frustumPlanes[planeIndex]))
                {
                    lastPlaneIndex = planeIndex;
                    return false;
                }
            }

            lastPlaneIndex = safeStart;
            return true;
        }

        private static bool IsAabbOutsidePlane(Bounds bounds, Plane plane)
        {
            Vector3 positive = bounds.center;
            Vector3 extents = bounds.extents;
            Vector3 normal = plane.normal;
            positive.x += normal.x >= 0f ? extents.x : -extents.x;
            positive.y += normal.y >= 0f ? extents.y : -extents.y;
            positive.z += normal.z >= 0f ? extents.z : -extents.z;
            return plane.GetDistanceToPoint(positive) < 0f;
        }

        private static void DestroyChunk(VoxelChunkState state)
        {
            if (state.mesh != null)
            {
                DestroyUnityObject(state.mesh);
            }
        }

        private static void DestroyUnityObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
                return;
            }

            UnityEngine.Object.DestroyImmediate(target);
        }

        private static void DisposeChunkBuild(ChunkBuild chunkBuild)
        {
            if (chunkBuild.requests.IsCreated)
            {
                chunkBuild.requests.Dispose();
            }

            if (chunkBuild.cells.IsCreated)
            {
                chunkBuild.cells.Dispose();
            }

            if (chunkBuild.vertices.IsCreated)
            {
                chunkBuild.vertices.Dispose();
            }

            if (chunkBuild.normals.IsCreated)
            {
                chunkBuild.normals.Dispose();
            }

            if (chunkBuild.uvs.IsCreated)
            {
                chunkBuild.uvs.Dispose();
            }

            if (chunkBuild.interiorIndices.IsCreated)
            {
                chunkBuild.interiorIndices.Dispose();
            }

            if (chunkBuild.transitionIndices.IsCreated)
            {
                chunkBuild.transitionIndices.Dispose();
            }

            if (chunkBuild.surfaceIndices.IsCreated)
            {
                chunkBuild.surfaceIndices.Dispose();
            }
        }
    }
}
