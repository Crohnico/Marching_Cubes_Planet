using MarchingCubesPlanet.VoxelEngine.Data;
using Unity.Mathematics;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    public sealed class PlanetInitializationSettings
    {
        public string stellarID;
        public string planetID;
        public float3 worldPosition;
        public float radius;
        public int seed;
        public int3 chunkSize;
        public int cellSize;
        public int3 detailFocusKey;
        public int segmentCount;
        public int3 segmentGrid;
        public Matrix4x4 worldToPlanetLocal;
        public float3 sphereLocalPosition;
        public float maximumTerrainRadius;
        public ScalarFieldSettings scalarField;
    }
}
