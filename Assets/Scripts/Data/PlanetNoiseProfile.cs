using System;
using Unity.Mathematics;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Data
{
    [CreateAssetMenu(menuName = "Voxel Engine/Planet Noise Profile")]
    public sealed class PlanetNoiseProfile : ScriptableObject
    {
        private const int CurveSampleCount = 16;
        private const int MaxVoronoiCells = 256;

        [Header("Planet")]
        [SerializeField, Min(1)] private int voronoiDivision = 56;
        [SerializeField, Min(0)] private int voronoiContinentMinRange = 43;
        [SerializeField, Min(0)] private int voronoiContinentMaxRange = 43;

        [Header("Legacy Shape")]
        [SerializeField, Range(0f, 1f)] private float landMinElevation = 0.015f;
        [SerializeField, Range(0f, 1f)] private float landMaxElevation = 0.127f;
        [SerializeField] private AnimationCurve landElevationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float oceanDepth = 0.16f;
        [SerializeField, Range(0f, 1f)] private float minimumOceanDepth = 0.03f;
        [SerializeField, Range(0f, 1f)] private float continentEdgeBlend = 0.16f;
        [SerializeField] private AnimationCurve continentBlendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float surfaceNoiseAmplitude = 0.308f;
        [SerializeField, Min(0.01f)] private float surfaceNoiseFrequency = 2f;
        [SerializeField, Range(0.05f, 0.95f)] private float seaLevelAtlasV = 0.337f;

        [Header("Voronoi Cell Variation")]
        [SerializeField] private Vector2 cellHeightModifierRange = new Vector2(1f, 1f);
        [SerializeField] private Vector2 cellRoughnessModifierRange = new Vector2(1f, 1f);

        public float GetMinimumSurfaceOffset(float referenceRadius)
        {
            float maxOceanDepth = Mathf.Max(oceanDepth, minimumOceanDepth);
            return -Mathf.Max(0f, referenceRadius) * (maxOceanDepth + surfaceNoiseAmplitude);
        }

        public float GetMaximumSurfaceOffset(float referenceRadius)
        {
            return Mathf.Max(0f, referenceRadius) * (landMaxElevation + surfaceNoiseAmplitude);
        }

        public float GetSeaSurfaceOffset(float referenceRadius)
        {
            return 0f;
        }

        public PlanetNoiseSettings BuildRuntimeSettings(int seed)
        {
            PlanetNoiseSettings settings = PlanetNoiseSettings.Default;
            int cellCount = Mathf.Clamp(voronoiDivision, 1, MaxVoronoiCells);
            int minLandCells = Mathf.Clamp(voronoiContinentMinRange, 0, cellCount);
            int maxLandCells = Mathf.Clamp(voronoiContinentMaxRange, minLandCells, cellCount);

            settings.enabled = 1;
            settings.seed = seed;
            settings.voronoiDivision = cellCount;
            settings.landCellCount = GetDeterministicRange(seed ^ 0x4f1bbcdc, minLandCells, maxLandCells);
            settings.landMinElevation = Mathf.Max(0f, Mathf.Min(landMinElevation, landMaxElevation));
            settings.landMaxElevation = Mathf.Max(settings.landMinElevation, Mathf.Max(landMinElevation, landMaxElevation));
            settings.oceanDepth = Mathf.Max(0f, oceanDepth);
            settings.minimumOceanDepth = Mathf.Max(0f, minimumOceanDepth);
            settings.continentEdgeBlend = Mathf.Max(0f, continentEdgeBlend);
            settings.surfaceNoiseAmplitude = Mathf.Max(0f, surfaceNoiseAmplitude);
            settings.surfaceNoiseFrequency = Mathf.Max(0.01f, surfaceNoiseFrequency);
            settings.seaLevelAtlasV = Mathf.Clamp(seaLevelAtlasV, 0.05f, 0.95f);
            settings.cellHeightModifierMin = Mathf.Min(cellHeightModifierRange.x, cellHeightModifierRange.y);
            settings.cellHeightModifierMax = Mathf.Max(cellHeightModifierRange.x, cellHeightModifierRange.y);
            settings.cellRoughnessModifierMin = Mathf.Min(cellRoughnessModifierRange.x, cellRoughnessModifierRange.y);
            settings.cellRoughnessModifierMax = Mathf.Max(cellRoughnessModifierRange.x, cellRoughnessModifierRange.y);
            BakeCurve(ref settings, landElevationCurve, false);
            BakeCurve(ref settings, continentBlendCurve, true);
            BakeLegacyVoronoi(ref settings, seed);
            return settings;
        }

        private void OnValidate()
        {
            voronoiDivision = Mathf.Clamp(voronoiDivision, 1, MaxVoronoiCells);
            voronoiContinentMinRange = Mathf.Clamp(voronoiContinentMinRange, 0, voronoiDivision);
            voronoiContinentMaxRange = Mathf.Clamp(voronoiContinentMaxRange, voronoiContinentMinRange, voronoiDivision);
            landMinElevation = Mathf.Clamp01(landMinElevation);
            landMaxElevation = Mathf.Max(landMinElevation, Mathf.Clamp01(landMaxElevation));
            oceanDepth = Mathf.Clamp01(oceanDepth);
            minimumOceanDepth = Mathf.Clamp01(minimumOceanDepth);
            continentEdgeBlend = Mathf.Clamp01(continentEdgeBlend);
            surfaceNoiseAmplitude = Mathf.Clamp01(surfaceNoiseAmplitude);
            surfaceNoiseFrequency = Mathf.Max(0.01f, surfaceNoiseFrequency);
            seaLevelAtlasV = Mathf.Clamp(seaLevelAtlasV, 0.05f, 0.95f);
            cellHeightModifierRange = SortRange(cellHeightModifierRange);
            cellRoughnessModifierRange = SortRange(cellRoughnessModifierRange);
            if (landElevationCurve == null || landElevationCurve.length == 0)
            {
                landElevationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            }

            if (continentBlendCurve == null || continentBlendCurve.length == 0)
            {
                continentBlendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            }
        }

        private void BakeCurve(ref PlanetNoiseSettings settings, AnimationCurve curve, bool blendCurve)
        {
            for (int i = 0; i < CurveSampleCount; i++)
            {
                float t = i / (CurveSampleCount - 1f);
                if (blendCurve)
                {
                    settings.SetBlendCurveSample(i, Mathf.Clamp01(curve.Evaluate(t)));
                }
                else
                {
                    settings.SetCurveSample(i, Mathf.Clamp01(curve.Evaluate(t)));
                }
            }
        }

        private void BakeLegacyVoronoi(ref PlanetNoiseSettings settings, int seed)
        {
            settings.ClearLandMask();
            settings.voronoiCenters.Clear();
            settings.landElevations.Clear();

            int cellCount = settings.voronoiDivision;
            for (int i = 0; i < cellCount; i++)
            {
                settings.voronoiCenters.Add(RandomUnitVector(seed, i));
                settings.landElevations.Add(0f);
            }

            int[] indices = new int[cellCount];
            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = i;
            }

            System.Random landRandom = new System.Random(MixSeed(seed, 0x13a5ba1d));
            for (int i = indices.Length - 1; i > 0; i--)
            {
                int swapIndex = landRandom.Next(i + 1);
                (indices[i], indices[swapIndex]) = (indices[swapIndex], indices[i]);
            }

            for (int i = 0; i < settings.landCellCount; i++)
            {
                settings.SetLandCell(indices[i]);
            }

            System.Random elevationRandom = new System.Random(MixSeed(seed, 0x26cb5d35));
            for (int i = 0; i < cellCount; i++)
            {
                if (!settings.IsLandCell(i))
                {
                    continue;
                }

                float t = (float)elevationRandom.NextDouble();
                if (landElevationCurve != null && landElevationCurve.length > 0)
                {
                    t = Mathf.Clamp01(landElevationCurve.Evaluate(t));
                }

                settings.landElevations[i] = Mathf.Lerp(settings.landMinElevation, settings.landMaxElevation, t);
            }
        }

        private static float3 RandomUnitVector(int seed, int index)
        {
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
            return new float3(
                Mathf.Cos((float)angle) * horizontalRadius,
                zFloat,
                Mathf.Sin((float)angle) * horizontalRadius);
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

        private static int GetDeterministicRange(int nextSeed, int minInclusive, int maxInclusive)
        {
            if (maxInclusive <= minInclusive)
            {
                return minInclusive;
            }

            uint hash = PlanetNoiseSettings.Hash((uint)nextSeed, 0x7f4a7c15u);
            int range = maxInclusive - minInclusive + 1;
            return minInclusive + (int)(hash % (uint)range);
        }

        private static Vector2 SortRange(Vector2 range)
        {
            return new Vector2(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
        }
    }
}
