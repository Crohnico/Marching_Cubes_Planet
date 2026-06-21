using Unity.Mathematics;

namespace MarchingCubesPlanet.VoxelEngine.Data
{
    public struct ScalarFieldSettings
    {
        public float3 debugSphereCenter;
        public float debugSphereRadius;
        public float isoLevel;
        public float surfaceLayerDepth;
        public float transitionLayerDepth;
        public PlanetNoiseSettings planetNoise;

        public float Sample(float3 worldPosition)
        {
            float surfaceOffset = planetNoise.enabled != 0
                ? planetNoise.SampleSurfaceOffset(worldPosition, debugSphereCenter, debugSphereRadius)
                : 0f;
            return debugSphereRadius + surfaceOffset - math.distance(worldPosition, debugSphereCenter);
        }

        public int GetLayerIndex(float3 worldPosition)
        {
            float surfaceOffset = planetNoise.enabled != 0
                ? planetNoise.SampleSurfaceOffset(worldPosition, debugSphereCenter, debugSphereRadius)
                : 0f;
            float depth = debugSphereRadius + surfaceOffset - math.distance(worldPosition, debugSphereCenter);
            if (depth <= surfaceLayerDepth)
            {
                return 2;
            }

            if (depth <= transitionLayerDepth)
            {
                return 1;
            }

            return 0;
        }

        public float2 GetAtlasUv(float3 worldPosition)
        {
            if (planetNoise.enabled == 0)
            {
                return new float2(0.5f, 0.5f);
            }

            return planetNoise.GetAtlasUv(worldPosition, debugSphereCenter, debugSphereRadius);
        }
    }
}
