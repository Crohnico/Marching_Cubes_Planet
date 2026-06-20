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

        public float Sample(float3 worldPosition)
        {
            return debugSphereRadius - math.distance(worldPosition, debugSphereCenter);
        }

        public int GetLayerIndex(float3 worldPosition)
        {
            float depth = debugSphereRadius - math.distance(worldPosition, debugSphereCenter);
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
    }
}
