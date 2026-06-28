using System.Collections.Generic;
using UnityEngine;

namespace MarchingCubesPlanet.Lab
{
    public sealed class PlanetLabResourceRegistry : MonoBehaviour
    {
        [SerializeField] private List<PlanetLabResourceRecord> records = new List<PlanetLabResourceRecord>(64);

        private int nextResourceId = 1;

        public IReadOnlyList<PlanetLabResourceRecord> Records => records;
        public int LiveResourceCount { get; private set; }
        public long OwnedCpuEstimatedBytes { get; private set; }
        public long OwnedGpuEstimatedBytes { get; private set; }
        public int LiveGraphicsBuffers { get; private set; }
        public int LiveComputeBuffers { get; private set; }
        public int LiveRenderTextures { get; private set; }
        public int LiveRuntimeMeshes { get; private set; }
        public int LiveRuntimeTextures { get; private set; }
        public int LiveRuntimeMaterials { get; private set; }

        public int RegisterResource(string resourceName, PlanetLabResourceType type, string ownerModule, long estimatedBytes)
        {
            return RegisterResource(resourceName, type, ownerModule, estimatedBytes, 0, 0);
        }

        public int RegisterResource(
            string resourceName,
            PlanetLabResourceType type,
            string ownerModule,
            long estimatedBytes,
            int elementCount,
            int stride)
        {
            if (estimatedBytes < 0)
            {
                estimatedBytes = 0;
            }

            PlanetLabResourceRecord record = new PlanetLabResourceRecord
            {
                resourceId = nextResourceId++,
                resourceName = string.IsNullOrWhiteSpace(resourceName) ? "Unnamed Resource" : resourceName,
                resourceType = type,
                ownerModule = string.IsNullOrWhiteSpace(ownerModule) ? "Unknown Owner" : ownerModule,
                elementCount = elementCount,
                stride = stride,
                estimatedBytes = estimatedBytes,
                createdAtFrame = Time.frameCount,
                isAlive = true
            };

            records.Add(record);
            RecalculateLiveTotals();
            return record.resourceId;
        }

        public bool TransferOwnership(int resourceId, string newOwnerModule)
        {
            for (int i = 0; i < records.Count; i++)
            {
                PlanetLabResourceRecord record = records[i];
                if (record.resourceId != resourceId)
                {
                    continue;
                }

                record.ownerModule = string.IsNullOrWhiteSpace(newOwnerModule) ? "Unknown Owner" : newOwnerModule;
                RecalculateLiveTotals();
                return true;
            }

            return false;
        }

        public bool TryGetResource(int resourceId, out PlanetLabResourceRecord record)
        {
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].resourceId == resourceId)
                {
                    record = records[i];
                    return true;
                }
            }

            record = null;
            return false;
        }

        public int CopyLiveRecords(PlanetLabResourceRecord[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < records.Count && count < buffer.Length; i++)
            {
                if (!records[i].isAlive)
                {
                    continue;
                }

                buffer[count++] = records[i];
            }

            return count;
        }

        public bool MarkReleased(int resourceId)
        {
            for (int i = 0; i < records.Count; i++)
            {
                PlanetLabResourceRecord record = records[i];
                if (record.resourceId != resourceId)
                {
                    continue;
                }

                if (!record.isAlive)
                {
                    return false;
                }

                record.isAlive = false;
                RecalculateLiveTotals();
                return true;
            }

            return false;
        }

        public void ClearReleasedRecords()
        {
            for (int i = records.Count - 1; i >= 0; i--)
            {
                if (!records[i].isAlive)
                {
                    records.RemoveAt(i);
                }
            }

            RecalculateLiveTotals();
        }

        public void ResetRegistry()
        {
            records.Clear();
            nextResourceId = 1;
            RecalculateLiveTotals();
        }

        public void RecalculateLiveTotals()
        {
            LiveResourceCount = 0;
            OwnedCpuEstimatedBytes = 0;
            OwnedGpuEstimatedBytes = 0;
            LiveGraphicsBuffers = 0;
            LiveComputeBuffers = 0;
            LiveRenderTextures = 0;
            LiveRuntimeMeshes = 0;
            LiveRuntimeTextures = 0;
            LiveRuntimeMaterials = 0;

            for (int i = 0; i < records.Count; i++)
            {
                PlanetLabResourceRecord record = records[i];
                if (!record.isAlive)
                {
                    continue;
                }

                LiveResourceCount++;

                if (IsGpuResource(record.resourceType))
                {
                    OwnedGpuEstimatedBytes += record.estimatedBytes;
                }
                else
                {
                    OwnedCpuEstimatedBytes += record.estimatedBytes;
                }

                CountType(record.resourceType);
            }
        }

        private static bool IsGpuResource(PlanetLabResourceType type)
        {
            return type == PlanetLabResourceType.GraphicsBuffer ||
                   type == PlanetLabResourceType.ComputeBuffer ||
                   type == PlanetLabResourceType.Mesh ||
                   type == PlanetLabResourceType.RenderTexture ||
                   type == PlanetLabResourceType.RuntimeTexture ||
                   type == PlanetLabResourceType.RuntimeMaterial;
        }

        private void CountType(PlanetLabResourceType type)
        {
            switch (type)
            {
                case PlanetLabResourceType.GraphicsBuffer:
                    LiveGraphicsBuffers++;
                    break;
                case PlanetLabResourceType.ComputeBuffer:
                    LiveComputeBuffers++;
                    break;
                case PlanetLabResourceType.RenderTexture:
                    LiveRenderTextures++;
                    break;
                case PlanetLabResourceType.Mesh:
                    LiveRuntimeMeshes++;
                    break;
                case PlanetLabResourceType.RuntimeTexture:
                    LiveRuntimeTextures++;
                    break;
                case PlanetLabResourceType.RuntimeMaterial:
                    LiveRuntimeMaterials++;
                    break;
            }
        }
    }
}
