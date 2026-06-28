using System.IO;
using MarchingCubesPlanet.Lab;
using NUnit.Framework;

namespace MarchingCubesPlanet.Lab.Tests
{
    public sealed class PlanetMemoryLabPathsTests
    {
        [Test]
        public void EditorBudgetOverridePathLivesInsideProjectTemp()
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string overridePath = PlanetMemoryLabPaths.GetBudgetOverridePath();

            Assert.IsTrue(overridePath.StartsWith(projectRoot));
            Assert.IsTrue(overridePath.Contains(Path.Combine("Temp", "PlanetLabMemory")));
            Assert.IsFalse(overridePath.Contains(Path.Combine("Assets", string.Empty)));
        }

        [Test]
        public void EditorReportDirectoryLivesOutsideAssets()
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string reportDirectory = PlanetMemoryLabPaths.GetReportDirectory();

            Assert.IsTrue(reportDirectory.StartsWith(projectRoot));
            Assert.IsTrue(reportDirectory.Contains(Path.Combine("Temp", "PlanetLabMemory", "Reports")));
            Assert.IsFalse(reportDirectory.Contains(Path.Combine("Assets", string.Empty)));
        }
    }
}
