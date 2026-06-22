using MarchingCubesPlanet.VoxelEngine.Data;
using MarchingCubesPlanet.VoxelEngine.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    public sealed partial class PlanetManager
    {
        private void EnsureCombinedRenderer()
        {
            RefreshPlanetActionRadiusState();
            RemoveLegacyRootRendererComponents();
            EnsureVisibleCombinedMeshBucket();
            EnsureFarCombinedMeshBucket();
            EnsureNearCombinedMeshBucketCount(NearCombinedMeshBucketCount);

            bool shouldUseNearMeshes = ShouldUseNearCombinedMeshes();
            int desiredBucketCount = shouldUseNearMeshes
                ? Mathf.Clamp(NearCombinedMeshBucketCount, 1, MaxCombinedMeshBucketCount)
                : 0;

            if (!combinedMeshRenderModeInitialized
                || useNearCombinedMeshes != shouldUseNearMeshes
                || activeCombinedMeshBucketCount != desiredBucketCount)
            {
                combinedMeshRenderModeInitialized = true;
                useNearCombinedMeshes = shouldUseNearMeshes;
                activeCombinedMeshBucketCount = desiredBucketCount;
                combinedMeshLayoutDirty = true;
                MarkCombinedRendererVisibilityDirty();
                if (useNearCombinedMeshes)
                {
                    nearCombinedMeshesBuiltOnce = false;
                    hasLastNearVisibilityFocusKey = false;
                    MarkAllNearCombinedMeshesDirty();
                }
                else
                {
                    nearCombinedMeshesBuiltOnce = false;
                    hasLastNearVisibilityFocusKey = false;
                    farCombinedMeshDirty = true;
                    combinedMeshesDirty = true;
                    CancelDeferredSegmentLodBuilds();
                }
            }
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
            Vector3 delta = GetCurrentDetailFocusVector3() - GetSpherePosition();
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
                farCombinedMeshBucket = CreateCombinedMeshBucket(
                    GetOrCreateRenderChild("FarMesh", transform),
                    "VoxelCombinedMesh_Far",
                    true,
                    false);
                MarkCombinedRendererVisibilityDirty();
            }

            ConfigurePlanetCenteredRenderTransform(farCombinedMeshBucket.owner.transform);
            if (farCombinedMeshBucket.meshFilter.sharedMesh != farCombinedMeshBucket.mesh)
            {
                farCombinedMeshBucket.meshFilter.sharedMesh = farCombinedMeshBucket.mesh;
            }
        }

        private void EnsureVisibleCombinedMeshBucket()
        {
            if (visibleCombinedMeshBucket == null)
            {
                visibleCombinedMeshBucket = CreateCombinedMeshBucket(
                    GetOrCreateRenderChild("VisibleMesh", transform),
                    "VisibleMesh_Mesh",
                    true,
                    true);
                MarkCombinedRendererVisibilityDirty();
            }

            ConfigurePlanetCenteredRenderTransform(visibleCombinedMeshBucket.owner.transform);
            if (visibleCombinedMeshBucket.meshRenderer.sharedMaterial != TerrainMaterial)
            {
                visibleCombinedMeshBucket.meshRenderer.sharedMaterial = TerrainMaterial;
            }

            if (visibleCombinedMeshBucket.meshFilter.sharedMesh != visibleCombinedMeshBucket.mesh)
            {
                visibleCombinedMeshBucket.meshFilter.sharedMesh = visibleCombinedMeshBucket.mesh;
            }
        }

        private void RemoveLegacyRootRendererComponents()
        {
            if (TryGetComponent(out MeshFilter meshFilter))
            {
                meshFilter.sharedMesh = null;
                DestroyUnityObject(meshFilter);
            }

            if (TryGetComponent(out MeshRenderer meshRenderer))
            {
                DestroyUnityObject(meshRenderer);
            }
        }

        private void ConfigurePlanetCenteredRenderTransform(Transform renderTransform)
        {
            if (renderTransform.parent != transform)
            {
                renderTransform.SetParent(transform, false);
            }

            Vector3 localPosition = GetSpherePositionInManagerLocal();
            if ((renderTransform.localPosition - localPosition).sqrMagnitude > 0.000001f)
            {
                renderTransform.localPosition = localPosition;
            }

            if (renderTransform.localRotation != Quaternion.identity)
            {
                renderTransform.localRotation = Quaternion.identity;
            }

            if (renderTransform.localScale != Vector3.one)
            {
                renderTransform.localScale = Vector3.one;
            }
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
                    false,
                    false);
                EnsureSegmentLodCache(bucket);
                nearCombinedMeshBuckets.Add(bucket);
            }

        }

        private CombinedMeshBucket CreateCombinedMeshBucket(
            GameObject bucketOwner,
            string meshName,
            bool createMeshFilter,
            bool createRenderer)
        {
            MeshFilter meshFilter = null;
            MeshRenderer meshRenderer = null;
            if (createMeshFilter)
            {
                if (!bucketOwner.TryGetComponent(out meshFilter))
                {
                    meshFilter = bucketOwner.AddComponent<MeshFilter>();
                }
            }
            else if (bucketOwner.TryGetComponent(out MeshFilter existingMeshFilter))
            {
                existingMeshFilter.sharedMesh = null;
                DestroyUnityObject(existingMeshFilter);
            }

            if (createRenderer)
            {
                if (!bucketOwner.TryGetComponent(out meshRenderer))
                {
                    meshRenderer = bucketOwner.AddComponent<MeshRenderer>();
                }
            }
            else if (bucketOwner.TryGetComponent(out MeshRenderer existingMeshRenderer))
            {
                DestroyUnityObject(existingMeshRenderer);
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
            Transform root = EnsureNearMeshesRoot().transform;
            Transform existing = root.Find(bucketName);
            if (existing == null)
            {
                existing = transform.Find(bucketName);
            }

            GameObject bucketOwner = existing != null
                ? existing.gameObject
                : new GameObject(bucketName);
            bucketOwner.transform.SetParent(root, false);
            UpdateNearCombinedMeshBucketTransform(bucketIndex, bucketOwner);
            bucketOwner.transform.localRotation = Quaternion.identity;
            bucketOwner.transform.localScale = Vector3.one;
            return bucketOwner;
        }

        private GameObject EnsureNearMeshesRoot()
        {
            if (nearMeshesRoot != null)
            {
                return nearMeshesRoot;
            }

            nearMeshesRoot = GetOrCreateRenderChild("NearMeshes", transform);
            nearMeshesRoot.transform.localPosition = Vector3.zero;
            nearMeshesRoot.transform.localRotation = Quaternion.identity;
            nearMeshesRoot.transform.localScale = Vector3.one;
            return nearMeshesRoot;
        }

        private static GameObject GetOrCreateRenderChild(string childName, Transform parent)
        {
            Transform existing = parent.Find(childName);
            GameObject child = existing != null ? existing.gameObject : new GameObject(childName);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            return child;
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
            if (!combinedRendererVisibilityDirty)
            {
                return;
            }

            combinedRendererVisibilityDirty = false;
            bool showNearMeshes = useNearCombinedMeshes
                && nearCombinedMeshesBuiltOnce
                && AreActiveSegmentLodsReady();
            bool hasVisibleSegment = false;
            bool hasReadyVisibleSegment = false;
            bool shouldCullRenderFrustum = ShouldCullRenderedChunks()
                && playerChunkTracker.ChunkCullingCamera != null;
            if (shouldCullRenderFrustum)
            {
                GeometryUtility.CalculateFrustumPlanes(playerChunkTracker.ChunkCullingCamera, chunkCullingFrustumPlanes);
            }

            bool shouldCullVisibleMeshFrustum = showNearMeshes && shouldCullRenderFrustum;
            bool planetVisibleInFrustum = !shouldCullVisibleMeshFrustum
                || GeometryUtility.TestPlanesAABB(chunkCullingFrustumPlanes, BuildPlanetWorldBounds());
            bool shouldCullNearFrustum = showNearMeshes && shouldCullRenderFrustum;

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

            bool showVisibleMesh = planetVisibleInFrustum
                && (!showNearMeshes
                    || !hasVisibleSegment
                    || !hasReadyVisibleSegment);
            if (visibleCombinedMeshBucket != null)
            {
                if (visibleCombinedMeshBucket.owner.activeSelf != showVisibleMesh)
                {
                    visibleCombinedMeshBucket.owner.SetActive(showVisibleMesh);
                }

                if (visibleCombinedMeshBucket.meshRenderer.enabled != showVisibleMesh)
                {
                    visibleCombinedMeshBucket.meshRenderer.enabled = showVisibleMesh;
                }

                if (visibleCombinedMeshBucket.meshRenderer.sharedMaterial != TerrainMaterial)
                {
                    visibleCombinedMeshBucket.meshRenderer.sharedMaterial = TerrainMaterial;
                }

                if (visibleCombinedMeshBucket.meshFilter.sharedMesh != visibleCombinedMeshBucket.mesh)
                {
                    visibleCombinedMeshBucket.meshFilter.sharedMesh = visibleCombinedMeshBucket.mesh;
                }
            }

            if (farCombinedMeshBucket != null && !farCombinedMeshBucket.owner.activeSelf)
            {
                farCombinedMeshBucket.owner.SetActive(true);
            }
        }

        private Bounds BuildPlanetWorldBounds()
        {
            float radius = sphereGenerator != null
                ? Mathf.Max(0.01f, sphereGenerator.MaximumTerrainRadius)
                : Mathf.Max(config.ChunkSize.x, Mathf.Max(config.ChunkSize.y, config.ChunkSize.z));
            return new Bounds(GetSpherePosition(), Vector3.one * radius * 2f);
        }

        private bool IsVisibleCombinedMeshRenderingActive()
        {
            return visibleCombinedMeshBucket != null
                && visibleCombinedMeshBucket.owner != null
                && visibleCombinedMeshBucket.owner.activeInHierarchy
                && visibleCombinedMeshBucket.meshRenderer != null
                && visibleCombinedMeshBucket.meshRenderer.enabled
                && visibleCombinedMeshBucket.mesh != null
                && visibleCombinedMeshBucket.mesh.vertexCount > 0;
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
            Vector3 sphereCenter = GetSpherePosition();
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
            ApplySegmentLodMesh(bucket, bucketIndex, activeLod, segmentActive);
        }


        private void RebuildFarCombinedMesh()
        {
            EnsureVisibleCombinedMeshBucket();
            EnsureFarCombinedMeshBucket();
            Mesh farMesh = GetOrBuildFarCombinedMesh();
            DeliverFarCombinedMesh(farMesh);
            RebuildVisibleFarMesh();
            if (TryGetCurrentFarHemisphereDirection(out Vector3 direction))
            {
                lastFarHemisphereDirection = direction;
                hasLastFarHemisphereDirection = true;
            }

            farCombinedMeshDirty = false;
            MarkCombinedRendererVisibilityDirty();
        }

        private Mesh GetOrBuildFarCombinedMesh()
        {
            if (TryLoadFarCombinedMeshFromDisk(out Mesh cachedMesh))
            {
                return cachedMesh;
            }

            string url = GetFarMeshUrl();
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            RebuildCombinedMeshBucket(farCombinedMeshBucket, 0, false, false);
            Debug.Log($"[PlanetStartup:{ResolvePlanetId()}] Far mesh built from active chunks. activeChunks={activeChunks.Count}, vertices={farCombinedMeshBucket.mesh.vertexCount}, elapsed={stopwatch.ElapsedMilliseconds}ms.", this);
            SaveFarCombinedMesh(farCombinedMeshBucket.mesh, url);
            return farCombinedMeshBucket.mesh;
        }

        private bool TryLoadStartupFarMeshFromDisk()
        {
            EnsureVisibleCombinedMeshBucket();
            EnsureFarCombinedMeshBucket();
            if (!TryLoadFarCombinedMeshFromDisk(out Mesh mesh))
            {
                return false;
            }

            DeliverFarCombinedMesh(mesh);
            RebuildVisibleFarMesh();
            farCombinedMeshDirty = false;
            MarkCombinedRendererVisibilityDirty();
            ApplyCombinedRendererVisibility();
            return true;
        }

        private bool TryLoadFarCombinedMeshFromDisk(out Mesh mesh)
        {
            string url = GetFarMeshUrl();
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            byte[] binary = FileManager.GetFile(url);
            if (binary == null)
            {
                mesh = null;
                return false;
            }

            try
            {
                mesh = MeshBinarySerializer.FromBinary(binary, "VoxelCombinedMesh_Far");
                Debug.Log($"[PlanetStartup:{ResolvePlanetId()}] Far mesh loaded from {url}. bytes={binary.Length}, vertices={mesh.vertexCount}, elapsed={stopwatch.ElapsedMilliseconds}ms.", this);
                return true;
            }
            catch (System.Exception exception)
            {
                string message = $"Far mesh could not be loaded for {ResolvePlanetId()} at {url}. {exception.Message}";
                Debug.LogError(message, this);
                throw new System.InvalidOperationException(message, exception);
            }
        }

        private void DeliverFarCombinedMesh(Mesh mesh)
        {
            if (mesh == null || farCombinedMeshBucket.mesh == mesh)
            {
                return;
            }

            Mesh previousMesh = farCombinedMeshBucket.mesh;
            farCombinedMeshBucket.mesh = mesh;
            farCombinedMeshBucket.meshFilter.sharedMesh = mesh;
            if (previousMesh != null && previousMesh != mesh)
            {
                DestroyUnityObject(previousMesh);
            }
        }

        private void SaveFarCombinedMesh(Mesh mesh, string url)
        {
            if (mesh == null)
            {
                return;
            }

            try
            {
                System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
                byte[] binary = MeshBinarySerializer.ToBinary(mesh);
                FileManager.SaveFile(url, binary);
                Debug.Log($"[PlanetStartup:{ResolvePlanetId()}] Far mesh saved to {url}. bytes={binary.Length}, vertices={mesh.vertexCount}, elapsed={stopwatch.ElapsedMilliseconds}ms.", this);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"Could not save Far mesh for {ResolvePlanetId()} at {url}. {exception.Message}", this);
            }
        }

        private void RebuildVisibleFarMesh()
        {
            EnsureVisibleCombinedMeshBucket();
            if (ShouldApplyVisibleFarCuts())
            {
                RebuildCombinedMeshBucket(visibleCombinedMeshBucket, 0, false, visibleCombinedMeshBucket.mesh, true);
                visibleCombinedMeshBucket.meshFilter.sharedMesh = visibleCombinedMeshBucket.mesh;
                return;
            }

            CopyFarMeshToVisibleMesh();
        }

        private bool ShouldApplyVisibleFarCuts()
        {
            return !useNearCombinedMeshes
                && activeChunks.Count > 0
                && UseRadialLayerCulling;
        }

        private void CopyFarMeshToVisibleMesh()
        {
            if (farCombinedMeshBucket == null || farCombinedMeshBucket.mesh == null)
            {
                return;
            }

            Mesh previousMesh = visibleCombinedMeshBucket.mesh;
            Mesh nextMesh = Instantiate(farCombinedMeshBucket.mesh);
            nextMesh.name = "VisibleMesh_Mesh";
            nextMesh.MarkDynamic();
            visibleCombinedMeshBucket.mesh = nextMesh;
            visibleCombinedMeshBucket.meshFilter.sharedMesh = nextMesh;
            if (previousMesh != null && previousMesh != nextMesh && previousMesh != farCombinedMeshBucket.mesh)
            {
                DestroyUnityObject(previousMesh);
            }
        }

        private Matrix4x4 GetPlanetLocalToBucketLocalMatrix(CombinedMeshBucket bucket)
        {
            if (bucket == null || bucket.owner == null)
            {
                return Matrix4x4.identity;
            }

            return bucket.owner.transform.worldToLocalMatrix;
        }


        private void RebuildCombinedMeshBucket(CombinedMeshBucket bucket, int bucketIndex, bool nearBucket)
        {
            RebuildCombinedMeshBucket(bucket, bucketIndex, nearBucket, true);
            if (bucket.meshFilter != null)
            {
                bucket.meshFilter.sharedMesh = bucket.mesh;
            }
        }

        private void RebuildCombinedMeshBucket(
            CombinedMeshBucket bucket,
            int bucketIndex,
            bool nearBucket,
            bool applyFarCuts)
        {
            RebuildCombinedMeshBucket(bucket, bucketIndex, nearBucket, bucket.mesh, applyFarCuts);
        }

        private void RebuildCombinedMeshBucket(
            CombinedMeshBucket bucket,
            int bucketIndex,
            bool nearBucket,
            Mesh targetMesh,
            bool applyFarCuts)
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

                    if (!ShouldRenderChunk(state, nearBucket, applyFarCuts))
                    {
                        continue;
                    }

                    AddChunkCombineInstances(state, planetLocalToBucketLocal, nearBucket, applyFarCuts);
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
            return GetSegmentIndexForWorldPosition(chunkCenter, safeBucketCount);
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
                ? localPosition - GetSpherePositionInManagerLocal()
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
                return GetSpherePositionInManagerLocal();
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
            return sphereGenerator != null ? GetSpherePositionInManagerLocal() + localCenter : localCenter;
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
            if (visibleCombinedMeshBucket != null
                && visibleCombinedMeshBucket.meshRenderer.enabled
                && visibleCombinedMeshBucket.mesh != null)
            {
                count += visibleCombinedMeshBucket.mesh.vertexCount;
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
            if (visibleCombinedMeshBucket != null
                && visibleCombinedMeshBucket.meshRenderer.enabled
                && visibleCombinedMeshBucket.mesh != null)
            {
                count += GetMeshTriangleCount(visibleCombinedMeshBucket.mesh);
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

        private bool ShouldRenderChunk(VoxelChunkState state, bool nearBucket, bool applyFarCuts)
        {
            if (nearBucket || !applyFarCuts)
            {
                return true;
            }

            return GetChunkLayerMask(state) != VoxelChunkLayerMask.None;
        }

        private void AddChunkCombineInstances(
            VoxelChunkState state,
            Matrix4x4 planetLocalToBucketLocal,
            bool nearBucket,
            bool applyFarCuts)
        {
            int layerMask = nearBucket || !applyFarCuts ? VoxelChunkLayerMask.All : GetChunkLayerMask(state);
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
            Vector3 sphereCenter = GetSpherePosition();
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
            MarkCombinedRendererVisibilityDirty();
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
            farCombinedMeshDirty = true;
        }

        private void MarkCombinedMeshDirty(int3 chunkCoord)
        {
            combinedMeshesDirty = true;
            MarkCombinedRendererVisibilityDirty();
            if (!useNearCombinedMeshes)
            {
                farCombinedMeshDirty = true;
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
            MarkCombinedRendererVisibilityDirty();
            for (int i = 0; i < nearCombinedMeshBuckets.Count; i++)
            {
                MarkNearCombinedMeshBucketDirty(nearCombinedMeshBuckets[i]);
            }
        }

        private void MarkCombinedRendererVisibilityDirty()
        {
            combinedRendererVisibilityDirty = true;
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

        private int GetFarCombinedMeshVertexCount()
        {
            return farCombinedMeshBucket != null && farCombinedMeshBucket.mesh != null
                ? farCombinedMeshBucket.mesh.vertexCount
                : 0;
        }

        private int GetVisibleCombinedMeshVertexCount()
        {
            return visibleCombinedMeshBucket != null && visibleCombinedMeshBucket.mesh != null
                ? visibleCombinedMeshBucket.mesh.vertexCount
                : 0;
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

            if (visibleCombinedMeshBucket != null && visibleCombinedMeshBucket.mesh != null)
            {
                visibleCombinedMeshBucket.mesh.Clear();
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
            hasLastNearVisibilityFocusKey = false;
            MarkCombinedRendererVisibilityDirty();
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

            if (visibleCombinedMeshBucket != null)
            {
                if (visibleCombinedMeshBucket.meshFilter != null)
                {
                    visibleCombinedMeshBucket.meshFilter.sharedMesh = null;
                }

                if (visibleCombinedMeshBucket.mesh != null)
                {
                    DestroyUnityObject(visibleCombinedMeshBucket.mesh);
                }

                visibleCombinedMeshBucket = null;
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
            nearMeshesRoot = null;
            activeCombinedMeshBucketCount = 0;
            useNearCombinedMeshes = false;
            nearCombinedMeshesBuiltOnce = false;
            combinedMeshRenderModeInitialized = false;
            hasLastNearVisibilityFocusKey = false;
            farCombinedMeshDirty = true;
            combinedMeshLayoutDirty = true;
            combinedMeshesDirty = true;
            MarkCombinedRendererVisibilityDirty();
        }

    }
}
