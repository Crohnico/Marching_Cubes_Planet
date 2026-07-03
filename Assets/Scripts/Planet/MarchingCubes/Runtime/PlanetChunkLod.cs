using System;
using MarchingCubesPlanet.Coordinates;
using UnityEngine;

namespace MarchingCubesPlanet.MarchingCubes
{
    public enum PlanetChunkLod
    {
        LOD0 = 0,
        LOD1 = 1,
        LOD2 = 2
    }

    public static class PlanetChunkLodUtility
    {
        public const PlanetChunkLod BaseRecipeLod = PlanetChunkLod.LOD1;
        public const PlanetChunkLod InitialFallbackLod = PlanetChunkLod.LOD2;

        public static PlanetRecipe BuildRecipeForLod(in PlanetRecipe lod1Recipe, PlanetChunkLod lod)
        {
            PlanetRecipe result = lod1Recipe;
            int lod1GridRadius = Mathf.Max(1, lod1Recipe.GridRadius);
            float lod1WorldRadius = lod1Recipe.WorldRadius;
            int targetGridRadius;

            switch (lod)
            {
                case PlanetChunkLod.LOD0:
                    targetGridRadius = lod1GridRadius * 2;
                    break;
                case PlanetChunkLod.LOD1:
                    targetGridRadius = lod1GridRadius;
                    break;
                case PlanetChunkLod.LOD2:
                    targetGridRadius = Mathf.Max(1, Mathf.RoundToInt(lod1GridRadius * 0.5f));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(lod), lod, "Unknown planet chunk LOD.");
            }

            result.GridRadius = targetGridRadius;
            result.WorldScale = lod1WorldRadius / targetGridRadius;
            return result;
        }

        public static float CalculateBaseChunkWorldSize(in PlanetRecipe lod1Recipe)
        {
            return Mathf.Max(0.0001f, PlanetMarchingCubesChunkRange.CanonicalChunkSize * lod1Recipe.WorldScale);
        }
    }

    public readonly struct PlanetChunkLodScoringContext
    {
        public PlanetChunkLodScoringContext(
            Vector3 playerPositionWorld,
            Vector3 cameraForwardWorld,
            float baseChunkWorldSize)
        {
            PlayerPositionWorld = playerPositionWorld;
            CameraForwardWorld = cameraForwardWorld.sqrMagnitude > 0.0001f
                ? cameraForwardWorld.normalized
                : Vector3.forward;
            BaseChunkWorldSize = Mathf.Max(0.0001f, baseChunkWorldSize);
            ProximityMaxChunks = PlanetChunkLodScorer.Lod1ExitDistanceChunks;
            ViewMaxRayDistanceChunks = PlanetChunkLodScorer.Lod1EnterDistanceChunks;
        }

        public Vector3 PlayerPositionWorld { get; }
        public Vector3 CameraForwardWorld { get; }
        public float BaseChunkWorldSize { get; }
        public float ProximityMaxChunks { get; }
        public float ViewMaxRayDistanceChunks { get; }
    }

    public readonly struct PlanetChunkLodScore
    {
        public PlanetChunkLodScore(
            int chunkId,
            PlanetChunkLod desiredLod,
            float score,
            float proximityScore,
            float viewScore,
            float distanceChunks,
            float viewRayDistanceChunks,
            Vector3 centerWorld)
        {
            ChunkId = chunkId;
            DesiredLod = desiredLod;
            Score = score;
            ProximityScore = proximityScore;
            ViewScore = viewScore;
            DistanceChunks = distanceChunks;
            ViewRayDistanceChunks = viewRayDistanceChunks;
            CenterWorld = centerWorld;
        }

        public int ChunkId { get; }
        public PlanetChunkLod DesiredLod { get; }
        public float Score { get; }
        public float ProximityScore { get; }
        public float ViewScore { get; }
        public float DistanceChunks { get; }
        public float ViewRayDistanceChunks { get; }
        public Vector3 CenterWorld { get; }
    }

    public struct PlanetChunkLodSummary
    {
        public int chunkCount;
        public int lod0Count;
        public int lod1Count;
        public int lod2Count;
        public float bestScore;
        public float averageScore;

        public void Record(PlanetChunkLodScore score)
        {
            if (chunkCount == 0 || score.Score > bestScore)
            {
                bestScore = score.Score;
            }

            averageScore += score.Score;
            chunkCount++;
            switch (score.DesiredLod)
            {
                case PlanetChunkLod.LOD0:
                    lod0Count++;
                    break;
                case PlanetChunkLod.LOD1:
                    lod1Count++;
                    break;
                default:
                    lod2Count++;
                    break;
            }
        }

        public void Finish()
        {
            if (chunkCount > 0)
            {
                averageScore /= chunkCount;
            }
        }
    }

    public static class PlanetChunkLodScorer
    {
        public const float ProximityWeight = 0.70f;
        public const float ViewWeight = 0.30f;
        public const float Lod0EnterDistanceChunks = 3f;
        public const float Lod0ExitDistanceChunks = 4f;
        public const float Lod1EnterDistanceChunks = 6f;
        public const float Lod1ExitDistanceChunks = 7f;

        public static PlanetChunkLodScore Evaluate(
            int chunkId,
            Vector3 chunkCenterWorld,
            in PlanetChunkLodScoringContext context)
        {
            Vector3 playerToChunk = chunkCenterWorld - context.PlayerPositionWorld;
            float distanceWorld = playerToChunk.magnitude;
            float distanceChunks = distanceWorld / context.BaseChunkWorldSize;
            float proximityScore = Mathf.Clamp01(1f - distanceChunks / context.ProximityMaxChunks);
            float forwardDistanceWorld = Vector3.Dot(playerToChunk, context.CameraForwardWorld);
            float viewRayDistanceChunks = 0f;
            float viewScore = 0f;
            if (forwardDistanceWorld > 0f)
            {
                Vector3 closestPointOnRay = context.PlayerPositionWorld + context.CameraForwardWorld * forwardDistanceWorld;
                viewRayDistanceChunks = (chunkCenterWorld - closestPointOnRay).magnitude / context.BaseChunkWorldSize;
                viewScore = Mathf.Clamp01(1f - viewRayDistanceChunks / context.ViewMaxRayDistanceChunks);
            }

            float score = proximityScore * ProximityWeight + viewScore * ViewWeight;
            PlanetChunkLod desiredLod = ResolveDesiredLod(distanceChunks);
            return new PlanetChunkLodScore(
                chunkId,
                desiredLod,
                score,
                proximityScore,
                viewScore,
                distanceChunks,
                viewRayDistanceChunks,
                chunkCenterWorld);
        }

        public static PlanetChunkLod ResolveDesiredLod(float distanceChunks)
        {
            if (distanceChunks <= Lod0EnterDistanceChunks)
            {
                return PlanetChunkLod.LOD0;
            }

            if (distanceChunks <= Lod1EnterDistanceChunks)
            {
                return PlanetChunkLod.LOD1;
            }

            return PlanetChunkLod.LOD2;
        }
    }
}
