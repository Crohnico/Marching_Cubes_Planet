using System;

namespace MarchingCubesPlanet.MarchingCubes
{
    [Serializable]
    public struct PlanetTransvoxelFaceDescriptor
    {
        public const int Stride = 32;

        public int originX;
        public int originY;
        public int originZ;
        public int chunkSize;
        public int axis;
        public int sign;
        public int outputChunkIndex;
        public float transitionWidth;

        public PlanetTransvoxelFaceDescriptor(
            PlanetMarchingCubesChunkOrigin origin,
            int chunkSize,
            int axis,
            int sign,
            int outputChunkIndex,
            float transitionWidth)
        {
            originX = origin.x;
            originY = origin.y;
            originZ = origin.z;
            this.chunkSize = chunkSize;
            this.axis = axis;
            this.sign = sign >= 0 ? 1 : -1;
            this.outputChunkIndex = outputChunkIndex;
            this.transitionWidth = transitionWidth;
        }
    }
}
