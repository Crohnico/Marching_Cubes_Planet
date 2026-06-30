using System;
using MarchingCubesPlanet.Coordinates;
using UnityEngine;

namespace MarchingCubesPlanet.Shape
{
    public static class PlanetGpuShapeCellBuilder
    {
        private const uint HashMask24 = 0x00ffffffu;

        public static PlanetGpuShapeBuildSummary Build(in PlanetRecipe recipe, PlanetGpuShapeCell[] output)
        {
            if (!PlanetRecipeValidator.Validate(in recipe, out string message))
            {
                throw new ArgumentException(message, nameof(recipe));
            }

            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (output.Length < recipe.VoronoiDivision)
            {
                throw new ArgumentException("Output buffer is smaller than VoronoiDivision.", nameof(output));
            }

            int cellCount = recipe.VoronoiDivision;
            bool[] continentFlags = BuildContinentFlags(in recipe);
            bool[] mountainBiomeFlags = BuildMountainBiomeFlags(in recipe, continentFlags);
            float minBaseOffset = float.PositiveInfinity;
            float maxBaseOffset = float.NegativeInfinity;
            float minRoughness = float.PositiveInfinity;
            float maxRoughness = float.NegativeInfinity;
            System.Random elevationRandom = new System.Random(MixSeed(recipe.Seed, 0x26cb5d35));

            for (int i = 0; i < cellCount; i++)
            {
                Vector3 direction = LegacyRandomUnitVector(recipe.Seed, i);
                bool isContinent = continentFlags[i];
                float baseOffset = isContinent
                    ? EvaluateLandOffset(in recipe, elevationRandom)
                    : EvaluateOceanOffset(in recipe);
                float roughness = Mathf.Lerp(
                    recipe.MinRoughness,
                    recipe.MaxRoughness,
                    LegacyHash01(recipe.Seed, i, 0x9e3779b9u));
                float heightModifier = Mathf.Lerp(
                    recipe.MinHeightModifier,
                    recipe.MaxHeightModifier,
                    LegacyHash01(recipe.Seed, i, 0x85ebca6bu));
                PlanetBiomeId biomeId = isContinent && mountainBiomeFlags[i]
                    ? PlanetBiomeId.Mountain
                    : PlanetBiomeId.Meadow;

                output[i] = PlanetGpuShapeCell.Create(direction, isContinent, baseOffset, roughness, heightModifier, biomeId);

                minBaseOffset = Mathf.Min(minBaseOffset, baseOffset);
                maxBaseOffset = Mathf.Max(maxBaseOffset, baseOffset);
                minRoughness = Mathf.Min(minRoughness, roughness);
                maxRoughness = Mathf.Max(maxRoughness, roughness);
            }

            return new PlanetGpuShapeBuildSummary
            {
                cellCount = cellCount,
                continentCellCount = recipe.ContinentCells,
                oceanCellCount = cellCount - recipe.ContinentCells,
                mountainBiomeCellCount = Mathf.Min(recipe.MountainBiomeCells, recipe.ContinentCells),
                minBaseOffset = minBaseOffset,
                maxBaseOffset = maxBaseOffset,
                minRoughness = minRoughness,
                maxRoughness = maxRoughness,
                estimatedBytes = cellCount * (long)PlanetGpuShapeCell.Stride
            };
        }

        public static Vector3 LegacyRandomUnitVector(int seed, int index)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "index must be greater than or equal to zero.");
            }

            System.Random random = new System.Random(MixSeed(seed, 0x4f1bbcdd));
            double z = 0.0;
            double angle = 0.0;
            for (int i = 0; i <= index; i++)
            {
                z = random.NextDouble() * 2.0 - 1.0;
                angle = random.NextDouble() * Math.PI * 2.0;
            }

            float zFloat = (float)z;
            float horizontalRadius = Mathf.Sqrt(Mathf.Max(0f, 1f - zFloat * zFloat));
            return new Vector3(
                Mathf.Cos((float)angle) * horizontalRadius,
                zFloat,
                Mathf.Sin((float)angle) * horizontalRadius);
        }

        private static bool[] BuildContinentFlags(in PlanetRecipe recipe)
        {
            int count = recipe.VoronoiDivision;
            int[] indices = new int[count];

            for (int i = 0; i < count; i++)
            {
                indices[i] = i;
            }

            System.Random landRandom = new System.Random(MixSeed(recipe.Seed, 0x13a5ba1d));
            for (int i = indices.Length - 1; i > 0; i--)
            {
                int swapIndex = landRandom.Next(i + 1);
                (indices[i], indices[swapIndex]) = (indices[swapIndex], indices[i]);
            }

            bool[] flags = new bool[count];
            for (int i = 0; i < recipe.ContinentCells; i++)
            {
                flags[indices[i]] = true;
            }

            return flags;
        }

        private static bool[] BuildMountainBiomeFlags(in PlanetRecipe recipe, bool[] continentFlags)
        {
            int count = recipe.VoronoiDivision;
            int mountainCount = Mathf.Min(recipe.MountainBiomeCells, recipe.ContinentCells);
            bool[] flags = new bool[count];
            if (mountainCount <= 0)
            {
                return flags;
            }

            int[] landIndices = new int[recipe.ContinentCells];
            int landCount = 0;
            for (int i = 0; i < count; i++)
            {
                if (continentFlags[i])
                {
                    landIndices[landCount] = i;
                    landCount++;
                }
            }

            System.Random mountainRandom = new System.Random(MixSeed(recipe.Seed, 0x2f6a4b1d));
            for (int i = landCount - 1; i > 0; i--)
            {
                int swapIndex = mountainRandom.Next(i + 1);
                (landIndices[i], landIndices[swapIndex]) = (landIndices[swapIndex], landIndices[i]);
            }

            for (int i = 0; i < mountainCount; i++)
            {
                flags[landIndices[i]] = true;
            }

            return flags;
        }

        private static float EvaluateLandOffset(in PlanetRecipe recipe, System.Random elevationRandom)
        {
            float t = (float)elevationRandom.NextDouble();
            float landElevation = Mathf.Lerp(recipe.MinLandElevation, recipe.MaxLandElevation, t);
            return recipe.GridRadius * landElevation;
        }

        private static float EvaluateOceanOffset(in PlanetRecipe recipe)
        {
            float oceanOffset = -recipe.GridRadius * recipe.OceanDepth;
            float minimumOceanOffset = -recipe.GridRadius * recipe.MinimumOceanDepth;
            return Mathf.Min(oceanOffset, minimumOceanOffset);
        }

        private static float LegacyHash01(int seed, int index, uint salt)
        {
            return (LegacyHash((uint)index ^ (uint)seed, salt) & HashMask24) / (float)HashMask24;
        }

        private static uint LegacyHash(uint value, uint salt)
        {
            uint hash = value + salt * 0x9e3779b9u;
            hash ^= hash >> 16;
            hash *= 0x7feb352du;
            hash ^= hash >> 15;
            hash *= 0x846ca68bu;
            hash ^= hash >> 16;
            return hash;
        }

        private static int MixSeed(int seed, int salt)
        {
            unchecked
            {
                int hash = seed;
                hash = (hash * 397) ^ salt;
                hash ^= hash << 13;
                hash ^= hash >> 17;
                hash ^= hash << 5;
                return hash;
            }
        }
    }
}
