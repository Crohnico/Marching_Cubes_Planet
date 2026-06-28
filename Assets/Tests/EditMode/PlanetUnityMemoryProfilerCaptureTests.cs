using MarchingCubesPlanet.Lab;
using NUnit.Framework;

namespace MarchingCubesPlanet.Lab.Tests
{
    public sealed class PlanetUnityMemoryProfilerCaptureTests
    {
        [Test]
        public void CaptureUnavailableDoesNotThrowWithoutPackage()
        {
            PlanetUnityMemoryProfilerCaptureResult result = PlanetUnityMemoryProfilerCapture.Capture("Test");

            Assert.IsFalse(result.isAvailable);
            Assert.AreEqual(PlanetLabDiagnosticSeverity.Warning, result.diagnostic.severity);
            Assert.IsNotNull(result.diagnostic.recommendedAction);
        }
    }
}
