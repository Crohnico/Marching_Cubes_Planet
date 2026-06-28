using System;

namespace MarchingCubesPlanet.Lab
{
    [Serializable]
    public struct PlanetUnityMemoryProfilerCaptureResult
    {
        public bool isAvailable;
        public string snapshotPath;
        public double operationMs;
        public PlanetLabDiagnostic diagnostic;
    }

    public static class PlanetUnityMemoryProfilerCapture
    {
        public static PlanetUnityMemoryProfilerCaptureResult Capture(string operationName)
        {
            PlanetLabDiagnostic diagnostic = PlanetLabDiagnostic.Warning(
                "Unity Memory Profiler capture is unavailable",
                "The project does not currently reference the Unity Memory Profiler package, so the Lab cannot request an official .snap capture.",
                "Install the official Unity Memory Profiler package when deep snapshot capture is needed. Until then, use owned snapshots and ProfilerRecorder counters.",
                "operationName=" + operationName);

            return new PlanetUnityMemoryProfilerCaptureResult
            {
                isAvailable = false,
                snapshotPath = string.Empty,
                operationMs = 0,
                diagnostic = diagnostic
            };
        }
    }
}
