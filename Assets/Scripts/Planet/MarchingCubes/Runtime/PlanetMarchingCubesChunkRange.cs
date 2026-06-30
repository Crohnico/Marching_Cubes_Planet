using System;
using System.Collections.Generic;
using MarchingCubesPlanet.Coordinates;
using UnityEngine;

namespace MarchingCubesPlanet.MarchingCubes
{
    [Serializable]
    public struct PlanetMarchingCubesChunkRange
    {
        public const int CanonicalChunkSize = 64;
        public const int CanonicalCellSizeGrid = 1;
        public const int DefaultSafetyMargin = 4;

        public int chunkSize;
        public int cellSizeGrid;
        public int safetyMargin;
        public int maxCandidateChunks;

        public static PlanetMarchingCubesChunkRange Default()
        {
            return new PlanetMarchingCubesChunkRange
            {
                chunkSize = CanonicalChunkSize,
                cellSizeGrid = CanonicalCellSizeGrid,
                safetyMargin = DefaultSafetyMargin,
                maxCandidateChunks = 0
            };
        }

        public bool HasCandidateChunkLimit => maxCandidateChunks > 0;

        public void EnsureDefaults()
        {
            if (chunkSize <= 0)
            {
                chunkSize = CanonicalChunkSize;
            }

            if (cellSizeGrid <= 0)
            {
                cellSizeGrid = CanonicalCellSizeGrid;
            }

            if (safetyMargin < 0)
            {
                safetyMargin = DefaultSafetyMargin;
            }
        }

