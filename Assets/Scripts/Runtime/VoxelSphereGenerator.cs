using System.Collections.Generic;
using MarchingCubesPlanet.VoxelEngine.Data;
using Unity.Mathematics;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    [DisallowMultipleComponent]
    public sealed class VoxelSphereGenerator : MonoBehaviour
    {
        [SerializeField] private VoxelChunkManager chunkManager;
        [SerializeField] private Transform centerOverride;
        [SerializeField] private Vector3 center = new Vector3(18f, 18f, 18f);
        [SerializeField, Min(0.01f)] private float radius = 14f;
        [SerializeField] private float isoLevel;
        [SerializeField, Min(0f)] private float surfaceLayerDepth = 64f;
        [SerializeField, Min(0f)] private float transitionLayerDepth = 192f;
        [SerializeField] private bool declareOnlySurfaceChunks = true;
        [SerializeField, Min(0f)] private float chunkDeclarationPadding = 0f;

        private readonly HashSet<int3> declaredChunks = new HashSet<int3>();

        public float Radius => radius;
        public Vector3 Center => centerOverride != null ? centerOverride.position : center;

        private void Reset()
        {
            TryGetComponent(out chunkManager);
        }

        public void Configure(VoxelChunkManager nextChunkManager)
        {
            chunkManager = nextChunkManager;
        }

        public ScalarFieldSettings BuildScalarFieldSettings()
        {
            Vector3 resolvedCenter = Center;
            return new ScalarFieldSettings
            {
                debugSphereCenter = new float3(resolvedCenter.x, resolvedCenter.y, resolvedCenter.z),
                debugSphereRadius = radius,
                isoLevel = isoLevel,
                surfaceLayerDepth = surfaceLayerDepth,
                transitionLayerDepth = Mathf.Max(surfaceLayerDepth, transitionLayerDepth)
            };
        }

        public void DeclareOccupiedChunks(VoxelChunkManager manager, int3 chunkSize)
        {
            if (manager == null)
            {
                return;
            }

            Vector3 resolvedCenter = Center;
            float3 min = new float3(
                resolvedCenter.x - radius,
                resolvedCenter.y - radius,
                resolvedCenter.z - radius);
            float3 max = new float3(
                resolvedCenter.x + radius,
                resolvedCenter.y + radius,
                resolvedCenter.z + radius);

            int3 minChunk = VoxelChunkUtility.GetChunkCoords(min, chunkSize);
            int3 maxChunk = VoxelChunkUtility.GetChunkCoords(max, chunkSize);

            HashSet<int3> nextDeclaredChunks = new HashSet<int3>();
            for (int x = minChunk.x; x <= maxChunk.x; x++)
            {
                for (int y = minChunk.y; y <= maxChunk.y; y++)
                {
                    for (int z = minChunk.z; z <= maxChunk.z; z++)
                    {
                        int3 chunkCoord = new int3(x, y, z);
                        if (!declareOnlySurfaceChunks || DoesChunkIntersectSurface(chunkCoord, chunkSize, resolvedCenter))
                        {
                            nextDeclaredChunks.Add(chunkCoord);
                        }
                    }
                }
            }

            foreach (int3 chunkCoord in declaredChunks)
            {
                if (!nextDeclaredChunks.Contains(chunkCoord))
                {
                    manager.ReleaseChunk(chunkCoord);
                }
            }

            foreach (int3 chunkCoord in nextDeclaredChunks)
            {
                if (!declaredChunks.Contains(chunkCoord))
                {
                    manager.DeclareChunk(chunkCoord);
                }
            }

            declaredChunks.Clear();
            foreach (int3 chunkCoord in nextDeclaredChunks)
            {
                declaredChunks.Add(chunkCoord);
            }
        }

        private void ReleaseDeclaredChunks(VoxelChunkManager manager)
        {
            foreach (int3 chunkCoord in declaredChunks)
            {
                manager.ReleaseChunk(chunkCoord);
            }

            declaredChunks.Clear();
        }

        [ContextMenu("Generate")]
        public void Generate()
        {
            if (chunkManager == null)
            {
                Debug.LogWarning("VoxelSphereGenerator necesita un VoxelChunkManager asignado.", this);
                return;
            }

            chunkManager.Generate();
        }

        private void OnValidate()
        {
            radius = Mathf.Max(0.01f, radius);
            surfaceLayerDepth = Mathf.Max(0f, surfaceLayerDepth);
            transitionLayerDepth = Mathf.Max(surfaceLayerDepth, transitionLayerDepth);
            chunkDeclarationPadding = Mathf.Max(0f, chunkDeclarationPadding);
        }

        private bool DoesChunkIntersectSurface(int3 chunkCoord, int3 chunkSize, Vector3 sphereCenter)
        {
            int3 chunkOrigin = VoxelChunkUtility.GetChunkOrigin(chunkCoord, chunkSize);
            Vector3 min = new Vector3(chunkOrigin.x, chunkOrigin.y, chunkOrigin.z);
            Vector3 max = min + new Vector3(chunkSize.x, chunkSize.y, chunkSize.z);
            float minDistanceSquared = GetSquaredDistanceToAabb(sphereCenter, min, max);
            float maxDistanceSquared = GetMaxSquaredDistanceToAabb(sphereCenter, min, max);
            float minRadius = Mathf.Max(0f, radius - chunkDeclarationPadding);
            float maxRadius = radius + chunkDeclarationPadding;
            return minDistanceSquared <= maxRadius * maxRadius
                && maxDistanceSquared >= minRadius * minRadius;
        }

        private static float GetSquaredDistanceToAabb(Vector3 point, Vector3 min, Vector3 max)
        {
            float squaredDistance = 0f;
            squaredDistance += GetSquaredDistanceToRange(point.x, min.x, max.x);
            squaredDistance += GetSquaredDistanceToRange(point.y, min.y, max.y);
            squaredDistance += GetSquaredDistanceToRange(point.z, min.z, max.z);
            return squaredDistance;
        }

        private static float GetSquaredDistanceToRange(float value, float min, float max)
        {
            if (value < min)
            {
                float delta = min - value;
                return delta * delta;
            }

            if (value > max)
            {
                float delta = value - max;
                return delta * delta;
            }

            return 0f;
        }

        private static float GetMaxSquaredDistanceToAabb(Vector3 point, Vector3 min, Vector3 max)
        {
            float x = Mathf.Max(Mathf.Abs(point.x - min.x), Mathf.Abs(point.x - max.x));
            float y = Mathf.Max(Mathf.Abs(point.y - min.y), Mathf.Abs(point.y - max.y));
            float z = Mathf.Max(Mathf.Abs(point.z - min.z), Mathf.Abs(point.z - max.z));
            return x * x + y * y + z * z;
        }

        private void OnDisable()
        {
            if (chunkManager == null)
            {
                declaredChunks.Clear();
                return;
            }

            ReleaseDeclaredChunks(chunkManager);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.35f);
            Gizmos.DrawWireSphere(Center, radius);
        }
    }
}
