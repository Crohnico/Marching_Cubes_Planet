using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    [DisallowMultipleComponent]
    public sealed class PlayerChunkTracker : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Camera chunkCullingCamera;
        [SerializeField] private bool enableChunkCulling = true;

        public Camera ChunkCullingCamera => chunkCullingCamera;
        public bool EnableChunkCulling => enableChunkCulling && chunkCullingCamera != null;
        public Transform TrackedTarget => target != null ? target : transform;
        public Vector3 TrackedPosition => TrackedTarget.position;

        private void Reset()
        {
            target = transform;
            chunkCullingCamera = GetComponentInChildren<Camera>();
        }

        public void Configure(Transform nextTarget, Camera nextChunkCullingCamera = null)
        {
            target = nextTarget;
            if (nextChunkCullingCamera != null)
            {
                chunkCullingCamera = nextChunkCullingCamera;
            }
            else if (chunkCullingCamera == null)
            {
                chunkCullingCamera = GetComponentInChildren<Camera>();
            }
        }

        private void OnEnable()
        {
            if (chunkCullingCamera == null)
            {
                chunkCullingCamera = GetComponentInChildren<Camera>();
            }
        }
    }
}
