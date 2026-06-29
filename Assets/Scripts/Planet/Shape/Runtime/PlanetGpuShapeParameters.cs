using System.Runtime.InteropServices;
using MarchingCubesPlanet.Coordinates;
using UnityEngine;

namespace MarchingCubesPlanet.Shape
{
    [StructLayout(LayoutKind.Sequential)]
    public struct PlanetGpuShapeParameters
    {
        public const int Stride = 80;

        public Vector4 radiusIsoSeedCellCount;
        public Vector4 elevation;
        public Vector4 oceanBlend;
        public Vector4 noise;
        public Vector4 noiseFractal;

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
                    0f)
            };
        }
    }
}
