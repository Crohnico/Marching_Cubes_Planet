using System.Collections.Generic;
using MarchingCubesPlanet.Coordinates;
using UnityEngine;

namespace MarchingCubesPlanet.MarchingCubes
{
    public sealed class LODGestor : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private PlanetChunkLodActivationProfile activationProfile;
        [SerializeField] private float checkIntervalSeconds = 0.2f;

        private List<ChunkLODGestor> chunks;
        private PlanetDirector director;
        private float checkTimer;

        public int ChunkCount => chunks != null ? chunks.Count : 0;

        public void SetChunks(List<ChunkLODGestor> chunks, PlanetDirector director, Transform player)
        {
            this.chunks = chunks;
            this.director = director;
            this.player = player;
            checkTimer = 0f;
            Tick();
        }

        public void ClearChunks()
        {
            chunks = null;
            director = null;
            checkTimer = 0f;
        }

        private void Update()
        {
            if (chunks == null || chunks.Count <= 0 || player == null)
            {
                return;
            }

            checkTimer += Time.deltaTime;
            if (checkTimer < Mathf.Max(0.01f, checkIntervalSeconds))
            {
                return;
            }

            checkTimer = 0f;
            Tick();
        }

        private void Tick()
        {
            if (chunks == null || chunks.Count <= 0 || player == null)
            {
                return;
            }

            PlanetChunkLodActivationConfig config = ResolveConfig();
            Vector3 playerPosition = player.position;
            PlanetRecipe recipe = director != null ? director.Recipe : PlanetRecipe.Default();
            float baseChunkWorldSize = PlanetChunkLodUtility.CalculateBaseChunkWorldSize(in recipe, in config);

            float lod0Distance = config.lod0MaxDistanceWorld;
            float lod1Distance = config.lod1MaxDistanceWorld;
            for (int i = 0; i < chunks.Count; i++)
            {
                ChunkLODGestor chunk = chunks[i];
                if (chunk == null)
                {
                    continue;
                }

                int desiredLod = ResolveDesiredLod(
                    (chunk.transform.position - playerPosition).sqrMagnitude,
                    chunk.CurrentLOD,
                    config.enableLod0,
                    lod0Distance,
                    lod1Distance,
                    baseChunkWorldSize);
                chunk.SetDesireLOD(desiredLod);
            }
        }

        private PlanetChunkLodActivationConfig ResolveConfig()
        {
            PlanetChunkLodActivationProfile profile = activationProfile != null
                ? activationProfile
                : Resources.Load<PlanetChunkLodActivationProfile>(PlanetChunkLodActivationProfile.DefaultResourcesPath);
            return profile != null ? profile.ToConfig() : PlanetChunkLodActivationConfig.Default();
        }

        private static int ResolveDesiredLod(
            float distanceSqr,
            int currentLod,
            bool enableLod0,
            float lod0Distance,
            float lod1Distance,
            float hysteresisDistance)
        {
            float lod0Limit = currentLod == (int)PlanetChunkLod.LOD0
                ? lod0Distance + hysteresisDistance
                : lod0Distance;
            if (enableLod0 && distanceSqr < lod0Limit * lod0Limit)
            {
                return (int)PlanetChunkLod.LOD0;
            }

            float lod1Limit = currentLod == (int)PlanetChunkLod.LOD1
                ? lod1Distance + hysteresisDistance
                : lod1Distance;
            return distanceSqr < lod1Limit * lod1Limit
                ? (int)PlanetChunkLod.LOD1
                : (int)PlanetChunkLod.LOD2;
        }
    }
}
