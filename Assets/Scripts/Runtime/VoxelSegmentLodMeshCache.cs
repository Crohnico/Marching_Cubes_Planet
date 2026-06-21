using System;
using MarchingCubesPlanet.VoxelEngine.Data;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    [DisallowMultipleComponent]
    public sealed class VoxelSegmentLodMeshCache : MonoBehaviour
    {
        private const int NoPendingLod = -1;

        [SerializeField] private Mesh[] lodMeshes = new Mesh[3];
        [SerializeField] private int activeLodIndex = -1;
        [SerializeField] private int pendingLodIndex = NoPendingLod;

        public event Action<VoxelSegmentLodMeshCache, int> LodMeshReceived;

        public int ActiveLodIndex => activeLodIndex;
        public int PendingLodIndex => pendingLodIndex;

        public void EnsureCapacity(int lodCount)
        {
            if (lodMeshes != null && lodMeshes.Length == lodCount)
            {
                return;
            }

            Mesh[] nextMeshes = new Mesh[lodCount];
            if (lodMeshes != null)
            {
                int copyCount = Mathf.Min(lodMeshes.Length, nextMeshes.Length);
                for (int i = 0; i < copyCount; i++)
                {
                    nextMeshes[i] = lodMeshes[i];
                }
            }

            lodMeshes = nextMeshes;
        }

        public Mesh GetMesh(int lodIndex)
        {
            return lodMeshes != null && lodIndex >= 0 && lodIndex < lodMeshes.Length
                ? lodMeshes[lodIndex]
                : null;
        }

        public void SetMesh(int lodIndex, Mesh mesh)
        {
            if (lodIndex < 0)
            {
                return;
            }

            EnsureCapacity(Mathf.Max(lodIndex + 1, lodMeshes != null ? lodMeshes.Length : 0));
            lodMeshes[lodIndex] = mesh;
            if (mesh != null)
            {
                LodMeshReceived?.Invoke(this, lodIndex);
            }

            if (pendingLodIndex == lodIndex && HasRenderableMesh(lodIndex))
            {
                LoadLOD(lodIndex);
            }
        }

        public bool HasRenderableMesh(int lodIndex)
        {
            Mesh mesh = GetMesh(lodIndex);
            return mesh != null && mesh.vertexCount > 0;
        }

        public int ResolveLodIndex(Vector3 focusWorldPosition, VoxelEngineConfig config, int lodCount, int fallbackLodIndex)
        {
            int safeLodCount = Mathf.Max(1, lodCount);
            if (config == null)
            {
                return Mathf.Clamp(fallbackLodIndex, 0, safeLodCount - 1);
            }

            float squaredDistance = (focusWorldPosition - transform.position).sqrMagnitude;
            for (int lodIndex = 0; lodIndex < safeLodCount; lodIndex++)
            {
                float maxDistance = config.GetMaxWorldDistanceForLod(lodIndex);
                if (squaredDistance <= maxDistance * maxDistance)
                {
                    return lodIndex;
                }
            }

            return safeLodCount - 1;
        }

        public bool LoadLOD(int lodIndex)
        {
            if (lodIndex < 0)
            {
                pendingLodIndex = NoPendingLod;
                return false;
            }

            if (!HasRenderableMesh(lodIndex))
            {
                pendingLodIndex = lodIndex;
                return false;
            }

            pendingLodIndex = NoPendingLod;
            ApplyActiveMesh(
                GetComponent<MeshFilter>(),
                GetComponent<MeshRenderer>(),
                GetComponent<MeshRenderer>() != null ? GetComponent<MeshRenderer>().sharedMaterial : null,
                lodIndex,
                true);
            return true;
        }

        public void ApplyActiveMesh(MeshFilter meshFilter, MeshRenderer meshRenderer, Material material, int lodIndex, bool visible)
        {
            Mesh mesh = GetMesh(lodIndex);
            bool renderable = visible && mesh != null && mesh.vertexCount > 0;
            Mesh desiredMesh = renderable ? mesh : meshFilter != null ? meshFilter.sharedMesh : null;

            if (activeLodIndex == lodIndex
                && meshFilter != null
                && meshFilter.sharedMesh == desiredMesh
                && meshRenderer != null
                && meshRenderer.enabled == renderable
                && meshRenderer.sharedMaterial == material)
            {
                return;
            }

            activeLodIndex = lodIndex;

            if (meshFilter != null)
            {
                if (renderable)
                {
                    meshFilter.sharedMesh = mesh;
                }
            }

            if (meshRenderer != null)
            {
                meshRenderer.sharedMaterial = material;
                meshRenderer.enabled = renderable;
            }
        }
    }
}
