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
        [SerializeField] private Camera chunkCullingCamera;
        [SerializeField] private bool enableChunkCulling = true;
        [SerializeField, Min(0f)] private float viewPositionUpdateThreshold = 1f;
        [SerializeField, Range(0f, 45f)] private float viewAngleUpdateThreshold = 1f;

        private bool hasCurrentChunk;
        private bool hasCurrentViewPose;
        private int3 currentChunk;
        private Vector3 currentViewPosition;
        private Quaternion currentViewRotation = Quaternion.identity;

        public event Action<int3> OnChunkChanged;
        public event Action OnViewChanged;

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
            UpdateView();
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
            OnChunkChanged?.Invoke(currentChunk);
        }

        private void UpdateView()
        {
            if (!EnableChunkCulling)
            {
                hasCurrentViewPose = false;
                return;
            }

            Transform viewTransform = chunkCullingCamera.transform;
            Vector3 nextPosition = viewTransform.position;
            Quaternion nextRotation = viewTransform.rotation;
            if (hasCurrentViewPose
                && !HasViewMoved(nextPosition)
                && !HasViewRotated(nextRotation))
            {
                return;
            }

            currentViewPosition = nextPosition;
            currentViewRotation = nextRotation;
            hasCurrentViewPose = true;
            OnViewChanged?.Invoke();
        }

        private bool HasViewMoved(Vector3 nextPosition)
        {
            float threshold = viewPositionUpdateThreshold;
            return (nextPosition - currentViewPosition).sqrMagnitude >= threshold * threshold;
        }

        private bool HasViewRotated(Quaternion nextRotation)
        {
            return Quaternion.Angle(currentViewRotation, nextRotation) >= viewAngleUpdateThreshold;
        }
    }
}
