using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    [DisallowMultipleComponent]
    public sealed class StellarSystem : MonoBehaviour
    {
        public string ID;

        [SerializeField] private Transform player;
        [SerializeField, Min(1)] private int startupFarChunkBuildsPerFrame = 8;
        [SerializeField, Min(1)] private int segmentLodChunksBuiltPerFrame = 16;
        [SerializeField, HideInInspector] private List<PlanetManager> planets = new List<PlanetManager>();

        public string SystemDataUrl => FileManager.CombineUrl("StellarSystems", ResolveSystemId());

        private async void Start()
        {
            RefreshPlanets();
            SortPlanetsByPlayerDistance();
            AssignPlanetIdsFromWorldPositions();

            try
            {
                await InitializePlanetsSequentiallyAsync();
            }
            catch (Exception exception)
            {
                Debug.LogError($"StellarSystem initialization failed for {ResolveSystemId()}. {exception}", this);
            }
        }

        [ContextMenu("Clear System Data")]
        public void ClearSystemData()
        {
            bool deleted = FileManager.DeleteDirectory(SystemDataUrl);
            Debug.Log(deleted
                ? $"Cleared system data: {SystemDataUrl}"
                : $"No system data found: {SystemDataUrl}", this);
        }

        private void RefreshPlanets()
        {
            planets.Clear();
            planets.AddRange(FindObjectsByType<PlanetManager>(FindObjectsSortMode.None));
        }

        private void SortPlanetsByPlayerDistance()
        {
            Transform resolvedPlayer = ResolvePlayer();
            if (resolvedPlayer == null)
            {
                return;
            }

            Vector3 playerPosition = resolvedPlayer.position;
            planets.Sort((a, b) =>
            {
                float distanceA = a != null ? (a.transform.position - playerPosition).sqrMagnitude : float.PositiveInfinity;
                float distanceB = b != null ? (b.transform.position - playerPosition).sqrMagnitude : float.PositiveInfinity;
                return distanceA.CompareTo(distanceB);
            });
        }

        private void AssignPlanetIdsFromWorldPositions()
        {
            for (int i = 0; i < planets.Count; i++)
            {
                PlanetManager planet = planets[i];
                if (planet == null)
                {
                    continue;
                }

                planet.PlanetID = BuildPlanetIdFromWorldPosition(planet.transform.position);
            }
        }

        private async Task InitializePlanetsSequentiallyAsync()
        {
            string resolvedSystemId = ResolveSystemId();
            for (int i = 0; i < planets.Count; i++)
            {
                PlanetManager planet = planets[i];
                if (planet == null)
                {
                    continue;
                }

                await InitializePlanetAsync(planet, resolvedSystemId);
            }
        }

        private Task InitializePlanetAsync(PlanetManager planet, string resolvedSystemId)
        {
            TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();
            StartCoroutine(InitializePlanetRoutine(planet, resolvedSystemId, completion));
            return completion.Task;
        }

        private IEnumerator InitializePlanetRoutine(
            PlanetManager planet,
            string resolvedSystemId,
            TaskCompletionSource<bool> completion)
        {
            IEnumerator initialization = planet.Initialize(
                resolvedSystemId,
                startupFarChunkBuildsPerFrame,
                segmentLodChunksBuiltPerFrame);

            while (true)
            {
                bool movedNext;
                object current;
                try
                {
                    movedNext = initialization.MoveNext();
                    current = movedNext ? initialization.Current : null;
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                    yield break;
                }

                if (!movedNext)
                {
                    break;
                }

                yield return current;
            }

            completion.TrySetResult(true);
        }

        private Transform ResolvePlayer()
        {
            if (player != null)
            {
                return player;
            }

            PlayerChunkTracker tracker = FindFirstObjectByType<PlayerChunkTracker>();
            if (tracker != null)
            {
                player = tracker.TrackedTarget;
                return player;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                player = mainCamera.transform;
                return player;
            }

            return null;
        }

        private string ResolveSystemId()
        {
            return string.IsNullOrWhiteSpace(ID) ? gameObject.name : ID.Trim();
        }

        private static string BuildPlanetIdFromWorldPosition(Vector3 position)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Planet_X{0}_Y{1}_Z{2}",
                FormatCoordinate(position.x),
                FormatCoordinate(position.y),
                FormatCoordinate(position.z));
        }

        private static string FormatCoordinate(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
