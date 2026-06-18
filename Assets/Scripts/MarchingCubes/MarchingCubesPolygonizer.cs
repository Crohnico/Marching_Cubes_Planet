using UnityEngine;

namespace MarchingCubesPlanet.MarchingCubes
{
    public static class MarchingCubesPolygonizer
    {
        public static MarchingCubesMeshData Polygonize(MarchingCube cube, float isoLevel = 0f)
        {
            MarchingCubesMeshData meshData = new MarchingCubesMeshData();
            MarchingCubesCaseResolver.Polygonize(cube, isoLevel, meshData);
            return meshData;
        }

        public static int Polygonize(MarchingCube cube, float isoLevel, MarchingCubesMeshData meshData)
        {
            return MarchingCubesCaseResolver.Polygonize(cube, isoLevel, meshData);
        }

        public static Mesh BuildMesh(MarchingCube cube, float isoLevel = 0f, string meshName = "Marching Cubes Cube")
        {
            MarchingCubesMeshData meshData = Polygonize(cube, isoLevel);
            return meshData.ToMesh(meshName);
        }
    }
}
