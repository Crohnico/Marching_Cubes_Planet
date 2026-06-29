using System.Collections.Generic;
using UnityEngine;

namespace MarchingCubesPlanet.TrianglePools
{
    public static class PlanetTrianglePoolRegistry
    {
        private static readonly Dictionary<uint, PlanetTrianglePoolController> Controllers =
            new Dictionary<uint, PlanetTrianglePoolController>();

        private static PlanetTriangleDistanceReference distanceReference;
        private static Vector3 fallbackPriorityOriginWorld;
        private static bool hasFallbackPriorityOriginWorld;

        public static bool HasDistanceReference => distanceReference != null;

        public static Vector3 PriorityOriginWorld => distanceReference != null
            ? distanceReference.Position
            : hasFallbackPriorityOriginWorld
                ? fallbackPriorityOriginWorld
                : Vector3.zero;

        public static PlanetTrianglePoolController Environment =>
            GetOrCreate(PlanetTriangleArtistId.EnvironmentValue);

        public static PlanetTrianglePoolController Particles =>
            GetOrCreate(PlanetTriangleArtistId.ParticlesValue);

        public static void RegisterDistanceReference(PlanetTriangleDistanceReference reference)
        {
            if (reference != null)
            {
                distanceReference = reference;
            }
        }

        public static void UnregisterDistanceReference(PlanetTriangleDistanceReference reference)
        {
            if (distanceReference == reference)
            {
                distanceReference = null;
            }
        }

        public static void SetFallbackPriorityOriginWorld(Vector3 value)
        {
            fallbackPriorityOriginWorld = value;
            hasFallbackPriorityOriginWorld = true;
        }

        public static PlanetTrianglePoolController GetOrCreate(uint artistId)
        {
            if (Controllers.TryGetValue(artistId, out PlanetTrianglePoolController controller))
            {
                return controller;
            }

            PlanetTriangleBudget budget = artistId == PlanetTriangleArtistId.ParticlesValue
                ? PlanetTriangleBudget.ParticlesDefault()
                : PlanetTriangleBudget.EnvironmentDefault();
            controller = new PlanetTrianglePoolController(budget);
            Controllers.Add(artistId, controller);
            return controller;
        }

        public static void RegisterBudget(PlanetTriangleBudget budget)
        {
            PlanetTrianglePoolController controller = GetOrCreate(budget.ArtistId);
            controller.Initialize(budget);
        }

        public static void SetEnvironmentTriangleBudget(int totalTriangleBudget)
        {
            Environment.SetTotalTriangleBudget(totalTriangleBudget);
        }

        public static void ReleaseAllSlots()
        {
            foreach (PlanetTrianglePoolController controller in Controllers.Values)
            {
                controller.ReleaseAllSlots();
            }
        }
    }
}
