using System.Runtime.InteropServices;
using MarchingCubesPlanet.Coordinates;
using UnityEngine;

namespace MarchingCubesPlanet.Shape
{
    [StructLayout(LayoutKind.Sequential)]
    public struct PlanetGpuShapeParameters
    {
        public const int Stride = 128;

        public Vector4 radiusIsoSeedCellCount;
        public Vector4 elevation;
        public Vector4 oceanBlend;
        public Vector4 noise;
        public Vector4 noiseFractal;
        public Vector4 continentEdgeShape;
        public Vector4 biomeShape;
        public Vector4 mountainBiome;

        public static PlanetGpuShapeParameters FromRecipe(in PlanetRecipe recipe)
        {
            return new PlanetGpuShapeParameters
            {
                radiusIsoSeedCellCount = new Vector4(
                    recipe.GridRadius,
                    recipe.IsoLevel,
                    recipe.Seed,
                    recipe.VoronoiDivision),
                elevation = new Vector4(
                    recipe.MinLandElevation,
                    recipe.MaxLandElevation,
                    recipe.MinHeightModifier,
                    recipe.MaxHeightModifier),
                oceanBlend = new Vector4(
                    recipe.OceanDepth,
                    recipe.MinimumOceanDepth,
                    recipe.ContinentEdgeBlend,
                    0f),
                noise = new Vector4(
                    recipe.SurfaceNoiseAmplitude,
                    recipe.SurfaceNoiseFrequency,
                    recipe.MinRoughness,
                    recipe.MaxRoughness),
                noiseFractal = new Vector4(
                    recipe.SurfaceNoiseOctaves,
                    recipe.SurfaceNoiseLacunarity,
                    recipe.SurfaceNoisePersistence,
                    recipe.SurfaceNoiseResponsePower),
                continentEdgeShape = new Vector4(
                    recipe.ContinentEdgeWidthMin,
                    recipe.ContinentEdgeWidthMax,
                    recipe.ContinentEdgeShiftStrength,
                    0f),
                biomeShape = new Vector4(
                    recipe.MountainBiomeHeight,
                    recipe.MountainBiomePeakRadius,
                    recipe.MountainBiomeEdgeBlend,
                    recipe.MountainBiomePeakSpread),
                mountainBiome = new Vector4(
                    recipe.MountainBiomeMinPeaks,
                    recipe.MountainBiomeMaxPeaks,
                    recipe.MountainBiomePeakFalloff,
                    0f)
            };
        }
    }
}
