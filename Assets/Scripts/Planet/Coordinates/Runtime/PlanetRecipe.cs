using System;
using UnityEngine;

namespace MarchingCubesPlanet.Coordinates
{
    [Serializable]
    public struct PlanetRecipe : ISerializationCallbackReceiver
    {
        public const int DefaultSurfaceNoiseOctaves = 4;
        public const float DefaultSurfaceNoiseLacunarity = 2f;
        public const float DefaultSurfaceNoisePersistence = 0.5f;
        public const float DefaultSurfaceNoiseResponsePower = 3.5f;
        public const int DefaultMountainBiomeCells = 10;
        public const int DefaultMountainBiomeMinPeaks = 1;
        public const int DefaultMountainBiomeMaxPeaks = 4;
        public const float DefaultMountainBiomeHeight = 0.18f;
        public const float DefaultMountainBiomePeakRadius = 0.055f;
        public const float DefaultMountainBiomePeakSpread = 0.45f;
        public const float DefaultMountainBiomeEdgeBlend = 0.18f;
        public const float DefaultMountainBiomePeakFalloff = 2.25f;
        public const float DefaultContinentEdgeWidthMin = 0.65f;
        public const float DefaultContinentEdgeWidthMax = 1.75f;
        public const float DefaultContinentEdgeShiftStrength = 0.75f;

        [SerializeField] private int gridRadius;
        [SerializeField] private float worldScale;
        [SerializeField] private int seed;
        [SerializeField] private float isoLevel;
        [SerializeField] private int voronoiDivision;
        [SerializeField] private int continentCells;
        [SerializeField] private float continentEdgeBlend;
        [SerializeField] private float continentEdgeWidthMin;
        [SerializeField] private float continentEdgeWidthMax;
        [SerializeField] private float continentEdgeShiftStrength;
        [SerializeField] private float minLandElevation;
        [SerializeField] private float maxLandElevation;
        [SerializeField] private float minHeightModifier;
        [SerializeField] private float maxHeightModifier;
        [SerializeField] private float oceanDepth;
        [SerializeField] private float minimumOceanDepth;
        [SerializeField] private float surfaceNoiseAmplitude;
        [SerializeField] private float surfaceNoiseFrequency;
        [SerializeField] private int surfaceNoiseOctaves;
        [SerializeField] private float surfaceNoiseLacunarity;
        [SerializeField] private float surfaceNoisePersistence;
        [SerializeField] private float surfaceNoiseResponsePower;
        [SerializeField] private int mountainBiomeCells;
        [SerializeField] private int mountainBiomeMinPeaks;
        [SerializeField] private int mountainBiomeMaxPeaks;
        [SerializeField] private float mountainBiomeHeight;
        [SerializeField] private float mountainBiomePeakRadius;
        [SerializeField] private float mountainBiomePeakSpread;
        [SerializeField] private float mountainBiomeEdgeBlend;
        [SerializeField] private float mountainBiomePeakFalloff;
        [SerializeField] private float minRoughness;
        [SerializeField] private float maxRoughness;

        public int GridRadius
        {
            get => gridRadius;
            set => gridRadius = value;
        }

        public float WorldScale
        {
            get => worldScale;
            set => worldScale = value;
        }

        public int Seed
        {
            get => seed;
            set => seed = value;
        }

        public float IsoLevel
        {
            get => isoLevel;
            set => isoLevel = value;
        }

        public int VoronoiDivision
        {
            get => voronoiDivision;
            set => voronoiDivision = value;
        }

        public int ContinentCells
        {
            get => continentCells;
            set => continentCells = value;
        }

        public float ContinentEdgeBlend
        {
            get => continentEdgeBlend;
            set => continentEdgeBlend = value;
        }

        public float ContinentEdgeWidthMin
        {
            get => continentEdgeWidthMin;
            set => continentEdgeWidthMin = value;
        }

