using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    internal sealed class PlanetSegmentSeamBehaviour
    {
        private readonly Dictionary<SegmentSeamKey, SeamRuntime> seams = new Dictionary<SegmentSeamKey, SeamRuntime>();
        private readonly HashSet<SegmentSeamKey> visibleThisUpdate = new HashSet<SegmentSeamKey>();

        private GameObject root;

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
                HideAll();
                return;
            }

            EnsureRoot(nearMeshesRoot.transform);
            for (int segmentIndex = 0; segmentIndex < activeBucketCount; segmentIndex++)
            {
                int3 coord = ToGridCoord(segmentIndex, segmentGrid);
                TryUpdateSeam(segmentIndex, coord + new int3(1, 0, 0), 0);
                TryUpdateSeam(segmentIndex, coord + new int3(0, 1, 0), 1);
                TryUpdateSeam(segmentIndex, coord + new int3(0, 0, 1), 2);
            }

            foreach (KeyValuePair<SegmentSeamKey, SeamRuntime> pair in seams)
            {
                bool active = visibleThisUpdate.Contains(pair.Key);
                if (pair.Value.owner != null && pair.Value.owner.activeSelf != active)
                {
                    pair.Value.owner.SetActive(active);
                }
            }

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
                SeamRuntime seam = GetOrCreateSeam(key, material, destroyObject);
                if (!EnsureSeamMesh(seam, key, bucketA, bucketB, meshA, meshB, getCacheUrl, destroyObject, getCellSizeForLod))
                {
                    return;
                }

                if (seam.meshFilter.sharedMesh != seam.mesh)
                {
                    seam.meshFilter.sharedMesh = seam.mesh;
                }

                if (seam.meshRenderer.sharedMaterial != material)
                {
                    seam.meshRenderer.sharedMaterial = material;
                }

                visibleThisUpdate.Add(key);
            }
        }

        public void ClearRuntimeCache(Action<UnityEngine.Object> destroyObject)
        {
            foreach (KeyValuePair<SegmentSeamKey, SeamRuntime> pair in seams)
            {
                SeamRuntime seam = pair.Value;
                if (seam.meshFilter != null)
                {
                    seam.meshFilter.sharedMesh = null;
                }

                if (seam.mesh != null)
                {
                    destroyObject(seam.mesh);
                }
            }

            seams.Clear();
            if (root != null)
            {
                destroyObject(root);
                root = null;
            }
        }

        private SeamRuntime GetOrCreateSeam(
            SegmentSeamKey key,
            Material material,
            Action<UnityEngine.Object> destroyObject)
        {
            if (seams.TryGetValue(key, out SeamRuntime seam) && seam.owner != null)
            {
                return seam;
            }

            GameObject owner = GetOrCreateChild(root.transform, $"Seam_{key}");
            if (!owner.TryGetComponent(out MeshFilter meshFilter))
            {
                meshFilter = owner.AddComponent<MeshFilter>();
            }

            if (!owner.TryGetComponent(out MeshRenderer meshRenderer))
            {
                meshRenderer = owner.AddComponent<MeshRenderer>();
            }

            meshRenderer.sharedMaterial = material;
            seam = new SeamRuntime
            {
                owner = owner,
                meshFilter = meshFilter,
                meshRenderer = meshRenderer
            };

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

        private void EnsureRoot(Transform parent)
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
        }

        private void HideAll()
        {
            foreach (KeyValuePair<SegmentSeamKey, SeamRuntime> pair in seams)
            {
                if (pair.Value.owner != null && pair.Value.owner.activeSelf)
                {
                    pair.Value.owner.SetActive(false);
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
            public GameObject owner;
            public MeshFilter meshFilter;
            public MeshRenderer meshRenderer;
            public Mesh mesh;
        }
    }
}
