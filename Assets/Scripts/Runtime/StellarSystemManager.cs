using System;
using System.Threading.Tasks;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    [DisallowMultipleComponent]
    public sealed class StellarSystemManager : MonoBehaviour
    {
        [SerializeField] private string systemId = "DefaultSystem";
        [SerializeField] private Transform player;
        [SerializeField, HideInInspector] private PlanetManager[] planetManagers;
        private int generationSequenceVersion;

        public string SystemId => systemId;
        public PlanetManager[] PlanetManagers => planetManagers;

        private void Awake()
        {
            RefreshPlanetManagers();
            SortPlanetsByDistanceToPlayer();
        }

        private async void Start()
        {
            generationSequenceVersion++;
            try
            {
                await GeneratePlanetFarsSequentiallyAsync(generationSequenceVersion);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void OnDisable()
        {
            generationSequenceVersion++;
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(systemId))
            {
                systemId = "DefaultSystem";
            }
        }

        public void RefreshPlanetManagers()
        {
            planetManagers = FindObjectsByType<PlanetManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        public void SortPlanetsByDistanceToPlayer()
        {
            if (planetManagers == null || planetManagers.Length <= 1)
            {
                return;
            }

            Vector3 referencePosition = player != null ? player.position : transform.position;
            Array.Sort(
                planetManagers,
                (a, b) => GetSquaredDistance(a, referencePosition).CompareTo(GetSquaredDistance(b, referencePosition)));
        }

        public Task GeneratePlanetFarsSequentiallyAsync()
        {
            generationSequenceVersion++;
            return GeneratePlanetFarsSequentiallyAsync(generationSequenceVersion);
        }

        private async Task GeneratePlanetFarsSequentiallyAsync(int version)
        {
            RefreshPlanetManagers();
            SortPlanetsByDistanceToPlayer();

            if (planetManagers == null)
            {
                return;
            }

            for (int i = 0; i < planetManagers.Length; i++)
            {
                if (version != generationSequenceVersion || !isActiveAndEnabled)
                {
                    return;
                }

                PlanetManager planet = planetManagers[i];
                if (planet == null || !planet.isActiveAndEnabled)
                {
                    continue;
                }

                await planet.GenerateFarAsync();
            }
        }

        public bool DeleteSystemData()
        {
            return FileManager.DeleteDirectory($"StellarSystems/{SanitizePathPart(systemId)}");
        }

        private static float GetSquaredDistance(PlanetManager planet, Vector3 referencePosition)
        {
            return planet != null
                ? (planet.transform.position - referencePosition).sqrMagnitude
                : float.PositiveInfinity;
        }

        private static string SanitizePathPart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "DefaultSystem";
            }

            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }
    }
}