        public float ContinentEdgeWidthMax
        {
            get => continentEdgeWidthMax;
            set => continentEdgeWidthMax = value;
        }

        public float ContinentEdgeShiftStrength
        {
            get => continentEdgeShiftStrength;
            set => continentEdgeShiftStrength = value;
        }

        public float MinLandElevation
        {
            get => minLandElevation;
            set => minLandElevation = value;
        }

        public float MaxLandElevation
        {
            get => maxLandElevation;
            set => maxLandElevation = value;
        }

        public float MinHeightModifier
        {
            get => minHeightModifier;
            set => minHeightModifier = value;
        }

        public float MaxHeightModifier
        {
            get => maxHeightModifier;
            set => maxHeightModifier = value;
        }

        public float OceanDepth
        {
            get => oceanDepth;
            set => oceanDepth = value;
        }

        public float MinimumOceanDepth
        {
            get => minimumOceanDepth;
            set => minimumOceanDepth = value;
        }

        public float SurfaceNoiseAmplitude
        {
            get => surfaceNoiseAmplitude;
            set => surfaceNoiseAmplitude = value;
        }

        public float SurfaceNoiseFrequency
        {
            get => surfaceNoiseFrequency;
            set => surfaceNoiseFrequency = value;
        }

        public int SurfaceNoiseOctaves
        {
            get => surfaceNoiseOctaves;
            set => surfaceNoiseOctaves = value;
        }

        public float SurfaceNoiseLacunarity
        {
            get => surfaceNoiseLacunarity;
            set => surfaceNoiseLacunarity = value;
        }

        public float SurfaceNoisePersistence
        {
            get => surfaceNoisePersistence;
            set => surfaceNoisePersistence = value;
        }

        public float SurfaceNoiseResponsePower
        {
            get => surfaceNoiseResponsePower;
            set => surfaceNoiseResponsePower = value;
        }

        public int MountainBiomeCells
        {
            get => mountainBiomeCells;
            set => mountainBiomeCells = value;
        }

        public int MountainBiomeMinPeaks
        {
            get => mountainBiomeMinPeaks;
            set => mountainBiomeMinPeaks = value;
        }

        public int MountainBiomeMaxPeaks
        {
            get => mountainBiomeMaxPeaks;
            set => mountainBiomeMaxPeaks = value;
        }

        public float MountainBiomeHeight
        {
            get => mountainBiomeHeight;
            set => mountainBiomeHeight = value;
        }

        public float MountainBiomePeakRadius
        {
            get => mountainBiomePeakRadius;
            set => mountainBiomePeakRadius = value;
        }

        public float MountainBiomePeakSpread
        {
            get => mountainBiomePeakSpread;
            set => mountainBiomePeakSpread = value;
        }

        public float MountainBiomeEdgeBlend
        {
            get => mountainBiomeEdgeBlend;
            set => mountainBiomeEdgeBlend = value;
        }

        public float MountainBiomePeakFalloff
        {
            get => mountainBiomePeakFalloff;
            set => mountainBiomePeakFalloff = value;
        }

        public float MinRoughness
        {
            get => minRoughness;
            set => minRoughness = value;
        }

        public float MaxRoughness
        {
            get => maxRoughness;
            set => maxRoughness = value;
        }

        public int GridDiameter => gridRadius * 2;
        public float WorldRadius => gridRadius * worldScale;
        public float WorldDiameter => GridDiameter * worldScale;

