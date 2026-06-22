using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] private List<PlanetManager> planets = new List<PlanetManager>();

        public string SystemDataUrl => FileManager.CombineUrl("StellarSystems", ResolveSystemId());

        private IEnumerator Start()
        {
            RefreshPlanets();
            SortPlanetsByPlayerDistance();

            for (int i = 0; i < planets.Count; i++)
            {
                PlanetManager planet = planets[i];
                if (planet == null)
                {
                    continue;
                }

                yield return planet.Initialize(ID, startupFarChunkBuildsPerFrame, segmentLodChunksBuiltPerFrame);
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
    }
}
