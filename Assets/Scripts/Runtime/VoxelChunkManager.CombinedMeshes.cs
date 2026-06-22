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
        private void EnsureCombinedRenderer()
        {
            RefreshPlanetActionRadiusState();
            EnsureFarCombinedMeshBucket();
            EnsureNearCombinedMeshBucketCount(NearCombinedMeshBucketCount);

            bool shouldUseNearMeshes = ShouldUseNearCombinedMeshes();
            int desiredBucketCount = shouldUseNearMeshes
                ? Mathf.Clamp(NearCombinedMeshBucketCount, 1, MaxCombinedMeshBucketCount)
                : 0;

            if (useNearCombinedMeshes != shouldUseNearMeshes
                || activeCombinedMeshBucketCount != desiredBucketCount)
            {
                useNearCombinedMeshes = shouldUseNearMeshes;
                activeCombinedMeshBucketCount = desiredBucketCount;
                combinedMeshLayoutDirty = true;
                if (useNearCombinedMeshes)
                {
                    nearCombinedMeshesBuiltOnce = false;
                    MarkAllNearCombinedMeshesDirty();
                }
            }

            ApplyCombinedRendererVisibility();
        }

        private void UpdateCombinedMeshForView()
        {
            if (!combinedMeshesDirty && !combinedMeshLayoutDirty)
            {
                return;
            }

            UpdateCombinedMesh(false);
        }

        private void UpdateCombinedMesh(bool force)
        {
            if (!force && !combinedMeshesDirty && !combinedMeshLayoutDirty)
            {
                return;
            }

            using (CombineMeshMarker.Auto())
            {
                EnsureCombinedRenderer();
                if (force || farCombinedMeshDirty || !IsFarCombinedMeshCached())
                {
                    RebuildFarCombinedMesh();
                }

                UpdateSegmentLodCombinedMeshes();
            }
        }


        private bool ShouldUseNearCombinedMeshes()
        {
            return !ShouldThrottlePlanetUpdatesOutsideActionRadius();
        }

        private void RefreshFarHemisphereIfNeeded()
        {
            if (!ShouldUseFarHemisphereRefresh())
            {
                return;
            }

            if (!TryGetCurrentFarHemisphereDirection(out Vector3 direction))
            {
                return;
            }

            if (hasLastFarHemisphereDirection
                && Vector3.Dot(lastFarHemisphereDirection, direction) >= GetFarHemisphereRefreshDotThreshold())
            {
                return;
            }

            lastFarHemisphereDirection = direction;
            hasLastFarHemisphereDirection = true;
            farCombinedMeshDirty = true;
            combinedMeshesDirty = true;
        }

        private bool ShouldUseFarHemisphereRefresh()
        {
            return UseRadialLayerCulling
                && sphereGenerator != null
                && !useNearCombinedMeshes
                && IsFarCombinedMeshCached();
        }

        private bool TryGetCurrentFarHemisphereDirection(out Vector3 direction)
        {
            Vector3 delta = GetCurrentDetailFocusVector3() - sphereGenerator.Center;
            float squaredMagnitude = delta.sqrMagnitude;
            if (squaredMagnitude <= 0.0001f)
            {
                direction = default;
                return false;
            }

            direction = delta / Mathf.Sqrt(squaredMagnitude);
            return true;
        }

        private float GetFarHemisphereRefreshDotThreshold()
        {
            return Mathf.Cos(farHemisphereRefreshAngle * Mathf.Deg2Rad);
        }

        private void EnsureFarCombinedMeshBucket()
        {
            if (farCombinedMeshBucket == null)
            {
                farCombinedMeshBucket = CreateCombinedMeshBucket(gameObject, "VoxelCombinedMesh_Far", true);
            }

            farCombinedMeshBucket.meshRenderer.sharedMaterial = TerrainMaterial;
            farCombinedMeshBucket.meshFilter.sharedMesh = farCombinedMeshBucket.mesh;
        }

        private void EnsureNearCombinedMeshBucketCount(int requiredBucketCount)
        {
            int safeCount = Mathf.Clamp(requiredBucketCount, 1, MaxCombinedMeshBucketCount);
            while (nearCombinedMeshBuckets.Count < safeCount)
            {
                int bucketIndex = nearCombinedMeshBuckets.Count;
                CombinedMeshBucket bucket = CreateCombinedMeshBucket(
                    CreateCombinedMeshBucketObject(bucketIndex),
                    $"VoxelCombinedMesh_Near_{bucketIndex:00}",
                    false);
                EnsureSegmentLodCache(bucket);
                nearCombinedMeshBuckets.Add(bucket);
            }

        }

        private CombinedMeshBucket CreateCombinedMeshBucket(GameObject bucketOwner, string meshName, bool createRenderer)
        {
            MeshFilter meshFilter = null;
            MeshRenderer meshRenderer = null;
            if (createRenderer)
            {
                if (!bucketOwner.TryGetComponent(out meshFilter))
                {
                    meshFilter = bucketOwner.AddComponent<MeshFilter>();
                }

                if (!bucketOwner.TryGetComponent(out meshRenderer))
                {
                    meshRenderer = bucketOwner.AddComponent<MeshRenderer>();
                }
            }
            else
            {
                if (bucketOwner.TryGetComponent(out MeshFilter existingMeshFilter))
                {
                    existingMeshFilter.sharedMesh = null;
                    DestroyUnityObject(existingMeshFilter);
                }

                if (bucketOwner.TryGetComponent(out MeshRenderer existingMeshRenderer))
                {
                    DestroyUnityObject(existingMeshRenderer);
                }
            }

            Mesh mesh = new Mesh
            {
                name = meshName,
                indexFormat = IndexFormat.UInt32
            };
            mesh.MarkDynamic();

            if (meshFilter != null)
            {
                meshFilter.sharedMesh = mesh;
            }

            if (meshRenderer != null)
            {
                meshRenderer.sharedMaterial = TerrainMaterial;
            }

            return new CombinedMeshBucket
            {
                owner = bucketOwner,
                meshFilter = meshFilter,
                meshRenderer = meshRenderer,
                mesh = mesh,
                activeLodIndex = -1,
                dirty = true
            };
        }


        private GameObject CreateCombinedMeshBucketObject(int bucketIndex)
        {
            string bucketName = $"VoxelCombinedMesh_Near_{bucketIndex:00}";
            Transform existing = transform.Find(bucketName);
            GameObject bucketOwner = existing != null
                ? existing.gameObject
                : new GameObject(bucketName);
            bucketOwner.transform.SetParent(transform, false);
            UpdateNearCombinedMeshBucketTransform(bucketIndex, bucketOwner);
            bucketOwner.transform.localRotation = Quaternion.identity;
            bucketOwner.transform.localScale = Vector3.one;
            return bucketOwner;
        }

        private void UpdateNearCombinedMeshBucketTransform(int bucketIndex, GameObject bucketOwner)
        {
            if (bucketOwner == null)
            {
                return;
            }

            Vector3 segmentCenter = GetSegmentLocalCenter(
                bucketIndex,
                Mathf.Clamp(NearCombinedMeshBucketCount, 1, MaxCombinedMeshBucketCount));
            bucketOwner.transform.localPosition = segmentCenter;
        }

        private void ApplyCombinedRendererVisibility()
        {
            bool showNearMeshes = useNearCombinedMeshes && nearCombinedMeshesBuiltOnce;
            bool hasVisibleSegment = false;
            bool hasReadyVisibleSegment = false;
            bool shouldCullNearFrustum = showNearMeshes
                && ShouldCullRenderedChunks()
                && playerChunkTracker.ChunkCullingCamera != null;
            if (shouldCullNearFrustum)
            {
                GeometryUtility.CalculateFrustumPlanes(playerChunkTracker.ChunkCullingCamera, chunkCullingFrustumPlanes);
            }

            for (int i = 0; i < nearCombinedMeshBuckets.Count; i++)
            {
                CombinedMeshBucket bucket = nearCombinedMeshBuckets[i];
                bool active = showNearMeshes
                    && i < activeCombinedMeshBucketCount
                    && IsNearSegmentVisible(i, shouldCullNearFrustum);

                if (useNearCombinedMeshes)
                {
                    EnsureSegmentLodCacheIfNeeded(bucket);
                    if (!bucket.owner.activeSelf)
                    {
                        bucket.owner.SetActive(true);
                    }

                    ApplySegmentLodVisibility(bucket, i, active);
                    if (active && IsSegmentLodActive(bucket))
                    {
                        hasReadyVisibleSegment = true;
                    }

                    if (!active)
                    {
                        continue;
                    }

                    hasVisibleSegment = true;
                }
                else
                {
                    if (!bucket.owner.activeSelf)
                    {
                        bucket.owner.SetActive(true);
                    }

                    if (bucket.lodCache != null)
                    {
                        bucket.lodCache.SetPivotActive(false);
                        bucket.lodCache.LoadLOD(-1);
                    }
                }
            }

            bool showFarMesh = !showNearMeshes
                || !hasVisibleSegment
                || !hasReadyVisibleSegment;
            if (farCombinedMeshBucket != null)
            {
                if (!farCombinedMeshBucket.owner.activeSelf)
                {
                    farCombinedMeshBucket.owner.SetActive(true);
                }

                if (farCombinedMeshBucket.meshRenderer.enabled != showFarMesh)
                {
                    farCombinedMeshBucket.meshRenderer.enabled = showFarMesh;
                }

                if (farCombinedMeshBucket.meshRenderer.sharedMaterial != TerrainMaterial)
                {
                    farCombinedMeshBucket.meshRenderer.sharedMaterial = TerrainMaterial;
                }

                if (farCombinedMeshBucket.meshFilter.sharedMesh != farCombinedMeshBucket.mesh)
                {
                    farCombinedMeshBucket.meshFilter.sharedMesh = farCombinedMeshBucket.mesh;
                }
            }
        }

        private bool IsNearSegmentVisible(int segmentIndex, bool shouldCullFrustum)
        {
            if (!IsNearSegmentOnPlayerHemisphere(segmentIndex))
            {
                return false;
            }

            if (!shouldCullFrustum)
            {
                return true;
            }

            Bounds bounds = BuildNearSegmentWorldBounds(segmentIndex);
            return IsChunkInCameraRange(bounds, playerChunkTracker.ChunkCullingCamera)
                && TestAabbAgainstFrustumCoherent(bounds, chunkCullingFrustumPlanes, 0, out _);
        }

        private bool IsNearSegmentOnPlayerHemisphere(int segmentIndex)
        {
            if (sphereGenerator == null)
            {
                return true;
            }

            Vector3 playerPosition = GetCurrentDetailFocusVector3();
            Vector3 sphereCenter = sphereGenerator.Center;
            float sphereRadius = sphereGenerator.Radius;
            if ((playerPosition - sphereCenter).sqrMagnitude < sphereRadius * sphereRadius)
            {
                return true;
            }

            return IsChunkOnPlayerHemisphere(BuildNearSegmentWorldBounds(segmentIndex), sphereCenter, playerPosition);
        }

        private Bounds BuildNearSegmentWorldBounds(int segmentIndex)
        {
            int segmentCount = Mathf.Clamp(NearCombinedMeshBucketCount, 1, MaxCombinedMeshBucketCount);
            if (!TryGetCombinedMeshBucketGrid(segmentCount, out int3 grid))
            {
                return new Bounds(transform.position, Vector3.one);
            }

            float radius = sphereGenerator != null
                ? Mathf.Max(0.01f, sphereGenerator.MaximumTerrainRadius)
                : 1f;
            Vector3 localCenter = GetSegmentLocalCenter(segmentIndex, segmentCount);
            Vector3 localSize = new Vector3(
                (radius * 2f) / grid.x,
                (radius * 2f) / grid.y,
                (radius * 2f) / grid.z);

            Vector3 worldCenter = transform.TransformPoint(localCenter);
            Vector3 worldX = transform.TransformVector(new Vector3(localSize.x, 0f, 0f));
            Vector3 worldY = transform.TransformVector(new Vector3(0f, localSize.y, 0f));
            Vector3 worldZ = transform.TransformVector(new Vector3(0f, 0f, localSize.z));
            Vector3 worldSize = new Vector3(
                Mathf.Abs(worldX.x) + Mathf.Abs(worldY.x) + Mathf.Abs(worldZ.x),
                Mathf.Abs(worldX.y) + Mathf.Abs(worldY.y) + Mathf.Abs(worldZ.y),
                Mathf.Abs(worldX.z) + Mathf.Abs(worldY.z) + Mathf.Abs(worldZ.z));

            return new Bounds(worldCenter, worldSize);
        }

        private void ApplySegmentLodVisibility(
            CombinedMeshBucket bucket,
            int bucketIndex,
            bool segmentActive)
        {
            EnsureSegmentLodCacheIfNeeded(bucket);
            int activeLod = segmentActive ? ResolveLodIndexForSegment(bucketIndex) : -1;
            ApplySegmentLodMesh(bucket, activeLod, segmentActive);
        }


        private void RebuildFarCombinedMesh()
        {
            EnsureFarCombinedMeshBucket();
            RebuildCombinedMeshBucket(farCombinedMeshBucket, 0, false);
            if (TryGetCurrentFarHemisphereDirection(out Vector3 direction))
            {
                lastFarHemisphereDirection = direction;
                hasLastFarHemisphereDirection = true;
            }

            farCombinedMeshDirty = false;
        }

        private Matrix4x4 GetPlanetLocalToBucketLocalMatrix(CombinedMeshBucket bucket)
        {
            if (bucket == null || bucket.owner == null)
            {
                return Matrix4x4.identity;
            }

            return bucket.owner.transform.worldToLocalMatrix * transform.localToWorldMatrix;
        }


        private void RebuildCombinedMeshBucket(CombinedMeshBucket bucket, int bucketIndex, bool nearBucket)
        {
            RebuildCombinedMeshBucket(bucket, bucketIndex, nearBucket, bucket.mesh);
            if (bucket.meshFilter != null)
            {
                bucket.meshFilter.sharedMesh = bucket.mesh;
            }
        }

        private void RebuildCombinedMeshBucket(CombinedMeshBucket bucket, int bucketIndex, bool nearBucket, Mesh targetMesh)
        {
            using (BuildCombineInstancesMarker.Auto())
            {
                combineInstances.Clear();
                Matrix4x4 planetLocalToBucketLocal = GetPlanetLocalToBucketLocalMatrix(bucket);
                foreach (VoxelChunkState state in activeChunks.Values)
                {
                    if (nearBucket && GetCombinedMeshBucketIndex(state.chunkCoord) != bucketIndex)
                    {
                        continue;
                    }

                    if (!state.generated || state.mesh == null || state.mesh.vertexCount == 0)
                    {
                        continue;
                    }

                    if (!ShouldRenderChunk(state))
                    {
                        continue;
                    }

                    AddChunkCombineInstances(state, planetLocalToBucketLocal, nearBucket);
                }
            }

            targetMesh.Clear();
            targetMesh.indexFormat = IndexFormat.UInt32;
            if (combineInstances.Count > 0)
            {
                EnsureCombineInstanceBuffer(bucket, combineInstances.Count);
                for (int i = 0; i < combineInstances.Count; i++)
                {
                    bucket.combineInstanceBuffer[i] = combineInstances[i];
                }

                using (CombineBucketMeshMarker.Auto())
                {
                    targetMesh.CombineMeshes(bucket.combineInstanceBuffer, true, true, false);
                    targetMesh.RecalculateBounds();
                }
            }

            if (bucket.meshRenderer != null)
            {
                bucket.meshRenderer.sharedMaterial = TerrainMaterial;
            }
        }

        private int GetCombinedMeshBucketIndex(int3 chunkCoord)
        {
            if (!useNearCombinedMeshes || activeCombinedMeshBucketCount <= 1)
            {
                return 0;
            }

            return GetSegmentIndexForChunkCoord(chunkCoord, activeCombinedMeshBucketCount);
        }

        private int ResolveCellSizeForChunk(int3 chunkCoord)
        {
            int requestedCellSize = GetCellSizeForLodIndex(ResolveActiveSegmentLodIndex());
            return NormalizeCellSizeForChunk(requestedCellSize, config.ChunkSize);
        }


        private int GetSegmentIndexForChunkCoord(int3 chunkCoord, int bucketCount)
        {
            int safeBucketCount = Mathf.Clamp(bucketCount, 1, MaxCombinedMeshBucketCount);
            if (!TryGetCombinedMeshBucketGrid(safeBucketCount, out int3 grid))
            {
                unchecked
                {
                    uint hash = (uint)chunkCoord.x * 73856093u
                        ^ (uint)chunkCoord.y * 19349663u
                        ^ (uint)chunkCoord.z * 83492791u;
                    return (int)(hash % (uint)safeBucketCount);
                }
            }

            int3 chunkSize = config.ChunkSize;
            int3 chunkOrigin = VoxelChunkUtility.GetChunkOrigin(chunkCoord, chunkSize);
            Vector3 chunkCenter = ToVector3(chunkOrigin) + ToVector3(chunkSize) * 0.5f;
            return GetSegmentIndexForLocalPosition(chunkCenter, safeBucketCount);
        }

        private int GetSegmentIndexForWorldPosition(Vector3 worldPosition, int bucketCount)
        {
            return GetSegmentIndexForLocalPosition(transform.InverseTransformPoint(worldPosition), bucketCount);
        }

        private int GetSegmentIndexForLocalPosition(Vector3 localPosition, int bucketCount)
        {
            int safeBucketCount = Mathf.Clamp(bucketCount, 1, MaxCombinedMeshBucketCount);
            if (!TryGetCombinedMeshBucketGrid(safeBucketCount, out int3 grid))
            {
                unchecked
                {
                    uint hash = (uint)Mathf.FloorToInt(localPosition.x) * 73856093u
                        ^ (uint)Mathf.FloorToInt(localPosition.y) * 19349663u
                        ^ (uint)Mathf.FloorToInt(localPosition.z) * 83492791u;
                    return (int)(hash % (uint)safeBucketCount);
                }
            }

            Vector3 localCenter = sphereGenerator != null
                ? localPosition - sphereGenerator.Center
                : localPosition;
            float radius = sphereGenerator != null
                ? Mathf.Max(0.01f, sphereGenerator.MaximumTerrainRadius)
                : Mathf.Max(config.ChunkSize.x, Mathf.Max(config.ChunkSize.y, config.ChunkSize.z));
            Vector3 normalized = (localCenter + Vector3.one * radius) / (radius * 2f);

            int x = Mathf.Clamp((int)(normalized.x * grid.x), 0, grid.x - 1);
            int y = Mathf.Clamp((int)(normalized.y * grid.y), 0, grid.y - 1);
            int z = Mathf.Clamp((int)(normalized.z * grid.z), 0, grid.z - 1);
            return x + grid.x * (y + grid.y * z);
        }

        private Vector3 GetSegmentCenter(int segmentIndex, int bucketCount)
        {
            return transform.TransformPoint(GetSegmentLocalCenter(segmentIndex, bucketCount));
        }

        private Vector3 GetSegmentLocalCenter(int segmentIndex, int bucketCount)
        {
            int safeBucketCount = Mathf.Clamp(bucketCount, 1, MaxCombinedMeshBucketCount);
            if (!TryGetCombinedMeshBucketGrid(safeBucketCount, out int3 grid))
            {
                return sphereGenerator != null ? sphereGenerator.Center : Vector3.zero;
            }

            int clampedIndex = Mathf.Clamp(segmentIndex, 0, safeBucketCount - 1);
            int x = clampedIndex % grid.x;
            int yz = clampedIndex / grid.x;
            int y = yz % grid.y;
            int z = yz / grid.y;
            float radius = sphereGenerator != null
                ? Mathf.Max(0.01f, sphereGenerator.MaximumTerrainRadius)
                : Mathf.Max(config.ChunkSize.x, Mathf.Max(config.ChunkSize.y, config.ChunkSize.z));
            Vector3 normalizedCenter = new Vector3(
                (x + 0.5f) / grid.x,
                (y + 0.5f) / grid.y,
                (z + 0.5f) / grid.z);
            Vector3 localCenter = normalizedCenter * (radius * 2f) - Vector3.one * radius;
            return sphereGenerator != null ? sphereGenerator.Center + localCenter : localCenter;
        }

        private Bounds GetSegmentBounds(int segmentIndex, int bucketCount)
        {
            int safeBucketCount = Mathf.Clamp(bucketCount, 1, MaxCombinedMeshBucketCount);
            if (!TryGetCombinedMeshBucketGrid(safeBucketCount, out int3 grid))
            {
                float fallbackSize = sphereGenerator != null
                    ? Mathf.Max(0.01f, sphereGenerator.MaximumTerrainRadius * 2f)
                    : Mathf.Max(config.ChunkSize.x, Mathf.Max(config.ChunkSize.y, config.ChunkSize.z));
                return new Bounds(GetSegmentCenter(segmentIndex, safeBucketCount), Vector3.one * fallbackSize);
            }

            float radius = sphereGenerator != null
                ? Mathf.Max(0.01f, sphereGenerator.MaximumTerrainRadius)
                : Mathf.Max(config.ChunkSize.x, Mathf.Max(config.ChunkSize.y, config.ChunkSize.z));
            Vector3 size = new Vector3(
                (radius * 2f) / grid.x,
                (radius * 2f) / grid.y,
                (radius * 2f) / grid.z);
            return new Bounds(GetSegmentCenter(segmentIndex, safeBucketCount), size);
        }

        private static bool TryGetCombinedMeshBucketGrid(int bucketCount, out int3 grid)
        {
            switch (bucketCount)
            {
                case 2:
                    grid = new int3(2, 1, 1);
                    return true;
                case 4:
                    grid = new int3(2, 2, 1);
                    return true;
                case 8:
                    grid = new int3(2, 2, 2);
                    return true;
                case 16:
                    grid = new int3(4, 2, 2);
                    return true;
                case 32:
                    grid = new int3(4, 4, 2);
                    return true;
                case 64:
                    grid = new int3(4, 4, 4);
                    return true;
                default:
                    grid = default;
                    return false;
            }
        }

        private int GetCombinedVertexCount()
        {
            int count = 0;
            if (farCombinedMeshBucket != null && farCombinedMeshBucket.meshRenderer.enabled && farCombinedMeshBucket.mesh != null)
            {
                count += farCombinedMeshBucket.mesh.vertexCount;
            }

            for (int i = 0; i < activeCombinedMeshBucketCount && i < nearCombinedMeshBuckets.Count; i++)
            {
                CombinedMeshBucket bucket = nearCombinedMeshBuckets[i];
                int lodIndex = bucket.activeLodIndex;
                Mesh mesh = GetSegmentLodMesh(bucket, lodIndex);
                if (mesh != null && IsSegmentLodActive(bucket))
                {
                    count += mesh.vertexCount;
                }
            }

            return count;
        }

        private int GetCombinedTriangleCount()
        {
            int count = 0;
            if (farCombinedMeshBucket != null && farCombinedMeshBucket.meshRenderer.enabled && farCombinedMeshBucket.mesh != null)
            {
                count += GetMeshTriangleCount(farCombinedMeshBucket.mesh);
            }

            for (int i = 0; i < activeCombinedMeshBucketCount && i < nearCombinedMeshBuckets.Count; i++)
            {
                CombinedMeshBucket bucket = nearCombinedMeshBuckets[i];
                int lodIndex = bucket.activeLodIndex;
                Mesh mesh = GetSegmentLodMesh(bucket, lodIndex);
                if (mesh == null || !IsSegmentLodActive(bucket))
                {
                    continue;
                }

                count += GetMeshTriangleCount(mesh);
            }

            return count;
        }

        private static int GetMeshTriangleCount(Mesh mesh)
        {
            int count = 0;
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                count += (int)mesh.GetIndexCount(subMesh) / 3;
            }

            return count;
        }

        private bool ShouldRenderChunk(VoxelChunkState state)
        {
            if (useNearCombinedMeshes)
            {
                return true;
            }

            return IsChunkRenderVisible(state.chunkCoord);
        }

        private void AddChunkCombineInstances(VoxelChunkState state, Matrix4x4 planetLocalToBucketLocal, bool nearBucket)
        {
            int layerMask = nearBucket ? VoxelChunkLayerMask.All : GetChunkLayerMask(state);
            Matrix4x4 chunkTransform = planetLocalToBucketLocal * Matrix4x4.Translate(ToVector3(state.chunkOrigin));
            AddChunkCombineInstance(state, InteriorSubMesh, VoxelChunkLayerMask.Interior, layerMask, chunkTransform);
            AddChunkCombineInstance(state, TransitionSubMesh, VoxelChunkLayerMask.Transition, layerMask, chunkTransform);
            AddChunkCombineInstance(state, SurfaceSubMesh, VoxelChunkLayerMask.Surface, layerMask, chunkTransform);
        }

        private void AddChunkCombineInstance(
            VoxelChunkState state,
            int subMeshIndex,
            int layerFlag,
            int layerMask,
            Matrix4x4 chunkTransform)
        {
            if ((layerMask & layerFlag) == 0 || !state.altIndices.HasSubMesh(subMeshIndex))
            {
                return;
            }

            combineInstances.Add(new CombineInstance
            {
                mesh = state.mesh,
                subMeshIndex = subMeshIndex,
                transform = chunkTransform
            });
        }

        private int GetChunkLayerMask(VoxelChunkState state)
        {
            if (!UseRadialLayerCulling || sphereGenerator == null)
            {
                return VoxelChunkLayerMask.All;
            }

            if (IsChunkInsideNeverLayerCullDistance(state.chunkCoord))
            {
                return VoxelChunkLayerMask.All;
            }

            Vector3 playerPosition = GetCurrentDetailFocusVector3();
            Vector3 sphereCenter = sphereGenerator.Center;
            float sphereRadius = sphereGenerator.Radius;
            bool playerInside = (playerPosition - sphereCenter).sqrMagnitude < sphereRadius * sphereRadius;
            if (playerInside)
            {
                return VoxelChunkLayerMask.Interior | VoxelChunkLayerMask.Transition;
            }

            return IsChunkOnPlayerHemisphere(state.chunkBounds, sphereCenter, playerPosition)
                ? VoxelChunkLayerMask.Surface | VoxelChunkLayerMask.Transition
                : VoxelChunkLayerMask.None;
        }

        private bool IsChunkInsideNeverLayerCullDistance(int3 chunkCoord)
        {
            if (NeverLayerCullChunkDistance <= 0)
            {
                return false;
            }

            int3 centerChunk = GetCurrentCenterChunk();
            int3 delta = chunkCoord - centerChunk;
            return math.lengthsq(delta) < NeverLayerCullChunkDistance * NeverLayerCullChunkDistance;
        }

        private static bool IsChunkOnPlayerHemisphere(Bounds chunkBounds, Vector3 sphereCenter, Vector3 playerPosition)
        {
            Vector3 playerDirection = playerPosition - sphereCenter;
            if (playerDirection.sqrMagnitude <= 0.0001f)
            {
                return true;
            }

            Vector3 chunkDirection = chunkBounds.center - sphereCenter;
            return Vector3.Dot(chunkDirection, playerDirection) >= 0f;
        }

        private void MarkCombinedMeshDirty()
        {
            if (useNearCombinedMeshes)
            {
                MarkAllNearCombinedMeshesDirty();
                if (IsFarBridgeVisible())
                {
                    farCombinedMeshDirty = true;
                }

                return;
            }

            combinedMeshesDirty = true;
            if (!IsFarCombinedMeshCached())
            {
                farCombinedMeshDirty = true;
            }
        }

        private void MarkCombinedMeshDirty(int3 chunkCoord)
        {
            combinedMeshesDirty = true;
            if (!useNearCombinedMeshes)
            {
                if (!IsFarCombinedMeshCached())
                {
                    farCombinedMeshDirty = true;
                }

                return;
            }

            if (IsFarBridgeVisible())
            {
                farCombinedMeshDirty = true;
            }

            if (combinedMeshLayoutDirty || activeCombinedMeshBucketCount <= 1 || nearCombinedMeshBuckets.Count == 0)
            {
                MarkAllNearCombinedMeshesDirty();
                return;
            }

            int bucketIndex = GetCombinedMeshBucketIndex(chunkCoord);
            if (bucketIndex >= 0 && bucketIndex < nearCombinedMeshBuckets.Count)
            {
                MarkNearCombinedMeshBucketDirty(nearCombinedMeshBuckets[bucketIndex]);
            }
        }

        private void MarkAllNearCombinedMeshesDirty()
        {
            combinedMeshesDirty = true;
            for (int i = 0; i < nearCombinedMeshBuckets.Count; i++)
            {
                MarkNearCombinedMeshBucketDirty(nearCombinedMeshBuckets[i]);
            }
        }

        private static void MarkNearCombinedMeshBucketDirty(CombinedMeshBucket bucket)
        {
            bucket.dirty = true;
            if (bucket.lodDirty == null)
            {
                return;
            }

            for (int lodIndex = 0; lodIndex < bucket.lodDirty.Length; lodIndex++)
            {
                bucket.lodDirty[lodIndex] = true;
            }
        }

        private bool IsFarCombinedMeshCached()
        {
            return farCombinedMeshBucket != null
                && farCombinedMeshBucket.mesh != null
                && farCombinedMeshBucket.mesh.vertexCount > 0;
        }

        private bool IsFarBridgeVisible()
        {
            return useNearCombinedMeshes && !nearCombinedMeshesBuiltOnce;
        }

        private static void EnsureCombineInstanceBuffer(CombinedMeshBucket bucket, int requiredLength)
        {
            if (bucket.combineInstanceBuffer == null || bucket.combineInstanceBuffer.Length != requiredLength)
            {
                bucket.combineInstanceBuffer = new CombineInstance[requiredLength];
            }
        }

        private void ClearCombinedMesh()
        {
            CancelDeferredSegmentLodBuilds();
            if (farCombinedMeshBucket != null && farCombinedMeshBucket.mesh != null)
            {
                farCombinedMeshBucket.mesh.Clear();
            }

            for (int i = 0; i < nearCombinedMeshBuckets.Count; i++)
            {
                CombinedMeshBucket bucket = nearCombinedMeshBuckets[i];
                if (bucket.mesh != null)
                {
                    bucket.mesh.Clear();
                }

                if (bucket.lodMeshes == null)
                {
                    continue;
                }

                for (int lodIndex = 0; lodIndex < bucket.lodMeshes.Length; lodIndex++)
                {
                    if (bucket.lodMeshes[lodIndex] != null)
                    {
                        bucket.lodMeshes[lodIndex].Clear();
                    }

                    if (bucket.lodCached != null && lodIndex < bucket.lodCached.Length)
                    {
                        bucket.lodCached[lodIndex] = false;
                    }

                    if (bucket.lodCache != null)
                    {
                        bucket.lodCache.SetMesh(lodIndex, null);
                    }
                }
            }

            farCombinedMeshDirty = true;
            nearCombinedMeshesBuiltOnce = false;
            MarkAllNearCombinedMeshesDirty();
        }

        private void DestroyCombinedMesh()
        {
            CancelDeferredSegmentLodBuilds();
            if (farCombinedMeshBucket != null)
            {
                if (farCombinedMeshBucket.meshFilter != null)
                {
                    farCombinedMeshBucket.meshFilter.sharedMesh = null;
                }

                if (farCombinedMeshBucket.mesh != null)
                {
                    DestroyUnityObject(farCombinedMeshBucket.mesh);
                }

                farCombinedMeshBucket = null;
            }

            for (int i = 0; i < nearCombinedMeshBuckets.Count; i++)
            {
                CombinedMeshBucket bucket = nearCombinedMeshBuckets[i];
                if (bucket.meshFilter != null)
                {
                    bucket.meshFilter.sharedMesh = null;
                }

                if (bucket.mesh != null)
                {
                    DestroyUnityObject(bucket.mesh);
                }

                if (bucket.lodMeshes != null)
                {
                    for (int lodIndex = 0; lodIndex < bucket.lodMeshes.Length; lodIndex++)
                    {
                        if (bucket.lodMeshes[lodIndex] != null && bucket.lodMeshes[lodIndex] != bucket.mesh)
                        {
                            DestroyUnityObject(bucket.lodMeshes[lodIndex]);
                        }
                    }
                }

                if (bucket.owner != null && bucket.owner != gameObject)
                {
                    DestroyUnityObject(bucket.owner);
                }
            }

            nearCombinedMeshBuckets.Clear();
            activeCombinedMeshBucketCount = 0;
            useNearCombinedMeshes = false;
            nearCombinedMeshesBuiltOnce = false;
            farCombinedMeshDirty = true;
            combinedMeshLayoutDirty = true;
            combinedMeshesDirty = true;
        }

    }
}


