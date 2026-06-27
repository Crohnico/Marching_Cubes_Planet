using System;

namespace MarchingCubesPlanet.Lab
{
    [Serializable]
    public class PlanetLabResourceRecord
    {
        public int resourceId;
        public string resourceName;
        public PlanetLabResourceType resourceType;
        public string ownerModule;
        public int elementCount;
        public int stride;
        public long estimatedBytes;
        public int createdAtFrame;
        public bool isAlive;
    }
}
