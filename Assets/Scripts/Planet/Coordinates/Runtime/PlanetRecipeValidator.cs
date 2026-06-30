namespace MarchingCubesPlanet.Coordinates
{
    public static class PlanetRecipeValidator
    {
        public static bool Validate(in PlanetRecipe recipe, out string message)
        {
            if (recipe.GridRadius <= 0)
            {
                message = "GridRadius must be greater than zero.";
                return false;
            }

            if (recipe.WorldScale <= 0f)
            {
                message = "WorldScale must be greater than zero.";
                return false;
            }

            if (recipe.VoronoiDivision <= 0)
            {
                message = "VoronoiDivision must be greater than zero.";
                return false;
            }

            if (recipe.ContinentCells < 0)
            {
                message = "ContinentCells must be greater than or equal to zero.";
                return false;
            }

            if (recipe.ContinentCells > recipe.VoronoiDivision)
            {
                message = "ContinentCells must be lower than or equal to VoronoiDivision.";
                return false;
            }

            if (recipe.ContinentEdgeBlend <= 0f)
            {
                message = "ContinentEdgeBlend must be greater than zero.";
                return false;
            }

            if (recipe.ContinentEdgeWidthMin <= 0f)
            {
                message = "ContinentEdgeWidthMin must be greater than zero.";
                return false;
            }

            if (recipe.ContinentEdgeWidthMax < recipe.ContinentEdgeWidthMin)
            {
                message = "ContinentEdgeWidthMax must be greater than or equal to ContinentEdgeWidthMin.";
                return false;
            }

            if (recipe.ContinentEdgeShiftStrength < 0f || recipe.ContinentEdgeShiftStrength > 2f)
            {
                message = "ContinentEdgeShiftStrength must be between zero and two.";
                return false;
            }

            if (recipe.MinLandElevation < 0f || recipe.MaxLandElevation < recipe.MinLandElevation)
            {
                message = "Land elevation range is invalid.";
                return false;
            }

            if (recipe.MinHeightModifier <= 0f || recipe.MaxHeightModifier < recipe.MinHeightModifier)
            {
                message = "Height modifier range is invalid.";
                return false;
            }

            if (recipe.OceanDepth < 0f || recipe.MinimumOceanDepth < 0f)
            {
                message = "Ocean depth values must be greater than or equal to zero.";
                return false;
            }

            if (recipe.SurfaceNoiseAmplitude < 0f)
            {
                message = "SurfaceNoiseAmplitude must be greater than or equal to zero.";
                return false;
            }

            if (recipe.SurfaceNoiseFrequency <= 0f)
            {
                message = "SurfaceNoiseFrequency must be greater than zero.";
                return false;
            }

            if (recipe.SurfaceNoiseOctaves < 1 || recipe.SurfaceNoiseOctaves > 8)
            {
                message = "SurfaceNoiseOctaves must be between 1 and 8.";
                return false;
            }

            if (recipe.SurfaceNoiseLacunarity <= 0f)
            {
                message = "SurfaceNoiseLacunarity must be greater than zero.";
                return false;
            }

            if (recipe.SurfaceNoisePersistence <= 0f || recipe.SurfaceNoisePersistence > 1f)
            {
                message = "SurfaceNoisePersistence must be greater than zero and lower than or equal to one.";
                return false;
            }

            if (recipe.SurfaceNoiseResponsePower <= 0f || recipe.SurfaceNoiseResponsePower > 8f)
            {
                message = "SurfaceNoiseResponsePower must be greater than zero and lower than or equal to eight.";
                return false;
            }

            if (recipe.MountainBiomeCells < 0)
            {
                message = "MountainBiomeCells must be greater than or equal to zero.";
                return false;
            }

            if (recipe.MountainBiomeCells > recipe.ContinentCells)
            {
                message = "MountainBiomeCells must be lower than or equal to ContinentCells.";
                return false;
            }

            if (recipe.MountainBiomeMinPeaks < 1 || recipe.MountainBiomeMinPeaks > 4)
            {
                message = "MountainBiomeMinPeaks must be between 1 and 4.";
                return false;
            }

            if (recipe.MountainBiomeMaxPeaks < recipe.MountainBiomeMinPeaks || recipe.MountainBiomeMaxPeaks > 4)
            {
                message = "MountainBiomeMaxPeaks must be greater than or equal to MountainBiomeMinPeaks and lower than or equal to 4.";
                return false;
            }

            if (recipe.MountainBiomeHeight < 0f)
            {
                message = "MountainBiomeHeight must be greater than or equal to zero.";
                return false;
            }

            if (recipe.MountainBiomePeakRadius <= 0f || recipe.MountainBiomePeakRadius > 1f)
            {
                message = "MountainBiomePeakRadius must be greater than zero and lower than or equal to one.";
                return false;
            }

            if (recipe.MountainBiomePeakSpread < 0f || recipe.MountainBiomePeakSpread > 2f)
            {
                message = "MountainBiomePeakSpread must be between zero and two.";
                return false;
            }

            if (recipe.MountainBiomeEdgeBlend <= 0f || recipe.MountainBiomeEdgeBlend > 1f)
            {
                message = "MountainBiomeEdgeBlend must be greater than zero and lower than or equal to one.";
                return false;
            }

            if (recipe.MountainBiomePeakFalloff <= 0f || recipe.MountainBiomePeakFalloff > 16f)
            {
                message = "MountainBiomePeakFalloff must be greater than zero and lower than or equal to sixteen.";
                return false;
            }

            if (recipe.MinRoughness <= 0f || recipe.MaxRoughness < recipe.MinRoughness)
            {
                message = "Roughness range is invalid.";
                return false;
            }

            message = "PlanetRecipe is valid.";
            return true;
        }
    }
}
