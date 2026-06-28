using System.IO;
using UnityEngine;

namespace MarchingCubesPlanet.Lab
{
    public static class PlanetMemoryLabPaths
    {
        private const string EditorOutputDirectory = "Temp/PlanetLabMemory";
        private const string ReportSubdirectory = "Reports";
        private const string BudgetOverrideFileName = "planet-memory-budget.override.json";

        public static string GetOutputDirectory()
        {
            if (Application.isEditor)
            {
                return Path.Combine(GetProjectRoot(), EditorOutputDirectory);
            }

            return Application.persistentDataPath;
        }

        public static string GetReportDirectory()
        {
            return Path.Combine(GetOutputDirectory(), ReportSubdirectory);
        }

        public static string GetBudgetOverridePath()
        {
            return Path.Combine(GetOutputDirectory(), BudgetOverrideFileName);
        }

        public static void EnsureOutputDirectory()
        {
            Directory.CreateDirectory(GetOutputDirectory());
        }

        public static void EnsureReportDirectory()
        {
            Directory.CreateDirectory(GetReportDirectory());
        }

        private static string GetProjectRoot()
        {
            return Directory.GetCurrentDirectory();
        }
    }
}
