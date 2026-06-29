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
        [SerializeField] private float minRoughness;
        [SerializeField] private float maxRoughness;
        [SerializeField] private int recipeVersion;

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
            get => surfaceNoiseOctaves <= 0 ? DefaultSurfaceNoiseOctaves : surfaceNoiseOctaves;
            set => surfaceNoiseOctaves = value;
        }

        public float SurfaceNoiseLacunarity
        {
            get => surfaceNoiseLacunarity <= 0f ? DefaultSurfaceNoiseLacunarity : surfaceNoiseLacunarity;
            set => surfaceNoiseLacunarity = value;
        }

        public float SurfaceNoisePersistence
        {
            get => surfaceNoisePersistence <= 0f ? DefaultSurfaceNoisePersistence : surfaceNoisePersistence;
            set => surfaceNoisePersistence = value;
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

        public int RecipeVersion
        {
            get => recipeVersion;
            set => recipeVersion = value;
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
            if (recipeVersion < 4)
            {
                continentEdgeWidthMin = DefaultContinentEdgeWidthMin;
                continentEdgeWidthMax = DefaultContinentEdgeWidthMax;
                continentEdgeShiftStrength = DefaultContinentEdgeShiftStrength;
                recipeVersion = 4;
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
                surfaceNoiseAmplitude = 0.308f,
                surfaceNoiseFrequency = 7f,
                surfaceNoiseOctaves = DefaultSurfaceNoiseOctaves,
                surfaceNoiseLacunarity = DefaultSurfaceNoiseLacunarity,
                surfaceNoisePersistence = DefaultSurfaceNoisePersistence,
                minRoughness = 0.6f,
                maxRoughness = 0.8f,
                recipeVersion = 4
            };
        }
    }
}
