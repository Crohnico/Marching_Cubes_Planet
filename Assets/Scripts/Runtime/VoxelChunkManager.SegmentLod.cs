using MarchingCubesPlanet.VoxelEngine.Data;
using MarchingCubesPlanet.VoxelEngine.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    public sealed partial class VoxelChunkManager
    {
        private void UpdateSegmentLodCombinedMeshes()
        {
            EnsureNearCombinedMeshBucketCount(NearCombinedMeshBucketCount);
            if (combinedMeshLayoutDirty)
            {
                CancelDeferredSegmentLodBuilds();
            }

            for (int i = 0; i < activeCombinedMeshBucketCount && i < nearCombinedMeshBuckets.Count; i++)
            {
                CombinedMeshBucket bucket = nearCombinedMeshBuckets[i];
                if (bucket.dirty || combinedMeshLayoutDirty)
                {
                    int activeLodIndex = ResolveActiveSegmentLodIndex();
                    RebuildSegmentLodCombinedMeshBucket(bucket, i, activeLodIndex);
                    QueueDeferredSegmentLodsForBucket(i, activeLodIndex);
                }

                bucket.dirty = false;
            }

            combinedMeshLayoutDirty = false;
            combinedMeshesDirty = false;
            nearCombinedMeshesBuiltOnce = true;
            ApplyCombinedRendererVisibility();
        }

        private void QueueDeferredSegmentLodsForBucket(int bucketIndex, int activeLodIndex)
        {
            int lodCount = GetSegmentLodCount();
            for (int lodIndex = math.min(activeLodIndex - 1, lodCount - 1); lodIndex >= 0; lodIndex--)
            {
                DeferredSegmentLodKey key = new DeferredSegmentLodKey(bucketIndex, lodIndex);
                if (queuedDeferredSegmentLodBuilds.Add(key))
                {
                    deferredSegmentLodBuildQueue.Add(key);
                }
            }
        }

        private void ProcessDeferredSegmentLodBuilds()
        {
            if (!useNearCombinedMeshes || !nearCombinedMeshesBuiltOnce)
            {
                return;
            }

            int builtChunks = 0;
            int chunkBudget = Mathf.Max(
                VoxelEngineConfig.MinDeferredSegmentLodChunksBuiltPerFrame,
                MaxDeferredSegmentLodChunksBuiltPerFrame);
            while (builtChunks < chunkBudget)
            {
                if (!activeDeferredSegmentLodBuild.active && !TryStartNextDeferredSegmentLodBuild())
                {
                    return;
                }

                if (!BuildNextDeferredSegmentLodChunk())
                {
                    CompleteDeferredSegmentLodBuild();
                    continue;
                }

                builtChunks++;
            }
        }

        private bool TryStartNextDeferredSegmentLodBuild()
        {
            while (deferredSegmentLodBuildQueue.Count > 0)
            {
                DeferredSegmentLodKey key = deferredSegmentLodBuildQueue[0];
                deferredSegmentLodBuildQueue.RemoveAt(0);
                queuedDeferredSegmentLodBuilds.Remove(key);

                if (key.bucketIndex < 0
                    || key.bucketIndex >= activeCombinedMeshBucketCount
                    || key.bucketIndex >= nearCombinedMeshBuckets.Count
                    || key.lodIndex < 0
                    || key.lodIndex >= GetSegmentLodCount())
                {
                    continue;
                }

                CombinedMeshBucket bucket = nearCombinedMeshBuckets[key.bucketIndex];
                EnsureSegmentLodCache(bucket);
                if (!bucket.lodDirty[key.lodIndex] && GetSegmentLodMesh(bucket, key.lodIndex) != null)
                {
                    continue;
                }

                deferredSegmentLodChunkCoords.Clear();
                deferredSegmentLodBuiltChunkCoords.Clear();
                deferredSegmentLodChunkMeshes.Clear();
                foreach (int3 chunkCoord in declaredChunks)
                {
                    if (GetSegmentIndexForChunkCoord(chunkCoord, NearCombinedMeshBucketCount) == key.bucketIndex)
                    {
                        deferredSegmentLodChunkCoords.Add(chunkCoord);
                    }
                }

                activeDeferredSegmentLodBuild = new DeferredSegmentLodBuild
                {
                    active = true,
                    key = key,
                    nextChunkIndex = 0,
                    cellSize = GetCellSizeForLodIndex(key.lodIndex)
                };
                return true;
            }

            return false;
        }

        private bool BuildNextDeferredSegmentLodChunk()
        {
            if (!activeDeferredSegmentLodBuild.active
                || activeDeferredSegmentLodBuild.nextChunkIndex >= deferredSegmentLodChunkCoords.Count)
            {
                return false;
            }

            int3 chunkSize = config.ChunkSize;
            int3 chunkCoord = deferredSegmentLodChunkCoords[activeDeferredSegmentLodBuild.nextChunkIndex];
            int3 chunkOrigin = VoxelChunkUtility.GetChunkOrigin(chunkCoord, chunkSize);
            Mesh chunkMesh = BuildSegmentLodChunkMesh(
                chunkCoord,
                chunkOrigin,
                chunkSize,
                activeDeferredSegmentLodBuild.cellSize,
                out _);

            if (chunkMesh != null && chunkMesh.vertexCount > 0)
            {
                deferredSegmentLodBuiltChunkCoords.Add(chunkCoord);
                deferredSegmentLodChunkMeshes.Add(chunkMesh);
            }
            else
            {
                DestroyUnityObject(chunkMesh);
            }

            activeDeferredSegmentLodBuild.nextChunkIndex++;
            return true;
        }

        private void CompleteDeferredSegmentLodBuild()
        {
            if (!activeDeferredSegmentLodBuild.active)
            {
                return;
            }

            DeferredSegmentLodKey key = activeDeferredSegmentLodBuild.key;
            if (key.bucketIndex >= 0 && key.bucketIndex < nearCombinedMeshBuckets.Count)
            {
                CombinedMeshBucket bucket = nearCombinedMeshBuckets[key.bucketIndex];
                Mesh mesh = CombineDeferredSegmentLodChunks(bucket, key);
                InstallSegmentLodMesh(bucket, key.bucketIndex, key.lodIndex, mesh);
                ApplyCombinedRendererVisibility();
            }

            for (int i = 0; i < deferredSegmentLodChunkMeshes.Count; i++)
            {
                DestroyUnityObject(deferredSegmentLodChunkMeshes[i]);
            }

            deferredSegmentLodChunkMeshes.Clear();
            deferredSegmentLodChunkCoords.Clear();
            deferredSegmentLodBuiltChunkCoords.Clear();
            activeDeferredSegmentLodBuild = default;
        }

        private Mesh CombineDeferredSegmentLodChunks(CombinedMeshBucket bucket, DeferredSegmentLodKey key)
        {
            combineInstances.Clear();
            Matrix4x4 planetLocalToBucketLocal = GetPlanetLocalToBucketLocalMatrix(bucket);
            int3 chunkSize = config.ChunkSize;
            for (int i = 0; i < deferredSegmentLodChunkMeshes.Count; i++)
            {
                Mesh chunkMesh = deferredSegmentLodChunkMeshes[i];
                if (chunkMesh == null || chunkMesh.vertexCount == 0)
                {
                    continue;
                }

                int3 chunkCoord = deferredSegmentLodBuiltChunkCoords[i];
                int3 chunkOrigin = VoxelChunkUtility.GetChunkOrigin(chunkCoord, chunkSize);
                VoxelChunkState chunkState = new VoxelChunkState
                {
                    chunkCoord = chunkCoord,
                    cellSize = activeDeferredSegmentLodBuild.cellSize,
                    owner = gameObject,
                    mesh = chunkMesh,
                    altIndices = BuildAltIndicesFromMesh(chunkMesh),
                    chunkOrigin = chunkOrigin,
                    chunkBounds = BuildChunkBounds(chunkOrigin, chunkSize),
                    visible = ChunkVisibility.Visible,
                    generated = true,
                    dirty = false
                };
                AddChunkCombineInstances(chunkState, planetLocalToBucketLocal, true, false);
            }

            Mesh mesh = new Mesh
            {
                name = $"VoxelCombinedMesh_Near_{key.bucketIndex:00}_LOD_{key.lodIndex}",
                indexFormat = IndexFormat.UInt32
            };
            mesh.MarkDynamic();
            if (combineInstances.Count == 0)
            {
                return mesh;
            }

            EnsureCombineInstanceBuffer(bucket, combineInstances.Count);
            for (int i = 0; i < combineInstances.Count; i++)
            {
                bucket.combineInstanceBuffer[i] = combineInstances[i];
            }

            using (CombineBucketMeshMarker.Auto())
            {
                mesh.CombineMeshes(bucket.combineInstanceBuffer, true, true, false);
                mesh.RecalculateBounds();
            }

            return mesh;
        }

        private static VoxelChunkAltIndices BuildAltIndicesFromMesh(Mesh mesh)
        {
            return new VoxelChunkAltIndices(
                mesh != null && mesh.subMeshCount > InteriorSubMesh ? (int)mesh.GetIndexCount(InteriorSubMesh) : 0,
                mesh != null && mesh.subMeshCount > TransitionSubMesh ? (int)mesh.GetIndexCount(TransitionSubMesh) : 0,
                mesh != null && mesh.subMeshCount > SurfaceSubMesh ? (int)mesh.GetIndexCount(SurfaceSubMesh) : 0);
        }

        private void CancelDeferredSegmentLodBuilds()
        {
            deferredSegmentLodBuildQueue.Clear();
            queuedDeferredSegmentLodBuilds.Clear();
            deferredSegmentLodChunkCoords.Clear();
            deferredSegmentLodBuiltChunkCoords.Clear();

            for (int i = 0; i < deferredSegmentLodChunkMeshes.Count; i++)
            {
                DestroyUnityObject(deferredSegmentLodChunkMeshes[i]);
            }

            deferredSegmentLodChunkMeshes.Clear();
            activeDeferredSegmentLodBuild = default;
        }

        private void CancelDeferredSegmentLodBuild(int bucketIndex, int lodIndex)
        {
            DeferredSegmentLodKey key = new DeferredSegmentLodKey(bucketIndex, lodIndex);
            queuedDeferredSegmentLodBuilds.Remove(key);
            for (int i = deferredSegmentLodBuildQueue.Count - 1; i >= 0; i--)
            {
                if (deferredSegmentLodBuildQueue[i].Equals(key))
                {
                    deferredSegmentLodBuildQueue.RemoveAt(i);
                }
            }

            if (!activeDeferredSegmentLodBuild.active || !activeDeferredSegmentLodBuild.key.Equals(key))
            {
                return;
            }

            for (int i = 0; i < deferredSegmentLodChunkMeshes.Count; i++)
            {
                DestroyUnityObject(deferredSegmentLodChunkMeshes[i]);
            }

            deferredSegmentLodChunkMeshes.Clear();
            deferredSegmentLodChunkCoords.Clear();
            deferredSegmentLodBuiltChunkCoords.Clear();
            activeDeferredSegmentLodBuild = default;
        }

        private void EnsureSegmentLodCache(CombinedMeshBucket bucket)
        {
            if (bucket.lodMeshes == null || bucket.lodMeshes.Length != SegmentLodCount)
            {
                bucket.lodMeshes = new Mesh[SegmentLodCount];
            }

            if (bucket.lodDirty == null || bucket.lodDirty.Length != SegmentLodCount)
            {
                bucket.lodDirty = new bool[SegmentLodCount];
                for (int lodIndex = 0; lodIndex < bucket.lodDirty.Length; lodIndex++)
                {
                    bucket.lodDirty[lodIndex] = true;
                }
            }

            if (bucket.lodCached == null || bucket.lodCached.Length != SegmentLodCount)
            {
                bucket.lodCached = new bool[SegmentLodCount];
            }

            if (bucket.lodCache == null && !bucket.owner.TryGetComponent(out bucket.lodCache))
            {
                bucket.lodCache = bucket.owner.AddComponent<VoxelSegmentLodMeshCache>();
            }

            bucket.lodCache.Configure(TerrainMaterial);
            bucket.lodCache.LodMeshRequested -= HandleSegmentLodMeshRequested;
            bucket.lodCache.LodMeshRequested += HandleSegmentLodMeshRequested;

            for (int lodIndex = 0; lodIndex < SegmentLodCount; lodIndex++)
            {
                if (bucket.lodMeshes[lodIndex] != null)
                {
                    bucket.lodCache.SetMesh(lodIndex, bucket.lodMeshes[lodIndex]);
                    bucket.lodCached[lodIndex] = bucket.lodMeshes[lodIndex].vertexCount > 0;
                }
            }

        }

        private void HandleSegmentLodMeshRequested(VoxelSegmentLodMeshCache cache, int lodIndex)
        {
            if (cache == null || lodIndex < 0 || lodIndex >= GetSegmentLodCount())
            {
                return;
            }

            int bucketIndex = FindSegmentLodCacheBucketIndex(cache);
            if (bucketIndex < 0 || bucketIndex >= nearCombinedMeshBuckets.Count)
            {
                return;
            }

            CombinedMeshBucket bucket = nearCombinedMeshBuckets[bucketIndex];
            EnsureSegmentLodCache(bucket);
            if (HasRenderableSegmentLodMesh(bucket, lodIndex))
            {
                cache.SetMesh(lodIndex, GetSegmentLodMesh(bucket, lodIndex));
                return;
            }

            CancelDeferredSegmentLodBuild(bucketIndex, lodIndex);
            RebuildSegmentLodCombinedMeshBucket(bucket, bucketIndex, lodIndex);
            bucket.activeLodIndex = lodIndex;
            cache.LoadLOD(lodIndex);
            ApplyCombinedRendererVisibility();
        }

        private int FindSegmentLodCacheBucketIndex(VoxelSegmentLodMeshCache cache)
        {
            for (int i = 0; i < nearCombinedMeshBuckets.Count; i++)
            {
                if (nearCombinedMeshBuckets[i].lodCache == cache)
                {
                    return i;
                }
            }

            return -1;
        }

        private void EnsureSegmentLodCacheIfNeeded(CombinedMeshBucket bucket)
        {
            if (bucket.lodCache == null
                || bucket.lodMeshes == null)
            {
                EnsureSegmentLodCache(bucket);
            }
        }

        private bool ApplySegmentLodMesh(CombinedMeshBucket bucket, int targetLodIndex, bool segmentActive)
        {
            int renderLodIndex = segmentActive ? ResolveRenderableLodIndex(bucket, targetLodIndex) : -1;
            bucket.activeLodIndex = renderLodIndex;

            if (bucket.lodCache != null)
            {
                bucket.lodCache.SetPivotActive(segmentActive);
                int requestedLodIndex = segmentActive && renderLodIndex < 0
                    ? targetLodIndex
                    : renderLodIndex;
                return bucket.lodCache.LoadLOD(segmentActive ? requestedLodIndex : -1);
            }
            return false;
        }

        private bool IsSegmentLodActive(CombinedMeshBucket bucket)
        {
            return bucket.activeLodIndex >= 0 && HasRenderableSegmentLodMesh(bucket, bucket.activeLodIndex);
        }

        private int ResolveRenderableLodIndex(CombinedMeshBucket bucket, int targetLodIndex)
        {
            if (targetLodIndex < 0)
            {
                return -1;
            }

            return HasRenderableSegmentLodMesh(bucket, targetLodIndex)
                ? targetLodIndex
                : -1;
        }

        private bool HasRenderableSegmentLodMesh(CombinedMeshBucket bucket, int lodIndex)
        {
            Mesh mesh = GetSegmentLodMesh(bucket, lodIndex);
            return mesh != null && mesh.vertexCount > 0;
        }

        private static Mesh GetSegmentLodMesh(CombinedMeshBucket bucket, int lodIndex)
        {
            if (lodIndex < 0)
            {
                return null;
            }

            Mesh mesh = bucket.lodMeshes != null && lodIndex < bucket.lodMeshes.Length
                ? bucket.lodMeshes[lodIndex]
                : null;
            if (mesh != null && mesh.vertexCount > 0)
            {
                return mesh;
            }

            return mesh;
        }

        private void RebuildSegmentLodCombinedMeshBucket(CombinedMeshBucket bucket, int bucketIndex, int lodIndex)
        {
            EnsureSegmentLodCache(bucket);
            lodIndex = Mathf.Clamp(lodIndex, 0, GetSegmentLodCount() - 1);
            Mesh targetMesh = lodIndex == ResolveActiveSegmentLodIndex()
                ? RebuildActiveSegmentLodMeshFromActiveChunks(bucket, bucketIndex, lodIndex)
                : RebuildSegmentLodMeshFromScalarField(bucket, bucketIndex, lodIndex);
            InstallSegmentLodMesh(bucket, bucketIndex, lodIndex, targetMesh);
        }

        private Mesh RebuildActiveSegmentLodMeshFromActiveChunks(CombinedMeshBucket bucket, int bucketIndex, int lodIndex)
        {
            Mesh targetMesh = GetSegmentLodMesh(bucket, lodIndex);
            if (targetMesh == null)
            {
                targetMesh = bucket.mesh;
            }

            if (targetMesh == null)
            {
                targetMesh = new Mesh
                {
                    name = $"VoxelCombinedMesh_Near_{bucketIndex:00}_LOD_{lodIndex}",
                    indexFormat = IndexFormat.UInt32
                };
                targetMesh.MarkDynamic();
            }

            RebuildCombinedMeshBucket(bucket, bucketIndex, true, targetMesh, false);
            return targetMesh;
        }

        private void InstallSegmentLodMesh(CombinedMeshBucket bucket, int bucketIndex, int lodIndex, Mesh targetMesh)
        {
            EnsureSegmentLodCache(bucket);
            lodIndex = Mathf.Clamp(lodIndex, 0, GetSegmentLodCount() - 1);
            Mesh previousLodMesh = GetSegmentLodMesh(bucket, lodIndex);
            Mesh meshBeingReplaced = previousLodMesh;
            bool activeLod = lodIndex == ResolveActiveSegmentLodIndex();
            if (activeLod && meshBeingReplaced == null)
            {
                meshBeingReplaced = bucket.mesh;
            }

            if (targetMesh == null)
            {
                targetMesh = new Mesh
                {
                    name = $"VoxelCombinedMesh_Near_{bucketIndex:00}_LOD_{lodIndex}",
                    indexFormat = IndexFormat.UInt32
                };
                targetMesh.MarkDynamic();
            }

            if (meshBeingReplaced != null && meshBeingReplaced != targetMesh)
            {
                DestroyUnityObject(meshBeingReplaced);
            }

            bucket.lodMeshes[lodIndex] = targetMesh;
            bucket.lodDirty[lodIndex] = false;
            bucket.lodCached[lodIndex] = targetMesh.vertexCount > 0;
            if (activeLod)
            {
                bucket.mesh = targetMesh;
                bucket.activeLodIndex = lodIndex;
            }

            if (bucket.lodCache != null)
            {
                bucket.lodCache.SetMesh(lodIndex, targetMesh);
            }
        }

        private Mesh RebuildSegmentLodMeshFromScalarField(
            CombinedMeshBucket bucket,
            int bucketIndex,
            int lodIndex)
        {
            int3 chunkSize = config.ChunkSize;
            int cellSize = GetCellSizeForLodIndex(lodIndex);
            combineInstances.Clear();
            scratchSegmentLodChunkMeshes.Clear();
            Matrix4x4 planetLocalToBucketLocal = GetPlanetLocalToBucketLocalMatrix(bucket);

            foreach (int3 chunkCoord in declaredChunks)
            {
                if (GetSegmentIndexForChunkCoord(chunkCoord, NearCombinedMeshBucketCount) != bucketIndex)
                {
                    continue;
                }

                int3 chunkOrigin = VoxelChunkUtility.GetChunkOrigin(chunkCoord, chunkSize);
                Mesh chunkMesh = BuildSegmentLodChunkMesh(chunkCoord, chunkOrigin, chunkSize, cellSize, out VoxelChunkAltIndices altIndices);
                if (chunkMesh == null || chunkMesh.vertexCount == 0)
                {
                    DestroyUnityObject(chunkMesh);
                    continue;
                }

                scratchSegmentLodChunkMeshes.Add(chunkMesh);
                VoxelChunkState chunkState = new VoxelChunkState
                {
                    chunkCoord = chunkCoord,
                    cellSize = cellSize,
                    owner = gameObject,
                    mesh = chunkMesh,
                    altIndices = altIndices,
                    chunkOrigin = chunkOrigin,
                    chunkBounds = BuildChunkBounds(chunkOrigin, chunkSize),
                    visible = ChunkVisibility.Visible,
                    generated = true,
                    dirty = false
                };
                AddChunkCombineInstances(chunkState, planetLocalToBucketLocal, true, false);
            }

            Mesh mesh = new Mesh
            {
                name = $"VoxelCombinedMesh_Near_{bucketIndex:00}_LOD_{lodIndex}",
                indexFormat = IndexFormat.UInt32
            };
            mesh.MarkDynamic();

            if (combineInstances.Count > 0)
            {
                EnsureCombineInstanceBuffer(bucket, combineInstances.Count);
                for (int i = 0; i < combineInstances.Count; i++)
                {
                    bucket.combineInstanceBuffer[i] = combineInstances[i];
                }

                using (CombineBucketMeshMarker.Auto())
                {
                    mesh.CombineMeshes(bucket.combineInstanceBuffer, true, true, false);
                    mesh.RecalculateBounds();
                }
            }

            for (int i = 0; i < scratchSegmentLodChunkMeshes.Count; i++)
            {
                DestroyUnityObject(scratchSegmentLodChunkMeshes[i]);
            }

            scratchSegmentLodChunkMeshes.Clear();
            return mesh;
        }

        private Mesh BuildSegmentLodChunkMesh(
            int3 chunkCoord,
            int3 chunkOrigin,
            int3 chunkSize,
            int cellSize,
            out VoxelChunkAltIndices altIndices)
        {
            cellRequestBuffer.Clear();
            BuildCellRequests(
                cellRequestBuffer,
                chunkOrigin,
                chunkSize,
                cellSize);

            if (cellRequestBuffer.Count == 0)
            {
                altIndices = default;
                return null;
            }

            NativeArray<VoxelCellBuildRequest> requests = default;
            NativeArray<VoxelCell> cells = default;
            NativeList<float3> vertices = default;
            NativeList<float3> normals = default;
            NativeList<float2> uvs = default;
            NativeList<int> interiorIndices = default;
            NativeList<int> transitionIndices = default;
            NativeList<int> surfaceIndices = default;

            try
            {
                int cellCount = cellRequestBuffer.Count;
                requests = new NativeArray<VoxelCellBuildRequest>(cellCount, Allocator.TempJob);
                cells = new NativeArray<VoxelCell>(cellCount, Allocator.TempJob);
                vertices = new NativeList<float3>(math.max(1, cellCount * MaxVerticesPerCell), Allocator.TempJob);
                normals = new NativeList<float3>(math.max(1, cellCount * MaxVerticesPerCell), Allocator.TempJob);
                uvs = new NativeList<float2>(math.max(1, cellCount * MaxVerticesPerCell), Allocator.TempJob);
                interiorIndices = new NativeList<int>(math.max(1, cellCount * MaxVerticesPerCell), Allocator.TempJob);
                transitionIndices = new NativeList<int>(math.max(1, cellCount * MaxVerticesPerCell), Allocator.TempJob);
                surfaceIndices = new NativeList<int>(math.max(1, cellCount * MaxVerticesPerCell), Allocator.TempJob);

                for (int i = 0; i < cellCount; i++)
                {
                    requests[i] = cellRequestBuffer[i];
                }

                ScalarFieldSettings scalarField = GetScalarFieldSettings();
                EvaluateVoxelCellsJob evaluateJob = new EvaluateVoxelCellsJob
                {
                    scalarField = scalarField,
                    requests = requests,
                    cells = cells
                };

                GenerateChunkMeshJob meshJob = new GenerateChunkMeshJob
                {
                    cells = cells,
                    cornerIndexAFromEdge = caseTable.cornerIndexAFromEdge,
                    cornerIndexBFromEdge = caseTable.cornerIndexBFromEdge,
                    triangulation = caseTable.triangulation,
                    chunkOrigin = chunkOrigin,
                    chunkSize = chunkSize,
                    scalarField = scalarField,
                    vertices = vertices,
                    normals = normals,
                    uvs = uvs,
                    interiorIndices = interiorIndices,
                    transitionIndices = transitionIndices,
                    surfaceIndices = surfaceIndices
                };

                JobHandle evaluateHandle = evaluateJob.Schedule(cellCount, 64);
                JobHandle meshHandle = meshJob.Schedule(evaluateHandle);
                meshHandle.Complete();

                Mesh mesh = BuildMesh(
                    BuildChunkName(chunkCoord, cellSize),
                    vertices,
                    normals,
                    uvs,
                    interiorIndices,
                    transitionIndices,
                    surfaceIndices,
                    out altIndices);
                mesh.MarkDynamic();
                return mesh;
            }
            finally
            {
                DisposeIfCreated(requests);
                DisposeIfCreated(cells);
                DisposeIfCreated(vertices);
                DisposeIfCreated(normals);
                DisposeIfCreated(uvs);
                DisposeIfCreated(interiorIndices);
                DisposeIfCreated(transitionIndices);
                DisposeIfCreated(surfaceIndices);
            }
        }

        private int ResolveLodIndexForSegment(int segmentIndex)
        {
            return ResolveLodIndexForSegment(segmentIndex, GetCurrentDetailFocusVector3());
        }

        private int ResolveLodIndexForSegment(int segmentIndex, Vector3 playerPosition)
        {
            int fallbackLod = ResolveActiveSegmentLodIndex();
            if (segmentIndex < 0 || segmentIndex >= nearCombinedMeshBuckets.Count)
            {
                return fallbackLod;
            }

            Vector3 segmentPosition = nearCombinedMeshBuckets[segmentIndex].owner.transform.position;
            float squaredDistance = (playerPosition - segmentPosition).sqrMagnitude;
            int lodCount = GetSegmentLodCount();
            for (int lodIndex = 0; lodIndex < lodCount; lodIndex++)
            {
                float maxDistance = config.GetMaxWorldDistanceForLod(lodIndex);
                if (squaredDistance <= maxDistance * maxDistance)
                {
                    return lodIndex;
                }
            }

            return lodCount - 1;
        }

        private int ResolveActiveSegmentLodIndex()
        {
            return Mathf.Clamp(ActiveSegmentLodIndex, 0, GetSegmentLodCount() - 1);
        }

        private int GetCellSizeForLodIndex(int lodIndex)
        {
            return NormalizeCellSizeForChunk(
                config.GetCellSizeAtLod(Mathf.Clamp(lodIndex, 0, GetSegmentLodCount() - 1)),
                config.ChunkSize);
        }

        private int GetSegmentLodCount()
        {
            return Mathf.Clamp(config != null ? config.LodCount : SegmentLodCount, 1, SegmentLodCount);
        }
    }
}
