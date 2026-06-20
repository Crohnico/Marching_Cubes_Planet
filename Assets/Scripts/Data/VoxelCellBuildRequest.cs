using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace MarchingCubesPlanet.VoxelEngine.Data
{
    [StructLayout(LayoutKind.Sequential)]
    public struct VoxelCellBuildRequest
    {
        public int3 origin;
        public int size;
    }
}
