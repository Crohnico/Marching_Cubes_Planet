using System;
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

        private bool hasCurrentChunk;
        private int3 currentChunk;

        public event Action<int3> OnChunkChanged;

        public int3 CurrentChunk => currentChunk;
        public bool HasCurrentChunk => hasCurrentChunk;

        private void Reset()
        {
            target = transform;
        }

        public void Configure(VoxelEngineConfig nextConfig, Transform nextTarget)
        {
            config = nextConfig;
            target = nextTarget;
            hasCurrentChunk = false;
            UpdateChunk();
        }

        private void OnEnable()
        {
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

            Transform trackedTarget = target != null ? target : transform;
            Vector3 position = trackedTarget.position;
            int3 nextChunk = VoxelChunkUtility.GetChunkCoords(new float3(position.x, position.y, position.z), config.ChunkSize);
            if (hasCurrentChunk && nextChunk.Equals(currentChunk))
            {
                return;
            }

            currentChunk = nextChunk;
            hasCurrentChunk = true;
            OnChunkChanged?.Invoke(currentChunk);
        }
    }
}
