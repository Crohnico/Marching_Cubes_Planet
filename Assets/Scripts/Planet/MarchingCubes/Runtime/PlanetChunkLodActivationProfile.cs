using UnityEngine;

namespace MarchingCubesPlanet.MarchingCubes
{
    [CreateAssetMenu(
        fileName = "PlanetChunkLodActivationProfile",
        menuName = "Marching Cubes Planet/Chunk LOD Activation Profile")]
    public sealed class PlanetChunkLodActivationProfile : ScriptableObject
    {
        public const string DefaultResourcesPath = "Planet/DefaultChunkLodActivationProfile";

        [SerializeField] private int canonicalChunkSize = PlanetChunkLodActivationConfig.DefaultCanonicalChunkSize;
        [SerializeField] private bool enableLod0;
        [SerializeField] private float lod0MaxDistanceWorld = PlanetChunkLodActivationConfig.DefaultLod0MaxDistanceWorld;
        [SerializeField] private float lod1MaxDistanceWorld = PlanetChunkLodActivationConfig.DefaultLod1MaxDistanceWorld;

        public PlanetChunkLodActivationConfig ToConfig()
        {
            PlanetChunkLodActivationConfig config = new PlanetChunkLodActivationConfig
            {
                canonicalChunkSize = canonicalChunkSize,
                enableLod0 = enableLod0,
                lod0MaxDistanceWorld = lod0MaxDistanceWorld,
                lod1MaxDistanceWorld = lod1MaxDistanceWorld
            };
            config.EnsureValid();
            return config;
        }
    }
}
