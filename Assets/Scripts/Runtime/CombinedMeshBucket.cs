using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    internal sealed class CombinedMeshBucket
    {
        public GameObject owner;
        public MeshFilter meshFilter;
        public MeshRenderer meshRenderer;
        public Mesh mesh;
        public VoxelSegmentLodMeshCache lodCache;
        public Mesh[] lodMeshes;
        public bool[] lodDirty;
        public bool[] lodCached;
        public int activeLodIndex;
        public CombineInstance[] combineInstanceBuffer;
        public bool dirty;
    }
}
