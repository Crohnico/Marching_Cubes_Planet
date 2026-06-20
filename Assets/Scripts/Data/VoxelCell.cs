using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace MarchingCubesPlanet.VoxelEngine.Data
{
    [StructLayout(LayoutKind.Sequential)]
    public struct VoxelCell
    {
        public int3 origin;
        public int size;
        public byte corners;
    }
}
