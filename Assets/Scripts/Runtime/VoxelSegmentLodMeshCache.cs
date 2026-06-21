using System;
using MarchingCubesPlanet.VoxelEngine.Data;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    [DisallowMultipleComponent]
    public sealed class VoxelSegmentLodMeshCache : MonoBehaviour
    {
        [SerializeField] private GameObject pivot;
        [SerializeField] private GameObject lodContainer;
        [SerializeField] private GameObject lod0;
        [SerializeField] private GameObject lod1;
        [SerializeField] private GameObject lod2;

        private Material material;
        private Transform detailFocus;
        private VoxelEngineConfig config;
        private Action delayedAction;
        private int activeLodIndex = -1;

        public event Action<VoxelSegmentLodMeshCache, int> LodMeshRequested;
        public event Action<VoxelSegmentLodMeshCache, int> LodMeshReceived;
        public Transform PivotTransform => EnsurePivot().transform;

        public void Configure(Material sharedMaterial, Transform focus, VoxelEngineConfig voxelConfig)
        {
            material = sharedMaterial;
            detailFocus = focus;
            config = voxelConfig;
            EnsurePivot();
            EnsureLodContainer();
            ReparentLod(lod0);
            ReparentLod(lod1);
            ReparentLod(lod2);
        }

        private void Update()
        {
            if (detailFocus == null || config == null)
            {
                return;
            }

            if (pivot != null && !pivot.activeInHierarchy)
            {
                return;
            }

            LoadLOD(ResolveLodIndex());
        }

        public void SetMesh(int lodIndex, Mesh mesh)
        {
            if (mesh == null)
            {
                GameObject existingLod = GetLod(lodIndex);
                if (existingLod != null)
                {
                    existingLod.SetActive(false);
                }

                return;
            }

            GameObject lod = GetOrCreateLod(lodIndex);
            if (lod == null)
            {
                return;
            }

            MeshFilter meshFilter = lod.GetComponent<MeshFilter>();
            if (meshFilter.sharedMesh == mesh)
            {
                return;
            }

            meshFilter.sharedMesh = mesh;
            lod.GetComponent<MeshRenderer>().sharedMaterial = material;
            lod.SetActive(true);
            lod.SetActive(false);

            delayedAction?.Invoke();
            delayedAction = null;
            LodMeshReceived?.Invoke(this, lodIndex);
        }

        public bool LoadLOD(int lodIndex)
        {
            if (lodIndex < 0 || lodIndex > 2)
            {
                activeLodIndex = -1;
                delayedAction = null;
                lod0?.SetActive(false);
                lod1?.SetActive(false);
                lod2?.SetActive(false);
                return false;
            }

            GameObject lod = GetLod(lodIndex);
            if (lod == null)
            {
                delayedAction = () => LoadLOD(lodIndex);
                LodMeshRequested?.Invoke(this, lodIndex);
                return false;
            }

            if (activeLodIndex == lodIndex)
            {
                return true;
            }

            lod0?.SetActive(lodIndex == 0);
            lod1?.SetActive(lodIndex == 1);
            lod2?.SetActive(lodIndex == 2);
            activeLodIndex = lodIndex;
            return true;
        }

        private GameObject GetOrCreateLod(int lodIndex)
        {
            GameObject lod = GetLod(lodIndex);
            if (lod != null)
            {
                ReparentLod(lod);
                return lod;
            }

            Transform existing = EnsureLodContainer().transform.Find($"LOD_{lodIndex}");
            if (existing == null)
            {
                existing = transform.Find($"LOD_{lodIndex}");
            }

            if (existing != null)
            {
                lod = existing.gameObject;
                ReparentLod(lod);
                if (!lod.TryGetComponent(out MeshFilter _))
                {
                    lod.AddComponent<MeshFilter>();
                }

                if (!lod.TryGetComponent(out MeshRenderer _))
                {
                    lod.AddComponent<MeshRenderer>();
                }

                SetLod(lodIndex, lod);
                return lod;
            }

            lod = new GameObject($"LOD_{lodIndex}");
            lod.transform.SetParent(EnsureLodContainer().transform, false);
            lod.AddComponent<MeshFilter>();
            lod.AddComponent<MeshRenderer>();
            SetLod(lodIndex, lod);
            return lod;
        }

        private void ReparentLod(GameObject lod)
        {
            if (lod == null)
            {
                return;
            }

            Transform parent = EnsureLodContainer().transform;
            if (lod.transform.parent != parent)
            {
                lod.transform.SetParent(parent, false);
            }
        }

        public void SetPivotActive(bool active)
        {
            GameObject owner = EnsurePivot();
            if (owner.activeSelf != active)
            {
                owner.SetActive(active);
            }
        }

        private GameObject EnsurePivot()
        {
            if (pivot != null)
            {
                return pivot;
            }

            Transform existing = transform.Find("Pivot");
            pivot = existing != null ? existing.gameObject : new GameObject("Pivot");
            pivot.transform.SetParent(transform, false);
            pivot.transform.localPosition = Vector3.zero;
            pivot.transform.localRotation = Quaternion.identity;
            pivot.transform.localScale = Vector3.one;
            return pivot;
        }

        private GameObject EnsureLodContainer()
        {
            if (lodContainer != null)
            {
                return lodContainer;
            }

            Transform existing = EnsurePivot().transform.Find("LOD_Container");
            lodContainer = existing != null ? existing.gameObject : new GameObject("LOD_Container");
            lodContainer.transform.SetParent(EnsurePivot().transform, false);
            lodContainer.transform.localPosition = Vector3.zero;
            lodContainer.transform.localRotation = Quaternion.identity;
            lodContainer.transform.localScale = Vector3.one;
            return lodContainer;
        }

        private GameObject GetLod(int lodIndex)
        {
            switch (lodIndex)
            {
                case 0: return lod0;
                case 1: return lod1;
                case 2: return lod2;
                default: return null;
            }
        }

        private void SetLod(int lodIndex, GameObject lod)
        {
            switch (lodIndex)
            {
                case 0: lod0 = lod; break;
                case 1: lod1 = lod; break;
                case 2: lod2 = lod; break;
            }
        }

        private int ResolveLodIndex()
        {
            float squaredDistance = (detailFocus.position - transform.position).sqrMagnitude;
            int lodCount = Mathf.Clamp(config.LodCount, 1, 3);
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
    }
}
