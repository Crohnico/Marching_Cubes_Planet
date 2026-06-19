using MarchingCubesPlanet.VoxelEngine.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MarchingCubesPlanet.VoxelEngine.Jobs
{
    [BurstCompile]
    public struct BuildVoxelCellLookupJob : IJob
    {
        [ReadOnly] public NativeArray<VoxelCell> cells;

        public int3 chunkOrigin;
        public int3 chunkSize;
        public NativeArray<byte> cornersByUnitCell;

        public void Execute()
        {
            for (int i = 0; i < cells.Length; i++)
            {
                VoxelCell cell = cells[i];
                int3 localOrigin = cell.origin - chunkOrigin;
                for (int x = 0; x < cell.size; x++)
                {
                    for (int y = 0; y < cell.size; y++)
                    {
                        for (int z = 0; z < cell.size; z++)
                        {
                            int3 local = localOrigin + new int3(x, y, z);
                            cornersByUnitCell[FlattenIndex(local, chunkSize)] = cell.corners;
                        }
                    }
                }
            }
        }

        private static int FlattenIndex(int3 local, int3 size)
        {
            return local.x + size.x * (local.y + size.y * local.z);
        }
    }

    [BurstCompile]
    public struct GenerateChunkMeshJob : IJob
    {
        [ReadOnly] public NativeArray<VoxelCell> cells;
        [ReadOnly] public NativeArray<byte> cornersByUnitCell;
        [ReadOnly] public NativeArray<int> cornerIndexAFromEdge;
        [ReadOnly] public NativeArray<int> cornerIndexBFromEdge;
        [ReadOnly] public NativeArray<int> triangulation;

        public int3 chunkOrigin;
        public int3 chunkSize;
        public byte declaredNeighborSides;
        public ScalarFieldSettings scalarField;
        public NativeList<float3> vertices;
        public NativeList<int> indices;

        public void Execute()
        {
            for (int i = 0; i < cells.Length; i++)
            {
                VoxelCell cell = cells[i];
                int caseIndex = cell.corners;
                if (caseIndex == 0)
                {
                    continue;
                }

                if (caseIndex == 255)
                {
                    AddExposedSolidCell(cell);
                    continue;
                }

                int rowStart = caseIndex * 16;
                for (int edgeIndexOffset = 0; edgeIndexOffset < 16; edgeIndexOffset += 3)
                {
                    int edgeA = triangulation[rowStart + edgeIndexOffset];
                    if (edgeA < 0)
                    {
                        break;
                    }

                    int edgeB = triangulation[rowStart + edgeIndexOffset + 1];
                    int edgeC = triangulation[rowStart + edgeIndexOffset + 2];
                    AddMarchingCubesTriangle(
                        InterpolateEdge(cell, edgeA),
                        InterpolateEdge(cell, edgeB),
                        InterpolateEdge(cell, edgeC));
                }
            }
        }

        private void AddExposedSolidCell(VoxelCell cell)
        {
            if (HasPureAirNeighbor(cell, new int3(0, 0, -1), 1 << 4))
            {
                AddCellTriangle(cell, 0, 2, 1);
                AddCellTriangle(cell, 0, 3, 2);
            }

            if (HasPureAirNeighbor(cell, new int3(0, 0, 1), 1 << 5))
            {
                AddCellTriangle(cell, 4, 5, 6);
                AddCellTriangle(cell, 4, 6, 7);
            }

            if (HasPureAirNeighbor(cell, new int3(-1, 0, 0), 1 << 0))
            {
                AddCellTriangle(cell, 0, 4, 7);
                AddCellTriangle(cell, 0, 7, 3);
            }

            if (HasPureAirNeighbor(cell, new int3(1, 0, 0), 1 << 1))
            {
                AddCellTriangle(cell, 1, 2, 6);
                AddCellTriangle(cell, 1, 6, 5);
            }

            if (HasPureAirNeighbor(cell, new int3(0, -1, 0), 1 << 2))
            {
                AddCellTriangle(cell, 0, 1, 5);
                AddCellTriangle(cell, 0, 5, 4);
            }

            if (HasPureAirNeighbor(cell, new int3(0, 1, 0), 1 << 3))
            {
                AddCellTriangle(cell, 3, 7, 6);
                AddCellTriangle(cell, 3, 6, 2);
            }
        }

        private bool HasPureAirNeighbor(VoxelCell cell, int3 direction, byte chunkBoundarySide)
        {
            if (IsNeighborFaceInsideChunk(cell, direction))
            {
                return HasPureAirNeighborInsideChunk(cell, direction);
            }

            if ((declaredNeighborSides & chunkBoundarySide) == 0)
            {
                return true;
            }

            return IsSampledNeighborPureAir(cell, direction);
        }

        private bool HasPureAirNeighborInsideChunk(VoxelCell cell, int3 direction)
        {
            int3 localOrigin = cell.origin - chunkOrigin;
            int size = cell.size;

            if (direction.x != 0)
            {
                int x = direction.x > 0 ? localOrigin.x + size : localOrigin.x - 1;
                for (int y = 0; y < size; y++)
                {
                    for (int z = 0; z < size; z++)
                    {
                        if (cornersByUnitCell[FlattenIndex(new int3(x, localOrigin.y + y, localOrigin.z + z))] == 0)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            if (direction.y != 0)
            {
                int y = direction.y > 0 ? localOrigin.y + size : localOrigin.y - 1;
                for (int x = 0; x < size; x++)
                {
                    for (int z = 0; z < size; z++)
                    {
                        if (cornersByUnitCell[FlattenIndex(new int3(localOrigin.x + x, y, localOrigin.z + z))] == 0)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            int fixedZ = direction.z > 0 ? localOrigin.z + size : localOrigin.z - 1;
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    if (cornersByUnitCell[FlattenIndex(new int3(localOrigin.x + x, localOrigin.y + y, fixedZ))] == 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsSampledNeighborPureAir(VoxelCell cell, int3 direction)
        {
            int3 neighborOrigin = cell.origin + direction * cell.size;
            for (int i = 0; i < 8; i++)
            {
                int3 samplePosition = neighborOrigin + GetCornerOffset(i, cell.size);
                float value = scalarField.Sample(new float3(samplePosition.x, samplePosition.y, samplePosition.z));
                if (value > scalarField.isoLevel)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsNeighborFaceInsideChunk(VoxelCell cell, int3 direction)
        {
            int3 localOrigin = cell.origin - chunkOrigin;
            if (direction.x < 0)
            {
                return localOrigin.x > 0;
            }

            if (direction.x > 0)
            {
                return localOrigin.x + cell.size < chunkSize.x;
            }

            if (direction.y < 0)
            {
                return localOrigin.y > 0;
            }

            if (direction.y > 0)
            {
                return localOrigin.y + cell.size < chunkSize.y;
            }

            if (direction.z < 0)
            {
                return localOrigin.z > 0;
            }

            return localOrigin.z + cell.size < chunkSize.z;
        }

        private int FlattenIndex(int3 local)
        {
            return local.x + chunkSize.x * (local.y + chunkSize.y * local.z);
        }

        private void AddCellTriangle(VoxelCell cell, int cornerA, int cornerB, int cornerC)
        {
            AddTriangle(
                GetCornerPosition(cell, cornerA),
                GetCornerPosition(cell, cornerB),
                GetCornerPosition(cell, cornerC));
        }

        private void AddMarchingCubesTriangle(float3 a, float3 b, float3 c)
        {
            // The table winding assumes the opposite inside/outside bit convention.
            // In this engine corner bit 1 means solid, so partial cells need reversed winding.
            AddTriangle(a, c, b);
        }

        private void AddTriangle(float3 a, float3 b, float3 c)
        {
            float3 localOrigin = new float3(chunkOrigin.x, chunkOrigin.y, chunkOrigin.z);
            int vertexIndex = vertices.Length;
            vertices.AddNoResize(a - localOrigin);
            vertices.AddNoResize(b - localOrigin);
            vertices.AddNoResize(c - localOrigin);

            indices.AddNoResize(vertexIndex);
            indices.AddNoResize(vertexIndex + 1);
            indices.AddNoResize(vertexIndex + 2);
        }

        private float3 InterpolateEdge(VoxelCell cell, int edgeIndex)
        {
            float3 from = GetCornerPosition(cell, cornerIndexAFromEdge[edgeIndex]);
            float3 to = GetCornerPosition(cell, cornerIndexBFromEdge[edgeIndex]);
            float fromValue = scalarField.Sample(from);
            float toValue = scalarField.Sample(to);
            float delta = toValue - fromValue;

            if (math.abs(delta) <= 0.000001f)
            {
                return from;
            }

            float t = math.saturate((scalarField.isoLevel - fromValue) / delta);
            return math.lerp(from, to, t);
        }

        private static float3 GetCornerPosition(VoxelCell cell, int cornerIndex)
        {
            int3 position = cell.origin + GetCornerOffset(cornerIndex, cell.size);
            return new float3(position.x, position.y, position.z);
        }

        private static int3 GetCornerOffset(int index, int size)
        {
            switch (index)
            {
                case 0:
                    return new int3(0, 0, 0);
                case 1:
                    return new int3(size, 0, 0);
                case 2:
                    return new int3(size, size, 0);
                case 3:
                    return new int3(0, size, 0);
                case 4:
                    return new int3(0, 0, size);
                case 5:
                    return new int3(size, 0, size);
                case 6:
                    return new int3(size, size, size);
                default:
                    return new int3(0, size, size);
            }
        }
    }
}
