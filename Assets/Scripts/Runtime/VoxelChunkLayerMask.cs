namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    internal static class VoxelChunkLayerMask
    {
        public const int None = 0;
        public const int Interior = 1 << 0;
        public const int Transition = 1 << 1;
        public const int Surface = 1 << 2;
        public const int All = Interior | Transition | Surface;
    }
}
