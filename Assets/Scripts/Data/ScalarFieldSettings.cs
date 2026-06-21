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

        public float2 GetAtlasUv(float3 worldPosition, float3 normal)
        {
            if (planetNoise.enabled == 0)
            {
                return new float2(0.5f, 0.5f);
            }

            float3 radialDirection = math.normalizesafe(
                worldPosition - debugSphereCenter,
                new float3(0f, 1f, 0f));
            float radialAlignment = math.abs(math.dot(math.normalizesafe(normal), radialDirection));
            if (radialAlignment >= 0.55f)
            {
                return planetNoise.GetAtlasUv(worldPosition, debugSphereCenter, debugSphereRadius);
            }

            return GetRadialHeightAtlasUv(worldPosition);
        }

        private float2 GetRadialHeightAtlasUv(float3 worldPosition)
        {
            float safeRadius = math.max(0.0001f, debugSphereRadius);
            float heightOffset = math.distance(worldPosition, debugSphereCenter) - safeRadius;
            float minHeightAtlasOffset = -safeRadius * math.max(planetNoise.oceanDepth, planetNoise.minimumOceanDepth)
                - safeRadius * planetNoise.surfaceNoiseAmplitude;
            float maxHeightAtlasOffset = safeRadius * planetNoise.landMaxElevation
                + safeRadius * planetNoise.surfaceNoiseAmplitude;

            if (heightOffset <= 0f)
            {
                float depthRange = math.max(0.0001f, -minHeightAtlasOffset);
                float underwaterHeight = math.saturate((heightOffset - minHeightAtlasOffset) / depthRange);
                return new float2(0.5f, math.lerp(0f, planetNoise.seaLevelAtlasV, underwaterHeight));
            }

            float landRange = math.max(0.0001f, maxHeightAtlasOffset);
            float landHeight = math.saturate(heightOffset / landRange);
            return new float2(0.5f, math.lerp(planetNoise.seaLevelAtlasV, 1f, landHeight));
        }
    }
}
