using System;
using MarchingCubesPlanet.Coordinates;
using UnityEngine;

namespace MarchingCubesPlanet.MarchingCubes
{
    [Serializable]
    public struct PlanetMarchingCubesSurfaceRange
    {
        private const int DefaultRadialSafetyMargin = 64;

        public int radialStartOffset;
        public int radialCubeCount;
        public int faceResolution;
        public float cubeSizeGrid;

        public static PlanetMarchingCubesSurfaceRange Default()
        {
            return new PlanetMarchingCubesSurfaceRange
            {
                radialStartOffset = -512,
                radialCubeCount = 1024,
                faceResolution = 16,
                cubeSizeGrid = 1f
            };
        }

        public int RadialEndOffset => radialStartOffset + radialCubeCount;
        public long CubeCount => 6L * faceResolution * faceResolution * radialCubeCount;

        public static PlanetMarchingCubesSurfaceRange RequiredForRecipe(
            in PlanetRecipe recipe,
            int faceResolution,
            int radialSafetyMargin = DefaultRadialSafetyMargin)
        {
            if (!PlanetRecipeValidator.Validate(in recipe, out string message))
            {
                throw new ArgumentException(message, nameof(recipe));
            }

            int safeFaceResolution = faceResolution > 0 ? faceResolution : Default().faceResolution;
            int safeMargin = Math.Max(1, radialSafetyMargin);
            float maxLandOffset = recipe.GridRadius *
                Mathf.Max(0f, recipe.MaxLandElevation) *
                Mathf.Max(0f, recipe.MaxHeightModifier);
            float maxNoiseOffset = recipe.GridRadius * Mathf.Max(0f, recipe.SurfaceNoiseAmplitude);
            float maxOceanOffset = recipe.GridRadius * Mathf.Max(
                Mathf.Max(0f, recipe.OceanDepth),
                Mathf.Max(0f, recipe.MinimumOceanDepth));

            int requiredStartOffset = -Mathf.CeilToInt(maxOceanOffset + maxNoiseOffset) - safeMargin;
            int requiredEndOffset = Mathf.CeilToInt(maxLandOffset + maxNoiseOffset) + safeMargin;
            requiredStartOffset = Math.Max(requiredStartOffset, -recipe.GridRadius + 1);
            requiredEndOffset = Math.Max(requiredEndOffset, requiredStartOffset + 1);

            return new PlanetMarchingCubesSurfaceRange
            {
                radialStartOffset = requiredStartOffset,
                radialCubeCount = requiredEndOffset - requiredStartOffset,
                faceResolution = safeFaceResolution,
                cubeSizeGrid = 1f
            };
        }

        public void ExpandToCover(in PlanetRecipe recipe)
        {
            PlanetMarchingCubesSurfaceRange required = RequiredForRecipe(in recipe, faceResolution);
            int start = Math.Min(radialStartOffset, required.radialStartOffset);
            int end = Math.Max(RadialEndOffset, required.RadialEndOffset);
            radialStartOffset = start;
            radialCubeCount = end - start;
            faceResolution = faceResolution > 0 ? faceResolution : required.faceResolution;
            cubeSizeGrid = 1f;
        }

        public bool Validate(out string message)
        {
            if (radialCubeCount <= 0)
            {
                message = "surface radialCubeCount must be greater than zero.";
                return false;
            }

            if (faceResolution <= 0)
            {
                message = "surface faceResolution must be greater than zero.";
                return false;
            }

            if (cubeSizeGrid != 1f)
            {
                message = "surface cubeSizeGrid must remain 1 while the recipe grid cell is 1x1x1.";
                return false;
            }

            if (CubeCount > int.MaxValue)
            {
                message = "The planet surface cube count must fit in a compute dispatch int.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        public override string ToString()
        {
            return "surfaceRadialStartOffset=" + radialStartOffset +
                   "\nsurfaceRadialCubeCount=" + radialCubeCount +
                   "\nsurfaceRadialEndOffset=" + RadialEndOffset +
                   "\nsurfaceFaceResolution=" + faceResolution +
                   "\nsurfaceCubeSizeGrid=" + cubeSizeGrid +
                   "\nsurfaceCubeCount=" + CubeCount;
        }
    }
}
