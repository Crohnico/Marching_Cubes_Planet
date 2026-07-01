using System.Collections.Generic;
using UnityEngine;

namespace MarchingCubesPlanet.TrianglePools
{
    public static class PlanetTrianglePoolRegistry
    {
        private static readonly Dictionary<uint, PlanetTrianglePoolController> Controllers =
            new Dictionary<uint, PlanetTrianglePoolController>();

        private static PlanetPlayerViewReference playerViewReference;
        private static PlanetTriangleDistanceReference distanceReference;
        private static Vector3 playerPositionWorld;
        private static Vector3 playerForwardWorld = Vector3.forward;
        private static int playerViewVersion;
        private static bool hasPlayerViewSnapshot;
        private static Vector3 fallbackPriorityOriginWorld;
        private static bool hasFallbackPriorityOriginWorld;

        public static bool HasPlayerViewReference => playerViewReference != null;
        public static bool HasPlayerViewSnapshot => hasPlayerViewSnapshot;
        public static bool HasPlayerViewData => hasPlayerViewSnapshot ||
                                                distanceReference != null;
        public static bool HasPriorityOriginData => HasPlayerViewData ||
                                                    hasFallbackPriorityOriginWorld;
        public static bool HasDistanceReference => distanceReference != null;

        public static Vector3 PlayerPositionWorld => hasPlayerViewSnapshot
            ? playerPositionWorld
            : distanceReference != null
                ? distanceReference.Position
                : hasFallbackPriorityOriginWorld
                    ? fallbackPriorityOriginWorld
                    : Vector3.zero;

        public static Vector3 PlayerForwardWorld => hasPlayerViewSnapshot
            ? playerForwardWorld
            : distanceReference != null
                ? distanceReference.Forward
                : Vector3.forward;

        public static int PlayerViewVersion => playerViewVersion;

        public static Vector3 PriorityOriginWorld => hasPlayerViewSnapshot
            ? playerPositionWorld
            : distanceReference != null
                ? distanceReference.Position
                : hasFallbackPriorityOriginWorld
                    ? fallbackPriorityOriginWorld
                    : Vector3.zero;

        public static PlanetTrianglePoolController Environment =>
            GetOrCreate(PlanetTriangleArtistId.EnvironmentValue);

        public static PlanetTrianglePoolController Particles =>
            GetOrCreate(PlanetTriangleArtistId.ParticlesValue);

        public static void RegisterPlayerViewReference(PlanetPlayerViewReference reference)
        {
            if (reference != null)
            {
                playerViewReference = reference;
            }
        }

        public static void UnregisterPlayerViewReference(PlanetPlayerViewReference reference)
        {
            if (playerViewReference == reference)
            {
                playerViewReference = null;
                hasPlayerViewSnapshot = false;
            }
        }

        public static void PushPlayerView(
            PlanetPlayerViewReference reference,
            Vector3 positionWorld,
            Vector3 forwardWorld,
            int version)
        {
            if (reference == null || playerViewReference != null && playerViewReference != reference)
            {
                return;
            }

            playerViewReference = reference;
            PushPlayerView(positionWorld, forwardWorld, version);
        }

        public static void PushPlayerView(Vector3 positionWorld, Vector3 forwardWorld, int version)
        {
            if (forwardWorld.sqrMagnitude <= 0.0001f)
            {
                forwardWorld = Vector3.forward;
            }

            playerPositionWorld = positionWorld;
            playerForwardWorld = forwardWorld.normalized;
            playerViewVersion = version;
            hasPlayerViewSnapshot = true;
        }

        public static void PushDistanceReferenceView(
            PlanetTriangleDistanceReference reference,
            Vector3 positionWorld,
            Vector3 forwardWorld,
            int version)
        {
            if (reference == null || distanceReference != reference || playerViewReference != null)
            {
                return;
            }

            PushPlayerView(positionWorld, forwardWorld, version);
        }

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
                if (playerViewReference == null)
                {
                    hasPlayerViewSnapshot = false;
                }
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
