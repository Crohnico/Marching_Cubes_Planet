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
                isoLevel = isoLevel
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
                        nextDeclaredChunks.Add(new int3(x, y, z));
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
        }

        private void OnDisable()
        {
            if (chunkManager == null)
            {
                declaredChunks.Clear();
                return;
            }

            foreach (int3 chunkCoord in declaredChunks)
            {
                chunkManager.ReleaseChunk(chunkCoord);
            }

            declaredChunks.Clear();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.35f);
            Gizmos.DrawWireSphere(Center, radius);
        }
    }
}
