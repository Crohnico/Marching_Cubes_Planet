using Unity.Mathematics;

namespace MarchingCubesPlanet.VoxelEngine.Data
{
    public struct ScalarFieldSettings
    {
        public float3 debugSphereCenter;
        public float debugSphereRadius;
        public float isoLevel;

        public float Sample(float3 worldPosition)
        {
            return debugSphereRadius - math.distance(worldPosition, debugSphereCenter);
        }
    }
}
