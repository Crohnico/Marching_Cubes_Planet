namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    internal struct VoxelChunkAltIndices
    {
        private const int InteriorSubMesh = 0;
        private const int TransitionSubMesh = 1;
        private const int SurfaceSubMesh = 2;

        public int interiorIndexCount;
        public int transitionIndexCount;
        public int surfaceIndexCount;

        public VoxelChunkAltIndices(int interiorIndexCount, int transitionIndexCount, int surfaceIndexCount)
        {
            this.interiorIndexCount = interiorIndexCount;
            this.transitionIndexCount = transitionIndexCount;
            this.surfaceIndexCount = surfaceIndexCount;
        }

        public int TotalIndexCount => interiorIndexCount + transitionIndexCount + surfaceIndexCount;

        public bool HasSubMesh(int subMeshIndex)
        {
            switch (subMeshIndex)
            {
                case InteriorSubMesh:
                    return interiorIndexCount > 0;
                case TransitionSubMesh:
                    return transitionIndexCount > 0;
                case SurfaceSubMesh:
                    return surfaceIndexCount > 0;
                default:
                    return false;
            }
        }
    }
}
