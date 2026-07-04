using MarchingCubesPlanet.MarchingCubes;
using UnityEngine;

namespace MarchingCubesPlanet.Preview
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Marching Cubes Planet/Chunk LOD Activation Gizmo")]
    public sealed class PlanetChunkLodActivationGizmo : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private Transform centerSource;
        [SerializeField] private PlanetChunkLodActivationProfile activationProfile;

        [Header("Drawing")]
        [SerializeField] private bool drawWhenNotSelected = true;
        [SerializeField] private Color lod0Color = new Color(1f, 0.25f, 0.15f, 0.85f);
        [SerializeField] private Color lod1Color = new Color(1f, 0.85f, 0.1f, 0.85f);
        [SerializeField] private Color lod2Color = new Color(0.25f, 0.55f, 1f, 0.45f);

        private void OnDrawGizmos()
        {
            if (drawWhenNotSelected)
            {
                DrawGizmos();
            }
        }

        private void OnDrawGizmosSelected()
        {
            DrawGizmos();
        }

        private void DrawGizmos()
        {
            PlanetChunkLodActivationConfig config = ResolveConfig();
            Vector3 center = centerSource != null ? centerSource.position : transform.position;

            if (config.enableLod0)
            {
                Gizmos.color = lod0Color;
                Gizmos.DrawWireSphere(center, config.lod0MaxDistanceWorld);
            }

            Gizmos.color = lod1Color;
            Gizmos.DrawWireSphere(center, config.lod1MaxDistanceWorld);

            Gizmos.color = lod2Color;
            Gizmos.DrawRay(center, Vector3.forward * config.lod1MaxDistanceWorld);
            Gizmos.DrawRay(center, Vector3.right * config.lod1MaxDistanceWorld);
        }

        private PlanetChunkLodActivationConfig ResolveConfig()
        {
            PlanetChunkLodActivationProfile profile = activationProfile != null
                ? activationProfile
                : Resources.Load<PlanetChunkLodActivationProfile>(PlanetChunkLodActivationProfile.DefaultResourcesPath);
            return profile != null ? profile.ToConfig() : PlanetChunkLodActivationConfig.Default();
        }
    }
}