        public bool Validate(out string message)
        {
            if (chunkSize != CanonicalChunkSize)
            {
                message = "chunkSize must remain 64 for canonical 07.";
                return false;
            }

            if (cellSizeGrid != CanonicalCellSizeGrid)
            {
                message = "cellSizeGrid must remain 1 for canonical 07.";
                return false;
            }

            if (safetyMargin < 0)
            {
                message = "safetyMargin must be zero or greater.";
                return false;
            }

            if (maxCandidateChunks < 0)
            {
                message = "maxCandidateChunks must be zero or greater. Zero means unlimited.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        public PlanetMarchingCubesChunkOrigin[] BuildCandidateChunks(in PlanetRecipe recipe, out PlanetMarchingCubesChunkBuildStats stats)
        {
            if (!PlanetRecipeValidator.Validate(in recipe, out string recipeMessage))
            {
                throw new ArgumentException(recipeMessage, nameof(recipe));
            }

            PlanetMarchingCubesChunkRange range = this;
            range.EnsureDefaults();
            if (!range.Validate(out string rangeMessage))
            {
                throw new ArgumentException(rangeMessage, nameof(range));
            }

            CalculateSurfaceShell(in recipe, range.safetyMargin, out float innerRadius, out float outerRadius);
            int chunkSizeValue = range.chunkSize;
            int minChunk = Mathf.FloorToInt(-outerRadius / chunkSizeValue);
            int maxChunk = Mathf.CeilToInt(outerRadius / chunkSizeValue) - 1;
            int chunksPerAxis = maxChunk - minChunk + 1;
            long scanned = (long)chunksPerAxis * chunksPerAxis * chunksPerAxis;

            List<PlanetMarchingCubesChunkOrigin> candidates = new List<PlanetMarchingCubesChunkOrigin>();
            bool reachedLimit = false;

            for (int z = minChunk; z <= maxChunk; z++)
            {
                for (int y = minChunk; y <= maxChunk; y++)
                {
                    for (int x = minChunk; x <= maxChunk; x++)
                    {
                        Vector3 min = new Vector3(x * chunkSizeValue, y * chunkSizeValue, z * chunkSizeValue);
                        Vector3 max = min + Vector3.one * chunkSizeValue;
                        if (!AabbIntersectsSphericalShell(min, max, innerRadius, outerRadius))
                        {
                            continue;
                        }

                        if (range.HasCandidateChunkLimit && candidates.Count >= range.maxCandidateChunks)
                        {
                            reachedLimit = true;
                            goto BuildDone;
                        }

                        candidates.Add(new PlanetMarchingCubesChunkOrigin(
                            x * chunkSizeValue,
                            y * chunkSizeValue,
                            z * chunkSizeValue));
                    }
                }
            }

BuildDone:
            stats = new PlanetMarchingCubesChunkBuildStats(
                candidates.Count,
                scanned,
                innerRadius,
                outerRadius,
                chunkSizeValue,
                reachedLimit);
            return candidates.ToArray();
        }

        public static void CalculateSurfaceShell(in PlanetRecipe recipe, int safetyMargin, out float innerRadius, out float outerRadius)
        {
            float safeRadius = Mathf.Max(0.0001f, recipe.GridRadius);
            float maxHeightModifier = Mathf.Max(0f, recipe.MaxHeightModifier);
            float maxNoiseOffset = safeRadius * Mathf.Max(0f, recipe.SurfaceNoiseAmplitude);
            float maxMountainBiomeOffset = safeRadius * Mathf.Max(0f, recipe.MountainBiomeHeight);
            float outwardIsoOffset = Mathf.Max(0f, -recipe.IsoLevel);
            float inwardIsoOffset = Mathf.Max(0f, recipe.IsoLevel);
            float maxOutwardOffset =
                safeRadius * Mathf.Max(0f, recipe.MaxLandElevation) * maxHeightModifier +
                maxMountainBiomeOffset +
                maxNoiseOffset +
                outwardIsoOffset;
            float maxInwardOffset =
                safeRadius * Mathf.Max(Mathf.Max(0f, recipe.OceanDepth), Mathf.Max(0f, recipe.MinimumOceanDepth)) * maxHeightModifier +
                maxNoiseOffset +
                inwardIsoOffset;

            innerRadius = Mathf.Max(0f, safeRadius - maxInwardOffset - Mathf.Max(0, safetyMargin));
            outerRadius = safeRadius + maxOutwardOffset + Mathf.Max(0, safetyMargin);
        }

        public static bool AabbIntersectsSphericalShell(Vector3 min, Vector3 max, float innerRadius, float outerRadius)
        {
            float minDistanceSquared = DistanceSquaredToAabb(Vector3.zero, min, max);
            float maxDistanceSquared = MaxDistanceSquaredToAabbFromOrigin(min, max);
            return minDistanceSquared <= outerRadius * outerRadius &&
                   maxDistanceSquared >= innerRadius * innerRadius;
        }

        private static float DistanceSquaredToAabb(Vector3 point, Vector3 min, Vector3 max)
        {
            float dx = AxisDistance(point.x, min.x, max.x);
            float dy = AxisDistance(point.y, min.y, max.y);
            float dz = AxisDistance(point.z, min.z, max.z);
            return dx * dx + dy * dy + dz * dz;
        }

        private static float AxisDistance(float value, float min, float max)
        {
            if (value < min)
            {
                return min - value;
            }

            if (value > max)
            {
                return value - max;
            }

            return 0f;
        }

        private static float MaxDistanceSquaredToAabbFromOrigin(Vector3 min, Vector3 max)
        {
            float x = Mathf.Max(Mathf.Abs(min.x), Mathf.Abs(max.x));
            float y = Mathf.Max(Mathf.Abs(min.y), Mathf.Abs(max.y));
            float z = Mathf.Max(Mathf.Abs(min.z), Mathf.Abs(max.z));
            return x * x + y * y + z * z;
        }

        public override string ToString()
        {
            return "chunkSize=" + chunkSize +
                   "\ncellSizeGrid=" + cellSizeGrid +
                   "\nsafetyMargin=" + safetyMargin +
                   "\nmaxCandidateChunks=" + maxCandidateChunks;
        }
    }
}
