using System;

namespace MarchingCubesPlanet.Lab
{
    [Serializable]
    public struct PlanetMemoryProfilerCounterValue
    {
        public PlanetMemoryProfilerCategoryKind category;
        public string counterName;
        public bool isAvailable;
        public long lastValue;
    }
}
