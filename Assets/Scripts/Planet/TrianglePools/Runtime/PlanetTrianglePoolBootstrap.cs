using UnityEngine;

namespace MarchingCubesPlanet.TrianglePools
{
    [DisallowMultipleComponent]
    public sealed class PlanetTrianglePoolBootstrap : MonoBehaviour
    {
        [SerializeField] private PlanetTriangleBudgetProfile[] activeProfiles;
        [SerializeField] private PlanetPlayerViewReference playerViewReference;
        [SerializeField] private PlanetTriangleDistanceReference distanceReference;

        private void Awake()
        {
            Init();
        }

        public void Init()
        {
            if (playerViewReference == null)
            {
                playerViewReference = FindFirstObjectByType<PlanetPlayerViewReference>();
            }

            if (playerViewReference != null)
            {
                PlanetTrianglePoolRegistry.RegisterPlayerViewReference(playerViewReference);
                playerViewReference.ForcePush();
            }

            if (distanceReference == null)
            {
                distanceReference = FindFirstObjectByType<PlanetTriangleDistanceReference>();
            }

            if (distanceReference != null)
            {
                PlanetTrianglePoolRegistry.RegisterDistanceReference(distanceReference);
            }

            if (activeProfiles == null || activeProfiles.Length == 0)
            {
                PlanetTrianglePoolRegistry.RegisterBudget(PlanetTriangleBudget.EnvironmentDefault());
                PlanetTrianglePoolRegistry.RegisterBudget(PlanetTriangleBudget.ParticlesDefault());
                return;
            }

            for (int i = 0; i < activeProfiles.Length; i++)
            {
                PlanetTriangleBudgetProfile profile = activeProfiles[i];
                if (profile != null)
                {
                    PlanetTrianglePoolRegistry.RegisterBudget(profile.ToBudget());
                }
            }
        }
    }
}
