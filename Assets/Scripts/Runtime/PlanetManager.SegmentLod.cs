using MarchingCubesPlanet.VoxelEngine.Data;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    public sealed partial class PlanetManager
    {
        private void UpdateSegmentLodCombinedMeshes()
        {
            EnsureNearCombinedMeshBucketCount(NearCombinedMeshBucketCount);
            if (combinedMeshLayoutDirty)
            {
                CancelDeferredSegmentLodBuilds();
                for (int i = 0; i < activeCombinedMeshBucketCount && i < nearCombinedMeshBuckets.Count; i++)
                {
                    nearCombinedMeshBuckets[i].dirty = true;
                }

                combinedMeshLayoutDirty = false;
            }

            if (!useNearCombinedMeshes || activeCombinedMeshBucketCount <= 0)
            {
                nearCombinedMeshesBuiltOnce = false;
                combinedMeshLayoutDirty = false;
                combinedMeshesDirty = farCombinedMeshDirty;
                MarkCombinedRendererVisibilityDirty();
                ApplyCombinedRendererVisibility();
                return;
            }

            int bucketBuildBudget = GetSegmentLodBucketBuildsPerFrame();
            int builtBuckets = 0;
            bool pendingBuckets = false;
            for (int i = 0; i < activeCombinedMeshBucketCount && i < nearCombinedMeshBuckets.Count; i++)
            {
                CombinedMeshBucket bucket = nearCombinedMeshBuckets[i];
                if (bucket.dirty)
                {
                    if (builtBuckets >= bucketBuildBudget)
                    {
                        pendingBuckets = true;
                        continue;
                    }

                    int activeLodIndex = ResolveActiveSegmentLodIndex();
                    GetOrBuildSegmentLodMesh(bucket, i, activeLodIndex);
                    QueueDeferredSegmentLodsForBucket(i, activeLodIndex);
                    bucket.dirty = false;
                    builtBuckets++;
                }
            }

            combinedMeshesDirty = pendingBuckets;
            if (!nearCombinedMeshesBuiltOnce)
            {
                nearCombinedMeshesBuiltOnce = !pendingBuckets;
            }

            MarkCombinedRendererVisibilityDirty();
            ApplyCombinedRendererVisibility();
        }

        private void QueueDeferredSegmentLodsForBucket(int bucketIndex, int activeLodIndex)
        {
            int lodCount = GetSegmentLodCount();
            for (int lodIndex = math.min(activeLodIndex - 1, lodCount - 1); lodIndex >= 0; lodIndex--)
            {
                QueueSegmentLodBuild(bucketIndex, lodIndex);
            }
        }

        private void QueueSegmentLodBuild(int bucketIndex, int lodIndex)
        {
            QueueSegmentLodBuild(bucketIndex, lodIndex, false);
        }

        private void QueueSegmentLodBuild(int bucketIndex, int lodIndex, bool priority)
        {
            if (bucketIndex < 0
                || bucketIndex >= activeCombinedMeshBucketCount
                || bucketIndex >= nearCombinedMeshBuckets.Count
                || lodIndex < 0
                || lodIndex >= GetSegmentLodCount())
            {
                return;
            }

            if (lodIndex == ResolveActiveSegmentLodIndex())
            {
                nearCombinedMeshBuckets[bucketIndex].dirty = true;
                combinedMeshesDirty = true;
                return;
            }

            DeferredSegmentLodKey key = new DeferredSegmentLodKey(bucketIndex, lodIndex);
            if (queuedDeferredSegmentLodBuilds.Add(key))
            {
                if (priority)
                {
                    deferredSegmentLodBuildQueue.Insert(0, key);
                }
                else
                {
                    deferredSegmentLodBuildQueue.Add(key);
                }
            }
            else if (priority)
            {
                for (int i = 0; i < deferredSegmentLodBuildQueue.Count; i++)
                {
                    if (deferredSegmentLodBuildQueue[i].Equals(key))
                    {
                        deferredSegmentLodBuildQueue.RemoveAt(i);
                        deferredSegmentLodBuildQueue.Insert(0, key);
                        break;
                    }
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
                1,
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
                if (GetOrLoadSegmentLodMesh(bucket, key.bucketIndex, key.lodIndex) != null)
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
                SaveSegmentLodMesh(mesh, key.bucketIndex, key.lodIndex);
                InstallSegmentLodMesh(bucket, key.bucketIndex, key.lodIndex, mesh);
                MarkCombinedRendererVisibilityDirty();
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
                    bucket.lodCached[lodIndex] = true;
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
            Mesh mesh = GetOrLoadSegmentLodMesh(bucket, bucketIndex, lodIndex);
            if (mesh != null && mesh.vertexCount > 0)
            {
                cache.SetMesh(lodIndex, mesh);
                return;
            }

            QueueSegmentLodBuild(bucketIndex, lodIndex, true);
            MarkCombinedRendererVisibilityDirty();
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

        private bool ApplySegmentLodMesh(CombinedMeshBucket bucket, int bucketIndex, int targetLodIndex, bool segmentActive)
        {
            int renderLodIndex = -1;
            if (segmentActive && targetLodIndex >= 0)
            {
                Mesh mesh = GetOrLoadSegmentLodMesh(bucket, bucketIndex, targetLodIndex);
                if (mesh != null && mesh.vertexCount > 0)
                {
                    renderLodIndex = targetLodIndex;
                }
                else
                {
                    QueueSegmentLodBuild(bucketIndex, targetLodIndex, true);
                    renderLodIndex = ResolveHighestLoadedSegmentLodIndex(bucket);
                }
            }

            bucket.activeLodIndex = renderLodIndex;

            if (bucket.lodCache != null)
            {
                bucket.lodCache.SetPivotActive(segmentActive);
                return bucket.lodCache.LoadLOD(segmentActive ? renderLodIndex : -1);
            }
            return false;
        }

        private bool IsSegmentLodActive(CombinedMeshBucket bucket)
        {
            return bucket.activeLodIndex >= 0 && HasRenderableSegmentLodMesh(bucket, bucket.activeLodIndex);
        }

        private bool AreActiveSegmentLodsReady()
        {
            if (activeCombinedMeshBucketCount <= 0 || activeCombinedMeshBucketCount > nearCombinedMeshBuckets.Count)
            {
                return false;
            }

            int activeLodIndex = ResolveActiveSegmentLodIndex();
            for (int i = 0; i < activeCombinedMeshBucketCount; i++)
            {
                if (!HasLoadedSegmentLodMesh(nearCombinedMeshBuckets[i], activeLodIndex))
                {
                    return false;
                }
            }

            return true;
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

        private int ResolveHighestLoadedSegmentLodIndex(CombinedMeshBucket bucket)
        {
            int lodCount = GetSegmentLodCount();
            for (int lodIndex = lodCount - 1; lodIndex >= 0; lodIndex--)
            {
                if (HasRenderableSegmentLodMesh(bucket, lodIndex))
                {
                    return lodIndex;
                }
            }

            return -1;
        }

        private bool HasRenderableSegmentLodMesh(CombinedMeshBucket bucket, int lodIndex)
        {
            Mesh mesh = GetSegmentLodMesh(bucket, lodIndex);
            return mesh != null && mesh.vertexCount > 0;
        }

        private bool HasLoadedSegmentLodMesh(CombinedMeshBucket bucket, int lodIndex)
        {
            return lodIndex >= 0
                && bucket.lodMeshes != null
                && lodIndex < bucket.lodMeshes.Length
                && bucket.lodMeshes[lodIndex] != null
                && bucket.lodCached != null
                && lodIndex < bucket.lodCached.Length
                && bucket.lodCached[lodIndex];
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

        private Mesh GetOrLoadSegmentLodMesh(CombinedMeshBucket bucket, int bucketIndex, int lodIndex)
        {
            EnsureSegmentLodCache(bucket);
            lodIndex = Mathf.Clamp(lodIndex, 0, GetSegmentLodCount() - 1);
            Mesh mesh = GetSegmentLodMesh(bucket, lodIndex);
            if (mesh != null && bucket.lodCached != null && bucket.lodCached[lodIndex])
            {
                return mesh;
            }

            if (mesh != null && mesh.vertexCount > 0)
            {
                bucket.lodCached[lodIndex] = true;
                return mesh;
            }

            if (!TryLoadSegmentLodMeshFromDisk(bucketIndex, lodIndex, out Mesh loadedMesh))
            {
                return null;
            }

            InstallSegmentLodMesh(bucket, bucketIndex, lodIndex, loadedMesh);
            return loadedMesh;
        }

        private Mesh GetOrBuildSegmentLodMesh(CombinedMeshBucket bucket, int bucketIndex, int lodIndex)
        {
            Mesh mesh = GetOrLoadSegmentLodMesh(bucket, bucketIndex, lodIndex);
            if (mesh != null)
            {
                return mesh;
            }

            RebuildSegmentLodCombinedMeshBucket(bucket, bucketIndex, lodIndex);
            return GetSegmentLodMesh(bucket, lodIndex);
        }

        private void RebuildSegmentLodCombinedMeshBucket(CombinedMeshBucket bucket, int bucketIndex, int lodIndex)
        {
            EnsureSegmentLodCache(bucket);
            lodIndex = Mathf.Clamp(lodIndex, 0, GetSegmentLodCount() - 1);
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            Mesh targetMesh = lodIndex == ResolveActiveSegmentLodIndex()
                ? RebuildActiveSegmentLodMeshFromActiveChunks(bucket, bucketIndex, lodIndex)
                : RebuildSegmentLodMeshFromScalarField(bucket, bucketIndex, lodIndex);
            Debug.Log($"[PlanetStartup:{ResolvePlanetId()}] Segment LOD built. segment={bucketIndex}, lod={lodIndex}, vertices={(targetMesh != null ? targetMesh.vertexCount : 0)}, elapsed={stopwatch.ElapsedMilliseconds}ms.", this);
            SaveSegmentLodMesh(targetMesh, bucketIndex, lodIndex);
            InstallSegmentLodMesh(bucket, bucketIndex, lodIndex, targetMesh);
        }

        private bool TryLoadSegmentLodMeshFromDisk(int bucketIndex, int lodIndex, out Mesh mesh)
        {
            string url = GetSegmentLodMeshUrl(bucketIndex, lodIndex);
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            byte[] binary = FileManager.GetFile(url);
            if (binary == null)
            {
                mesh = null;
                return false;
            }

            try
            {
                mesh = MeshBinarySerializer.FromBinary(binary, $"VoxelCombinedMesh_Near_{bucketIndex:00}_LOD_{lodIndex}");
                Debug.Log($"[PlanetStartup:{ResolvePlanetId()}] Segment LOD loaded from {url}. segment={bucketIndex}, lod={lodIndex}, bytes={binary.Length}, vertices={mesh.vertexCount}, elapsed={stopwatch.ElapsedMilliseconds}ms.", this);
                return true;
            }
            catch (System.Exception exception)
            {
                string message = $"Segment LOD mesh could not be loaded for {ResolvePlanetId()} at {url}. segment={bucketIndex}, lod={lodIndex}. {exception.Message}";
                Debug.LogError(message, this);
                throw new System.InvalidOperationException(message, exception);
            }
        }

        private void SaveSegmentLodMesh(Mesh mesh, int bucketIndex, int lodIndex)
        {
            if (mesh == null)
            {
                return;
            }

            string url = GetSegmentLodMeshUrl(bucketIndex, lodIndex);
            try
            {
                System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
                byte[] binary = MeshBinarySerializer.ToBinary(mesh);
                FileManager.SaveFile(url, binary);
                Debug.Log($"[PlanetStartup:{ResolvePlanetId()}] Segment LOD saved to {url}. segment={bucketIndex}, lod={lodIndex}, bytes={binary.Length}, vertices={mesh.vertexCount}, elapsed={stopwatch.ElapsedMilliseconds}ms.", this);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"Could not save Segment LOD mesh for {ResolvePlanetId()} at {url}. segment={bucketIndex}, lod={lodIndex}. {exception.Message}", this);
            }
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
            bucket.lodCached[lodIndex] = true;
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
            Mesh mesh = ChunkBuilder.BuildChunkMeshNow(
                BuildChunkName(chunkCoord, cellSize),
                chunkOrigin,
                chunkSize,
                cellSize,
                GetScalarFieldSettings(),
                caseTable,
                cellRequestBuffer,
                out altIndices);
            if (mesh != null)
            {
                mesh.MarkDynamic();
            }

            return mesh;
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

        private int GetSegmentLodBucketBuildsPerFrame()
        {
            return Mathf.Max(1, MaxDeferredSegmentLodChunksBuiltPerFrame / 16);
        }

        private int GetCellSizeForLodIndex(int lodIndex)
        {
            return ChunkBuilder.NormalizeCellSizeForChunk(
                config.GetCellSizeAtLod(Mathf.Clamp(lodIndex, 0, GetSegmentLodCount() - 1)),
                config.ChunkSize);
        }

        private int GetSegmentLodCount()
        {
            return Mathf.Clamp(config != null ? config.LodCount : SegmentLodCount, 1, SegmentLodCount);
        }
    }
}
