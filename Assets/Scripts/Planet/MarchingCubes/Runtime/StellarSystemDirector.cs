using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace MarchingCubesPlanet.MarchingCubes
{
    [DisallowMultipleComponent]
    public sealed class StellarSystemDirector : MonoBehaviour
    {
        private const string CacheRootFolderName = "StellarSystemShellCache";
        private const string SystemIdFileName = "stellar_system_id";
        private const string ShellsFolderName = "shells";
        private const string ShellCacheExtension = ".pshellgpu";

        [SerializeField] private int stellarSystemSeedId = 1;
        [SerializeField] private PlanetDirector[] planets = Array.Empty<PlanetDirector>();
        [SerializeField] private PlanetChunkLod shellLod = PlanetChunkLod.LOD2;
        [SerializeField] private bool loadShellsOnStart = true;
        [SerializeField] private bool discoverChildPlanets = true;
        [SerializeField] private bool clearCacheWhenSystemIdChanges = true;

        private string CacheRootPath => Path.Combine(Application.persistentDataPath, CacheRootFolderName);
        private string ShellsPath => Path.Combine(CacheRootPath, ShellsFolderName);

        private void Awake()
        {
            ResolvePlanets();
            for (int i = 0; i < planets.Length; i++)
            {
                if (planets[i] != null)
                {
                    planets[i].SetAutoLoadShellOnStart(false);
                }
            }
        }

        private void Start()
        {
            if (!loadShellsOnStart)
            {
                return;
            }

            LoadShells();
        }

        public void LoadShells()
        {
            ResolvePlanets();
            PrepareCacheRoot();

            for (int i = 0; i < planets.Length; i++)
            {
                PlanetDirector planet = planets[i];
                if (planet == null)
                {
                    continue;
                }

                int planetSeed = unchecked(stellarSystemSeedId + i);
                planet.SetAutoLoadShellOnStart(false);
                planet.SetPlanetSeed(planetSeed);
                planet.LoadShellFromCacheOrGenerate(BuildShellCachePath(planet, i), shellLod);
            }
        }

        public void SetStellarSystemSeedId(int seedId)
        {
            if (stellarSystemSeedId == seedId)
            {
                return;
            }

            stellarSystemSeedId = seedId;
        }

        private void ResolvePlanets()
        {
            if (planets != null && planets.Length > 0)
            {
                return;
            }

            if (discoverChildPlanets)
            {
                planets = GetComponentsInChildren<PlanetDirector>(true);
                if (planets.Length > 0)
                {
                    return;
                }
            }

            planets = FindObjectsByType<PlanetDirector>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
        }

        private void PrepareCacheRoot()
        {
            string rootPath = CacheRootPath;
            string systemIdPath = Path.Combine(rootPath, SystemIdFileName);
            string expectedSystemId = stellarSystemSeedId.ToString(CultureInfo.InvariantCulture);

            if (clearCacheWhenSystemIdChanges && Directory.Exists(rootPath))
            {
                string currentSystemId = File.Exists(systemIdPath)
                    ? File.ReadAllText(systemIdPath).Trim()
                    : string.Empty;
                if (string.IsNullOrEmpty(currentSystemId) ||
                    !string.Equals(currentSystemId, expectedSystemId, StringComparison.Ordinal))
                {
                    Debug.Log(
                        "<color=#FF4040>[Shell Cache] Clearing stellar system cache. oldSystemId=" +
                        (string.IsNullOrEmpty(currentSystemId) ? "<none>" : currentSystemId) +
                        " newSystemId=" +
                        expectedSystemId +
                        " path=" +
                        rootPath +
                        "</color>",
                        this);
                    Directory.Delete(rootPath, true);
                }
            }

            Directory.CreateDirectory(ShellsPath);
            File.WriteAllText(systemIdPath, expectedSystemId);
        }

        private string BuildShellCachePath(PlanetDirector planet, int planetIndex)
        {
            string planetId = PlanetChunkMeshCache.BuildPlanetId(planet.Recipe);
            string fileName =
                "planet_" +
                planetIndex.ToString(CultureInfo.InvariantCulture) +
                "_seed_" +
                planet.Recipe.Seed.ToString(CultureInfo.InvariantCulture) +
                "_" +
                planetId +
                "_" +
                shellLod +
                ShellCacheExtension;
            return Path.Combine(ShellsPath, fileName);
        }
    }
}
