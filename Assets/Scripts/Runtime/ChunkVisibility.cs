namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    internal struct ChunkVisibility
    {
        public bool inRange;
        public bool inFrustum;

        public static ChunkVisibility Visible => new ChunkVisibility
        {
            inRange = true,
            inFrustum = true
        };

        public bool IsVisible => inRange && inFrustum;
    }
}
