using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    internal sealed class PlanetSegmentSeamBehaviour
    {
        private readonly Dictionary<SegmentSeamKey, SeamRuntime> seams = new Dictionary<SegmentSeamKey, SeamRuntime>();
        private readonly HashSet<SegmentSeamKey> visibleThisUpdate = new HashSet<SegmentSeamKey>();
        private readonly List<CombineInstance> combineInstances = new List<CombineInstance>();

        private GameObject root;
        private SeamRenderBucket lod01Bucket;
        private SeamRenderBucket lod12Bucket;

        public void UpdateSeams(
            bool showNearMeshes,
            GameObject nearMeshesRoot,
            IReadOnlyList<CombinedMeshBucket> buckets,
            int activeBucketCount,
            int3 segmentGrid,
            bool[] segmentActive,
            Material material,
            Func<SegmentSeamKey, string> getCacheUrl,
            Action<UnityEngine.Object> destroyObject,
            Func<int, int> getCellSizeForLod)
        {
            visibleThisUpdate.Clear();
            if (!showNearMeshes
                || nearMeshesRoot == null
                || buckets == null
                || activeBucketCount <= 1
                || !IsValidGrid(segmentGrid))
            {
                HideBuckets();
                return;
            }

            EnsureRoot(nearMeshesRoot.transform, destroyObject);
            EnsureRenderBuckets(material);
            for (int segmentIndex = 0; segmentIndex < activeBucketCount; segmentIndex++)
            {
                int3 coord = ToGridCoord(segmentIndex, segmentGrid);
                TryUpdateSeam(segmentIndex, coord + new int3(1, 0, 0), 0);
                TryUpdateSeam(segmentIndex, coord + new int3(0, 1, 0), 1);
                TryUpdateSeam(segmentIndex, coord + new int3(0, 0, 1), 2);
            }

            RebuildRenderBucket(lod01Bucket, material, 0, 1);
            RebuildRenderBucket(lod12Bucket, material, 1, 2);

            void TryUpdateSeam(int segmentA, int3 coordB, int axis)
            {
                if (!IsInsideGrid(coordB, segmentGrid))
                {
                    return;
                }

                int segmentB = ToSegmentIndex(coordB, segmentGrid);
                if (segmentB >= activeBucketCount
                    || segmentA >= buckets.Count
                    || segmentB >= buckets.Count
                    || segmentActive == null
                    || segmentA >= segmentActive.Length
                    || segmentB >= segmentActive.Length
                    || !segmentActive[segmentA]
                    || !segmentActive[segmentB])
                {
                    return;
                }

                CombinedMeshBucket bucketA = buckets[segmentA];
                CombinedMeshBucket bucketB = buckets[segmentB];
                int lodA = bucketA.activeLodIndex;
                int lodB = bucketB.activeLodIndex;
                if (lodA < 0 || lodB < 0 || Mathf.Abs(lodA - lodB) != 1)
                {
                    return;
                }

                Mesh meshA = GetLodMesh(bucketA, lodA);
                Mesh meshB = GetLodMesh(bucketB, lodB);
                if (meshA == null || meshB == null || meshA.vertexCount == 0 || meshB.vertexCount == 0)
                {
                    return;
                }

                SegmentSeamKey key = new SegmentSeamKey(segmentA, segmentB, axis, lodA, lodB);
                SeamRuntime seam = GetOrCreateSeam(key, destroyObject);
                if (EnsureSeamMesh(seam, key, bucketA, bucketB, meshA, meshB, getCacheUrl, destroyObject, getCellSizeForLod))
                {
                    visibleThisUpdate.Add(key);
                }
            }
        }

        public void ClearRuntimeCache(Action<UnityEngine.Object> destroyObject)
        {
            foreach (KeyValuePair<SegmentSeamKey, SeamRuntime> pair in seams)
            {
                if (pair.Value.mesh != null)
                {
                    destroyObject(pair.Value.mesh);
                }
            }

            seams.Clear();
            DestroyRenderBucket(lod01Bucket, destroyObject);
            DestroyRenderBucket(lod12Bucket, destroyObject);
            lod01Bucket = null;
            lod12Bucket = null;
            if (root != null)
            {
                destroyObject(root);
                root = null;
            }
        }

        private SeamRuntime GetOrCreateSeam(SegmentSeamKey key, Action<UnityEngine.Object> destroyObject)
        {
            if (seams.TryGetValue(key, out SeamRuntime seam))
            {
                return seam;
            }

            seam = new SeamRuntime();
            if (seams.TryGetValue(key, out SeamRuntime previous) && previous.mesh != null)
            {
                destroyObject(previous.mesh);
            }

            seams[key] = seam;
            return seam;
        }

        private bool EnsureSeamMesh(
            SeamRuntime seam,
            SegmentSeamKey key,
            CombinedMeshBucket bucketA,
            CombinedMeshBucket bucketB,
            Mesh meshA,
            Mesh meshB,
            Func<SegmentSeamKey, string> getCacheUrl,
            Action<UnityEngine.Object> destroyObject,
            Func<int, int> getCellSizeForLod)
        {
            if (seam.mesh != null)
            {
                return seam.mesh.vertexCount > 0;
            }

            string cacheUrl = getCacheUrl(key);
            byte[] binary = FileManager.GetFile(cacheUrl);
            if (binary != null)
            {
                try
                {
                    seam.mesh = MeshBinarySerializer.FromBinary(binary, $"SegmentSeam_{key}");
                    if (seam.mesh != null && seam.mesh.vertexCount > 0)
                    {
                        return true;
                    }

                    destroyObject(seam.mesh);
                    seam.mesh = null;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Could not load segment seam mesh at {cacheUrl}. {exception.Message}");
                }
            }

            Matrix4x4 meshAToRoot = root.transform.worldToLocalMatrix * bucketA.owner.transform.localToWorldMatrix;
            Matrix4x4 meshBToRoot = root.transform.worldToLocalMatrix * bucketB.owner.transform.localToWorldMatrix;
            seam.mesh = SegmentLodSeamBuilder.Build(
                key,
                meshA,
                meshB,
                meshAToRoot,
                meshBToRoot,
                getCellSizeForLod != null ? getCellSizeForLod(key.lodA) : 1,
                getCellSizeForLod != null ? getCellSizeForLod(key.lodB) : 1,
                $"SegmentSeam_{key}");

            if (seam.mesh != null && seam.mesh.vertexCount > 0)
            {
                try
                {
                    FileManager.SaveFile(cacheUrl, MeshBinarySerializer.ToBinary(seam.mesh));
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Could not save segment seam mesh at {cacheUrl}. {exception.Message}");
                }

                return true;
            }

            if (seam.mesh != null)
            {
                destroyObject(seam.mesh);
                seam.mesh = null;
            }

            return false;
        }

        private void EnsureRoot(Transform parent, Action<UnityEngine.Object> destroyObject)
        {
            if (root != null)
            {
                if (root.transform.parent != parent)
                {
                    root.transform.SetParent(parent, false);
                }

                return;
            }

            root = GetOrCreateChild(parent, "Seams");
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            RemoveLegacyIndividualSeamObjects(destroyObject);
        }

        private void EnsureRenderBuckets(Material material)
        {
            lod01Bucket = EnsureRenderBucket(lod01Bucket, "LOD_0_1", material);
            lod12Bucket = EnsureRenderBucket(lod12Bucket, "LOD_1_2", material);
        }

        private SeamRenderBucket EnsureRenderBucket(SeamRenderBucket bucket, string name, Material material)
        {
            if (bucket == null || bucket.owner == null)
            {
                GameObject owner = GetOrCreateChild(root.transform, name);
                if (!owner.TryGetComponent(out MeshFilter meshFilter))
                {
                    meshFilter = owner.AddComponent<MeshFilter>();
                }

                if (!owner.TryGetComponent(out MeshRenderer meshRenderer))
                {
                    meshRenderer = owner.AddComponent<MeshRenderer>();
                }

                Mesh mesh = meshFilter.sharedMesh;
                if (mesh == null)
                {
                    mesh = new Mesh
                    {
                        name = $"SegmentSeams_{name}",
                        indexFormat = IndexFormat.UInt32
                    };
                    mesh.MarkDynamic();
                    meshFilter.sharedMesh = mesh;
                }

                bucket = new SeamRenderBucket
                {
                    owner = owner,
                    meshFilter = meshFilter,
                    meshRenderer = meshRenderer,
                    mesh = mesh
                };
            }

            if (bucket.meshRenderer.sharedMaterial != material)
            {
                bucket.meshRenderer.sharedMaterial = material;
            }

            return bucket;
        }

        private void RebuildRenderBucket(SeamRenderBucket bucket, Material material, int lodMin, int lodMax)
        {
            if (bucket == null || bucket.mesh == null)
            {
                return;
            }

            combineInstances.Clear();
            foreach (SegmentSeamKey key in visibleThisUpdate)
            {
                int min = Mathf.Min(key.lodA, key.lodB);
                int max = Mathf.Max(key.lodA, key.lodB);
                if (min != lodMin || max != lodMax)
                {
                    continue;
                }

                if (!seams.TryGetValue(key, out SeamRuntime seam)
                    || seam.mesh == null
                    || seam.mesh.vertexCount == 0)
                {
                    continue;
                }

                combineInstances.Add(new CombineInstance
                {
                    mesh = seam.mesh,
                    subMeshIndex = 0,
                    transform = Matrix4x4.identity
                });
            }

            bool active = combineInstances.Count > 0;
            if (bucket.owner.activeSelf != active)
            {
                bucket.owner.SetActive(active);
            }

            bucket.mesh.Clear();
            bucket.mesh.indexFormat = IndexFormat.UInt32;
            if (!active)
            {
                return;
            }

            bucket.meshRenderer.sharedMaterial = material;
            bucket.mesh.CombineMeshes(combineInstances.ToArray(), true, true, false);
            bucket.mesh.RecalculateBounds();
            if (bucket.meshFilter.sharedMesh != bucket.mesh)
            {
                bucket.meshFilter.sharedMesh = bucket.mesh;
            }
        }

        private void HideBuckets()
        {
            SetBucketActive(lod01Bucket, false);
            SetBucketActive(lod12Bucket, false);
        }

        private static void SetBucketActive(SeamRenderBucket bucket, bool active)
        {
            if (bucket != null && bucket.owner != null && bucket.owner.activeSelf != active)
            {
                bucket.owner.SetActive(active);
            }
        }

        private static void DestroyRenderBucket(SeamRenderBucket bucket, Action<UnityEngine.Object> destroyObject)
        {
            if (bucket == null)
            {
                return;
            }

            if (bucket.meshFilter != null)
            {
                bucket.meshFilter.sharedMesh = null;
            }

            if (bucket.mesh != null)
            {
                destroyObject(bucket.mesh);
            }
        }

        private void RemoveLegacyIndividualSeamObjects(Action<UnityEngine.Object> destroyObject)
        {
            if (root == null)
            {
                return;
            }

            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = root.transform.GetChild(i);
                if (child != null && child.name.StartsWith("Seam_", StringComparison.Ordinal))
                {
                    destroyObject(child.gameObject);
                }
            }
        }

        private static Mesh GetLodMesh(CombinedMeshBucket bucket, int lodIndex)
        {
            return bucket.lodMeshes != null && lodIndex >= 0 && lodIndex < bucket.lodMeshes.Length
                ? bucket.lodMeshes[lodIndex]
                : null;
        }

        private static bool IsValidGrid(int3 grid)
        {
            return grid.x > 0 && grid.y > 0 && grid.z > 0;
        }

        private static bool IsInsideGrid(int3 coord, int3 grid)
        {
            return coord.x >= 0 && coord.x < grid.x
                && coord.y >= 0 && coord.y < grid.y
                && coord.z >= 0 && coord.z < grid.z;
        }

        private static int3 ToGridCoord(int segmentIndex, int3 grid)
        {
            int x = segmentIndex % grid.x;
            int yz = segmentIndex / grid.x;
            int y = yz % grid.y;
            int z = yz / grid.y;
            return new int3(x, y, z);
        }

        private static int ToSegmentIndex(int3 coord, int3 grid)
        {
            return coord.x + grid.x * (coord.y + grid.y * coord.z);
        }

        private static GameObject GetOrCreateChild(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            GameObject child = existing != null ? existing.gameObject : new GameObject(childName);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            return child;
        }

        private sealed class SeamRuntime
        {
            public Mesh mesh;
        }

        private sealed class SeamRenderBucket
        {
            public GameObject owner;
            public MeshFilter meshFilter;
            public MeshRenderer meshRenderer;
            public Mesh mesh;
        }
    }
}
