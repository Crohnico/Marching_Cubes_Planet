using System;
using System.Collections.Generic;
using Unity.Profiling;

namespace MarchingCubesPlanet.Lab
{
    public sealed class PlanetMemoryProfilerRecorderSet : IDisposable
    {
        private readonly List<CounterSlot> counters = new List<CounterSlot>(32);

        public int Count => counters.Count;
        public int AvailableCount { get; private set; }
        public int UnavailableCount => counters.Count - AvailableCount;

        public void InitializeDefaultCounters()
        {
            Dispose();
            AddMemory("App Resident Memory");
            AddMemory("App Committed Memory");
            AddMemory("Total Used Memory");
            AddMemory("Total Reserved Memory");
            AddMemory("System Used Memory");
            AddMemory("System Total Used Memory");
            AddMemory("GC Used Memory");
            AddMemory("GC Reserved Memory");
            AddMemory("GC Allocated In Frame");
            AddMemory("GC Allocation In Frame Count");
            AddRender("Used Buffers Bytes");
            AddRender("Used Buffers Count");
            AddRender("Render Textures Bytes");
            AddRender("Render Textures Count");
            AddRender("Used Textures Bytes");
            AddRender("Used Textures Count");
            AddMemory("Gfx Used Memory");
            AddMemory("Gfx Reserved Memory");
            AddMemory("Texture Memory");
            AddMemory("Mesh Memory");
            AddRender("Draw Calls Count");
            AddRender("SetPass Calls Count");
            AddRender("Triangles Count");
            AddRender("Vertices Count");
            AddRender("Vertex Buffer Upload In Frame Bytes");
            AddRender("Index Buffer Upload In Frame Bytes");
            AddRender("Vertex Buffer Upload In Frame Count");
            AddRender("Index Buffer Upload In Frame Count");
            AddMemory("Profiler Used Memory");
            AddMemory("Profiler Reserved Memory");
            AddMemory("Object Count");
            AddMemory("Asset Count");
            AddMemory("GameObject Count");
            AddMemory("Scene Object Count");
            AddMemory("Material Count");
            AddMemory("Mesh Count");
            AddMemory("Texture Count");
        }

        public PlanetMemoryProfilerCounterValue[] CaptureValues()
        {
            PlanetMemoryProfilerCounterValue[] values = new PlanetMemoryProfilerCounterValue[counters.Count];
            for (int i = 0; i < counters.Count; i++)
            {
                CounterSlot slot = counters[i];
                long value = 0;
                bool available = slot.isAvailable;

                if (available)
                {
                    value = slot.recorder.LastValue;
                }

                values[i] = new PlanetMemoryProfilerCounterValue
                {
                    category = slot.category,
                    counterName = slot.counterName,
                    isAvailable = available,
                    lastValue = value
                };
            }

            return values;
        }

        public void Dispose()
        {
            for (int i = 0; i < counters.Count; i++)
            {
                if (counters[i].recorder.Valid)
                {
                    counters[i].recorder.Dispose();
                }
            }

            counters.Clear();
            AvailableCount = 0;
        }

        private void AddMemory(string counterName)
        {
            AddCounter(PlanetMemoryProfilerCategoryKind.Memory, ProfilerCategory.Memory, counterName);
        }

        private void AddRender(string counterName)
        {
            AddCounter(PlanetMemoryProfilerCategoryKind.Render, ProfilerCategory.Render, counterName);
        }

        private void AddCounter(PlanetMemoryProfilerCategoryKind categoryKind, ProfilerCategory category, string counterName)
        {
            CounterSlot slot = new CounterSlot
            {
                category = categoryKind,
                counterName = counterName
            };

            try
            {
                slot.recorder = ProfilerRecorder.StartNew(category, counterName, 1);
                slot.isAvailable = slot.recorder.Valid;
            }
            catch (Exception)
            {
                slot.isAvailable = false;
            }

            if (slot.isAvailable)
            {
                AvailableCount++;
            }

            counters.Add(slot);
        }

        private struct CounterSlot
        {
            public PlanetMemoryProfilerCategoryKind category;
            public string counterName;
            public bool isAvailable;
            public ProfilerRecorder recorder;
        }
    }
}
