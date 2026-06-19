using Unity.Collections;

namespace MarchingCubesPlanet.VoxelEngine.MarchingCubes
{
    public struct MarchingCubesCaseTable
    {
        public NativeArray<int> cornerIndexAFromEdge;
        public NativeArray<int> cornerIndexBFromEdge;
        public NativeArray<int> triangulation;

        public bool IsCreated => cornerIndexAFromEdge.IsCreated
            && cornerIndexBFromEdge.IsCreated
            && triangulation.IsCreated;

        public void Dispose()
        {
            if (cornerIndexAFromEdge.IsCreated)
            {
                cornerIndexAFromEdge.Dispose();
            }

            if (cornerIndexBFromEdge.IsCreated)
            {
                cornerIndexBFromEdge.Dispose();
            }

            if (triangulation.IsCreated)
            {
                triangulation.Dispose();
            }
        }
    }
}