        public bool IsValid(out string message)
        {
            return PlanetRecipeValidator.Validate(in this, out message);
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            if (continentEdgeWidthMin <= 0f)
            {
                continentEdgeWidthMin = DefaultContinentEdgeWidthMin;
            }

            if (continentEdgeWidthMax <= 0f || continentEdgeWidthMax < continentEdgeWidthMin)
            {
                continentEdgeWidthMax = DefaultContinentEdgeWidthMax;
            }

            if (surfaceNoiseOctaves <= 0)
            {
                surfaceNoiseOctaves = DefaultSurfaceNoiseOctaves;
            }

            if (surfaceNoiseLacunarity <= 0f)
            {
                surfaceNoiseLacunarity = DefaultSurfaceNoiseLacunarity;
            }

            if (surfaceNoisePersistence <= 0f)
            {
                surfaceNoisePersistence = DefaultSurfaceNoisePersistence;
            }

            if (surfaceNoiseResponsePower <= 0f)
            {
                surfaceNoiseResponsePower = DefaultSurfaceNoiseResponsePower;
            }

            if (mountainBiomeMinPeaks <= 0)
            {
                mountainBiomeMinPeaks = DefaultMountainBiomeMinPeaks;
            }

            if (mountainBiomeMaxPeaks <= 0)
            {
                mountainBiomeMaxPeaks = DefaultMountainBiomeMaxPeaks;
            }

            if (mountainBiomeMaxPeaks < mountainBiomeMinPeaks)
            {
                mountainBiomeMaxPeaks = mountainBiomeMinPeaks;
            }

            if (mountainBiomePeakRadius <= 0f)
            {
                mountainBiomePeakRadius = DefaultMountainBiomePeakRadius;
            }

            if (mountainBiomePeakSpread <= 0f)
            {
                mountainBiomePeakSpread = DefaultMountainBiomePeakSpread;
            }

            if (mountainBiomeEdgeBlend <= 0f)
            {
                mountainBiomeEdgeBlend = DefaultMountainBiomeEdgeBlend;
            }

            if (mountainBiomePeakFalloff <= 0f)
            {
                mountainBiomePeakFalloff = DefaultMountainBiomePeakFalloff;
            }

            if (mountainBiomeHeight <= 0f && mountainBiomeCells > 0)
            {
                mountainBiomeHeight = DefaultMountainBiomeHeight;
            }
        }

        public static PlanetRecipe Default()
        {
            return new PlanetRecipe
            {
                gridRadius = 1000,
                worldScale = 4f,
                seed = 12345,
                isoLevel = 0f,
                voronoiDivision = 100,
                continentCells = 84,
                continentEdgeBlend = 0.16f,
                continentEdgeWidthMin = DefaultContinentEdgeWidthMin,
                continentEdgeWidthMax = DefaultContinentEdgeWidthMax,
                continentEdgeShiftStrength = DefaultContinentEdgeShiftStrength,
                minLandElevation = 0.015f,
                maxLandElevation = 0.127f,
                minHeightModifier = 0.3f,
                maxHeightModifier = 1.5f,
                oceanDepth = 0.16f,
                minimumOceanDepth = 0.03f,
                surfaceNoiseAmplitude = 0.08f,
                surfaceNoiseFrequency = 7f,
                surfaceNoiseOctaves = DefaultSurfaceNoiseOctaves,
                surfaceNoiseLacunarity = DefaultSurfaceNoiseLacunarity,
                surfaceNoisePersistence = DefaultSurfaceNoisePersistence,
                surfaceNoiseResponsePower = DefaultSurfaceNoiseResponsePower,
                mountainBiomeCells = DefaultMountainBiomeCells,
                mountainBiomeMinPeaks = DefaultMountainBiomeMinPeaks,
                mountainBiomeMaxPeaks = DefaultMountainBiomeMaxPeaks,
                mountainBiomeHeight = DefaultMountainBiomeHeight,
                mountainBiomePeakRadius = DefaultMountainBiomePeakRadius,
                mountainBiomePeakSpread = DefaultMountainBiomePeakSpread,
                mountainBiomeEdgeBlend = DefaultMountainBiomeEdgeBlend,
                mountainBiomePeakFalloff = DefaultMountainBiomePeakFalloff,
                minRoughness = 0.6f,
                maxRoughness = 0.8f
            };
        }
    }
}
