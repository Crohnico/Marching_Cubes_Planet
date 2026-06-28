using System;
using MarchingCubesPlanet.Coordinates;
using UnityEngine;

namespace MarchingCubesPlanet.Shape
{
    public static class PlanetGpuShapeCellBuilder
    {
        private const float GoldenAngle = 2.39996322972865332f;
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
            float minBaseOffset = float.PositiveInfinity;
            float maxBaseOffset = float.NegativeInfinity;
            float minRoughness = float.PositiveInfinity;
            float maxRoughness = float.NegativeInfinity;

            for (int i = 0; i < cellCount; i++)
            {
                Vector3 direction = FibonacciDirection(i, cellCount, recipe.Seed);
                bool isContinent = continentFlags[i];
                float baseOffset = isContinent
                    ? EvaluateLandOffset(in recipe, i)
                    : EvaluateOceanOffset(in recipe);
                float roughness = Mathf.Lerp(
                    recipe.MinRoughness,
                    recipe.MaxRoughness,
                    Hash01((uint)recipe.Seed, (uint)i, 0x51ed270bu));
                uint hash = Hash((uint)recipe.Seed, (uint)i, 0xb5297a4du) & HashMask24;

                output[i] = PlanetGpuShapeCell.Create(direction, isContinent, baseOffset, roughness, hash);

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
                minBaseOffset = minBaseOffset,
                maxBaseOffset = maxBaseOffset,
                minRoughness = minRoughness,
                maxRoughness = maxRoughness,
                estimatedBytes = cellCount * (long)PlanetGpuShapeCell.Stride
            };
        }

        public static Vector3 FibonacciDirection(int index, int count, int seed)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "count must be greater than zero.");
            }

            float y = 1f - (index + 0.5f) * (2f / count);
            float radius = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float seedRotation = (Hash((uint)seed, 0u, 0x68bc21ebu) & 1023u) / 1023f * Mathf.PI * 2f;
            float theta = index * GoldenAngle + seedRotation;
            return new Vector3(Mathf.Cos(theta) * radius, y, Mathf.Sin(theta) * radius);
        }

        private static bool[] BuildContinentFlags(in PlanetRecipe recipe)
        {
            int count = recipe.VoronoiDivision;
            uint[] keys = new uint[count];
            int[] indices = new int[count];

            for (int i = 0; i < count; i++)
            {
                keys[i] = Hash((uint)recipe.Seed, (uint)i, 0x9e3779b9u);
                indices[i] = i;
            }

            Array.Sort(keys, indices);

            bool[] flags = new bool[count];
            for (int i = 0; i < recipe.ContinentCells; i++)
            {
                flags[indices[i]] = true;
            }

            return flags;
        }

        private static float EvaluateLandOffset(in PlanetRecipe recipe, int cellIndex)
        {
            float t = Smooth01(Hash01((uint)recipe.Seed, (uint)cellIndex, 0x27d4eb2du));
            float landElevation = Mathf.Lerp(recipe.MinLandElevation, recipe.MaxLandElevation, t);
            float heightModifier = Mathf.Lerp(
                recipe.MinHeightModifier,
                recipe.MaxHeightModifier,
                Hash01((uint)recipe.Seed, (uint)cellIndex, 0x165667b1u));
            return recipe.GridRadius * landElevation * heightModifier;
        }

        private static float EvaluateOceanOffset(in PlanetRecipe recipe)
        {
            float oceanOffset = -recipe.GridRadius * recipe.OceanDepth;
            float minimumOceanOffset = -recipe.GridRadius * recipe.MinimumOceanDepth;
            return Mathf.Min(oceanOffset, minimumOceanOffset);
        }

        private static float Smooth01(float value)
        {
            return value * value * (3f - 2f * value);
        }

        private static float Hash01(uint seed, uint index, uint salt)
        {
            return (Hash(seed, index, salt) & HashMask24) / (float)HashMask24;
        }

        private static uint Hash(uint seed, uint index, uint salt)
        {
            uint hash = seed ^ salt;
            hash ^= index + 0x9e3779b9u + (hash << 6) + (hash >> 2);
            hash ^= hash >> 16;
            hash *= 0x7feb352du;
            hash ^= hash >> 15;
            hash *= 0x846ca68bu;
            hash ^= hash >> 16;
            return hash;
        }
    }
}
