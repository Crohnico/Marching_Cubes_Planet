using MarchingCubesPlanet.VoxelEngine.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MarchingCubesPlanet.VoxelEngine.Jobs
{
    [BurstCompile]
    public struct EvaluateVoxelCellsJob : IJobParallelFor
    {
        public ScalarFieldSettings scalarField;

        [ReadOnly] public NativeArray<VoxelCellBuildRequest> requests;
        [WriteOnly] public NativeArray<VoxelCell> cells;

        public void Execute(int index)
        {
            VoxelCellBuildRequest request = requests[index];
            int3 origin = request.origin;
            int size = request.size;

            byte corners = 0;
            for (int i = 0; i < 8; i++)
            {
                int3 samplePosition = origin + GetCornerOffset(i, size);
                float value = scalarField.Sample(new float3(samplePosition.x, samplePosition.y, samplePosition.z));
                if (value > scalarField.isoLevel)
                {
                    corners |= (byte)(1 << i);
                }
            }

            cells[index] = new VoxelCell
            {
                origin = origin,
                size = size,
                corners = corners,
                boundarySides = request.boundarySides,
                type = SampleMaterialType(origin)
            };
        }

        private static int SampleMaterialType(int3 unitCellOrigin)
        {
            // Debug material until a real material provider exists.
            return 1 + math.abs((unitCellOrigin.x * 17 + unitCellOrigin.y * 31 + unitCellOrigin.z * 47) % 3);
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
