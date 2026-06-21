using MarchingCubesPlanet.VoxelEngine.Data;
using Unity.Mathematics;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    [DisallowMultipleComponent]
    public sealed class PlayerChunkTracker : MonoBehaviour
    {
        [SerializeField] private VoxelEngineConfig config;
        [SerializeField] private Transform target;
        [SerializeField] private Camera chunkCullingCamera;
        [SerializeField] private bool enableChunkCulling = true;

        private bool hasCurrentChunk;
        private int3 currentChunk;

        public int3 CurrentChunk => currentChunk;
        public bool HasCurrentChunk => hasCurrentChunk;
        public Camera ChunkCullingCamera => chunkCullingCamera;
        public bool EnableChunkCulling => enableChunkCulling && chunkCullingCamera != null;
        public Transform TrackedTarget => target != null ? target : transform;
        public Vector3 TrackedPosition => TrackedTarget.position;

        private void Reset()
        {
            target = transform;
            chunkCullingCamera = GetComponentInChildren<Camera>();
        }

        public void Configure(VoxelEngineConfig nextConfig, Transform nextTarget, Camera nextChunkCullingCamera = null)
        {
            config = nextConfig;
            target = nextTarget;
            if (nextChunkCullingCamera != null)
            {
                chunkCullingCamera = nextChunkCullingCamera;
            }
            else if (chunkCullingCamera == null)
            {
                chunkCullingCamera = GetComponentInChildren<Camera>();
            }

            hasCurrentChunk = false;
            UpdateChunk();
        }

        private void OnEnable()
        {
            if (chunkCullingCamera == null)
            {
                chunkCullingCamera = GetComponentInChildren<Camera>();
            }

            UpdateChunk();
        }

        private void Update()
        {
            UpdateChunk();
        }

        private void UpdateChunk()
        {
            if (config == null)
            {
                return;
            }

            Vector3 position = TrackedPosition;
            int3 nextChunk = VoxelChunkUtility.GetChunkCoords(new float3(position.x, position.y, position.z), config.ChunkSize);
            if (hasCurrentChunk && nextChunk.Equals(currentChunk))
            {
                return;
            }

            currentChunk = nextChunk;
            hasCurrentChunk = true;
        }
    }
}
