namespace MarchingCubesPlanet.Compute
{
    public static class PlanetComputeMemory
    {
        public const int Float4Stride = 16;

        public static long EstimateBufferBytes(int elementCount, int stride)
        {
            if (elementCount <= 0 || stride <= 0)
            {
                return 0;
            }

            return (long)elementCount * stride;
        }
    }
}
