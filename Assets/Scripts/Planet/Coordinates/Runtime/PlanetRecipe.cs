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
        public const int MaxMaterialLayers = 16;

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
        [SerializeField] private PlanetMaterialLayer[] materialLayers;

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

        public PlanetMaterialLayer[] MaterialLayers
        {
            get
            {
                EnsureMaterialLayers();
                return materialLayers;
            }
            set
            {
                materialLayers = value;
                EnsureMaterialLayers();
            }
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
            EnsureMaterialLayers();
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

            EnsureMaterialLayers();
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
                maxRoughness = 0.8f,
                materialLayers = CreateDefaultMaterialLayers()
            };
        }

        private void EnsureMaterialLayers()
        {
            if (materialLayers == null || materialLayers.Length == 0)
            {
                materialLayers = CreateDefaultMaterialLayers();
                return;
            }

            if (materialLayers.Length > MaxMaterialLayers)
            {
                Array.Resize(ref materialLayers, MaxMaterialLayers);
            }

            materialLayers[0] = materialLayers[0].AsRequiredBaseLayer();
            for (int i = 1; i < materialLayers.Length; i++)
            {
                materialLayers[i].EnsureValid();
            }
        }

        private static PlanetMaterialLayer[] CreateDefaultMaterialLayers()
        {
            return new[]
            {
                PlanetMaterialLayer.Base(
                        "Tierra basica",
                        PlanetLayerMaterial.BasicTerrain,
                        new Color(0.42f, 0.24f, 0.12f, 1f))
                    .WithSurfaceAppearance(new Color(0.24f, 0.58f, 0.20f, 1f), 85, 78, 0, 225),
                PlanetMaterialLayer.Replace(
                    "Arena",
                    PlanetLayerMaterial.Sand,
                    new Color(0.78f, 0.68f, 0.42f, 1f),
                    150,
                    235,
                    35,
                    82)
                    .WithUnderwaterAppearance(new Color(0.58f, 0.48f, 0.28f, 1f), 100, 72),
                PlanetMaterialLayer.Replace(
                    "Roca",
                    PlanetLayerMaterial.Rock,
                    new Color(0.46f, 0.46f, 0.43f, 1f),
                    0,
                    245,
                    34,
                    65)
            };
        }
    }

    public enum PlanetLayerOperation
    {
        BaseMaterial = 0,
        ReplaceMaterial = 1
    }

    public enum PlanetMaterialEnvironmentBehaviour
    {
        None = 0,
        OverrideColor = 1
    }

    public enum PlanetLayerMaterial
    {
        BasicTerrain = 0,
        Rock = 1,
        Grass = 2,
        Sand = 3,
        Marble = 4,
        Iron = 5,
        Copper = 6,
        Silicon = 7,
        Ice = 8,
        Air = 100
    }

    [Serializable]
    public struct PlanetMaterialLayer
    {
        public const int AppearanceMaximum = 255;
        private const int CurrentAuthoringVersion = 3;

        [SerializeField] private string name;
        [SerializeField] private bool enabled;
        [SerializeField] private PlanetLayerOperation operation;
        [SerializeField] private PlanetLayerMaterial material;
        [SerializeField] private Color atlasColor;

        [SerializeField, Range(0, AppearanceMaximum)] private int minAppearance;
        [SerializeField, Range(0, AppearanceMaximum)] private int maxAppearance;
        [SerializeField, Range(0, 100)] private int abundance;
        [SerializeField, Range(0, 100)] private int coherence;

        [SerializeField] private PlanetMaterialEnvironmentBehaviour underwaterBehaviour;
        [SerializeField] private Color underwaterAtlasColor;
        [SerializeField, Range(0, 100)] private int underwaterProbability;
        [SerializeField, Range(0, 100)] private int underwaterCoherence;

        [SerializeField] private PlanetMaterialEnvironmentBehaviour surfaceBehaviour;
        [SerializeField] private Color surfaceAtlasColor;
        [SerializeField, Range(0, 100)] private int surfaceProbability;
        [SerializeField, Range(0, 100)] private int surfaceCoherence;
        [SerializeField, Range(0, AppearanceMaximum)] private int surfaceMinAppearance;
        [SerializeField, Range(0, AppearanceMaximum)] private int surfaceMaxAppearance;

        [SerializeField, HideInInspector] private int authoringVersion;

        // Serialized only to migrate recipes authored before the compact material contract.
        [SerializeField, HideInInspector] private float heightMin01;
        [SerializeField, HideInInspector] private float heightMax01;
        [SerializeField, HideInInspector] private float falloffMin01;
        [SerializeField, HideInInspector] private float falloffMax01;
        [SerializeField, HideInInspector] private float coverage01;
        [SerializeField, HideInInspector] private float massScaleMinMeters;
        [SerializeField, HideInInspector] private float massScaleMaxMeters;
        [SerializeField, HideInInspector] private float massCoherence01;
        [SerializeField, HideInInspector] private float strength01;
        [SerializeField, HideInInspector] private float altitudeBias;
        [SerializeField, HideInInspector] private int seedOffset;

        public string Name => name;
        public bool Enabled => enabled;
        public PlanetLayerOperation Operation => operation;
        public PlanetLayerMaterial Material => material;
        public Color AtlasColor => atlasColor;
        public int MinAppearance => minAppearance;
        public int MaxAppearance => maxAppearance;
        public int Abundance => abundance;
        public int Coherence => coherence;
        public PlanetMaterialEnvironmentBehaviour UnderwaterBehaviour => underwaterBehaviour;
        public Color UnderwaterAtlasColor => underwaterAtlasColor;
        public int UnderwaterProbability => underwaterProbability;
        public int UnderwaterCoherence => underwaterCoherence;
        public PlanetMaterialEnvironmentBehaviour SurfaceBehaviour => surfaceBehaviour;
        public Color SurfaceAtlasColor => surfaceAtlasColor;
        public int SurfaceProbability => surfaceProbability;
        public int SurfaceCoherence => surfaceCoherence;
        public int SurfaceMinAppearance => surfaceMinAppearance;
        public int SurfaceMaxAppearance => surfaceMaxAppearance;

        public static PlanetMaterialLayer Base(string name, Color color)
        {
            return Base(name, PlanetLayerMaterial.BasicTerrain, color);
        }

        public static PlanetMaterialLayer Base(string name, PlanetLayerMaterial material, Color color)
        {
            return Create(name, PlanetLayerOperation.BaseMaterial, material, color, 0, AppearanceMaximum, 100, 100)
                .AsRequiredBaseLayer();
        }

        public static PlanetMaterialLayer Replace(
            string name,
            PlanetLayerMaterial material,
            Color color,
            int minAppearance,
            int maxAppearance,
            int abundance,
            int coherence)
        {
            return Create(
                name,
                PlanetLayerOperation.ReplaceMaterial,
                material,
                color,
                minAppearance,
                maxAppearance,
                abundance,
                coherence);
        }

        public PlanetMaterialLayer WithUnderwaterAppearance(Color color, int probability, int coherence = 50)
        {
            underwaterBehaviour = PlanetMaterialEnvironmentBehaviour.OverrideColor;
            underwaterAtlasColor = color;
            underwaterProbability = probability;
            underwaterCoherence = coherence;
            EnsureValid();
            return this;
        }

        public PlanetMaterialLayer WithSurfaceAppearance(
            Color color,
            int probability,
            int coherence = 50,
            int minAppearance = 0,
            int maxAppearance = AppearanceMaximum)
        {
            surfaceBehaviour = PlanetMaterialEnvironmentBehaviour.OverrideColor;
            surfaceAtlasColor = color;
            surfaceProbability = probability;
            surfaceCoherence = coherence;
            surfaceMinAppearance = minAppearance;
            surfaceMaxAppearance = maxAppearance;
            EnsureValid();
            return this;
        }

        public PlanetMaterialLayer AsRequiredBaseLayer()
        {
            MigrateLegacyAuthoring();
            enabled = true;
            operation = PlanetLayerOperation.BaseMaterial;
            if (atlasColor.a <= 0f)
            {
                atlasColor = new Color(0.78f, 0.36f, 0.55f, 1f);
            }

            minAppearance = 0;
            maxAppearance = AppearanceMaximum;
            abundance = 100;
            return this;
        }

        public void EnsureValid()
        {
            MigrateLegacyAuthoring();
            if (string.IsNullOrWhiteSpace(name))
            {
                name = material.ToString();
            }

            operation = operation == PlanetLayerOperation.BaseMaterial
                ? PlanetLayerOperation.BaseMaterial
                : PlanetLayerOperation.ReplaceMaterial;
            minAppearance = Mathf.Clamp(minAppearance, 0, AppearanceMaximum);
            maxAppearance = Mathf.Clamp(maxAppearance, minAppearance, AppearanceMaximum);
            abundance = Mathf.Clamp(abundance, 0, 100);
            coherence = Mathf.Clamp(coherence, 0, 100);
            underwaterProbability = Mathf.Clamp(underwaterProbability, 0, 100);
            underwaterCoherence = Mathf.Clamp(underwaterCoherence, 0, 100);
            surfaceProbability = Mathf.Clamp(surfaceProbability, 0, 100);
            surfaceCoherence = Mathf.Clamp(surfaceCoherence, 0, 100);
            surfaceMinAppearance = Mathf.Clamp(surfaceMinAppearance, 0, AppearanceMaximum);
            surfaceMaxAppearance = Mathf.Clamp(
                surfaceMaxAppearance,
                surfaceMinAppearance,
                AppearanceMaximum);
            if (atlasColor.a <= 0f)
            {
                atlasColor.a = 1f;
            }

            if (underwaterAtlasColor.a <= 0f)
            {
                underwaterAtlasColor = atlasColor;
            }

            if (surfaceAtlasColor.a <= 0f)
            {
                surfaceAtlasColor = atlasColor;
            }
        }

        private static PlanetMaterialLayer Create(
            string name,
            PlanetLayerOperation operation,
            PlanetLayerMaterial material,
            Color color,
            int minAppearance,
            int maxAppearance,
            int abundance,
            int coherence)
        {
            PlanetMaterialLayer layer = new PlanetMaterialLayer
            {
                name = name,
                enabled = true,
                operation = operation,
                material = material,
                atlasColor = color,
                minAppearance = minAppearance,
                maxAppearance = maxAppearance,
                abundance = abundance,
                coherence = coherence,
                underwaterAtlasColor = color,
                underwaterCoherence = 50,
                surfaceAtlasColor = color,
                surfaceCoherence = 50,
                surfaceMaxAppearance = AppearanceMaximum,
                authoringVersion = CurrentAuthoringVersion
            };
            layer.EnsureValid();
            return layer;
        }

        private void MigrateLegacyAuthoring()
        {
            if (authoringVersion >= CurrentAuthoringVersion)
            {
                return;
            }

            if (authoringVersion == 2)
            {
                surfaceMinAppearance = 0;
                surfaceMaxAppearance = AppearanceMaximum;
                authoringVersion = CurrentAuthoringVersion;
                return;
            }

            if (authoringVersion == 1)
            {
                underwaterCoherence = coherence;
                surfaceCoherence = coherence;
                surfaceMinAppearance = 0;
                surfaceMaxAppearance = AppearanceMaximum;
                authoringVersion = CurrentAuthoringVersion;
                return;
            }

            bool hasLegacyAuthoring = heightMax01 > 0f || coverage01 > 0f ||
                                      massScaleMinMeters > 0f || massScaleMaxMeters > 0f;
            if (hasLegacyAuthoring)
            {
                minAppearance = Mathf.RoundToInt(Mathf.Clamp01(heightMin01 / 1.5f) * AppearanceMaximum);
                maxAppearance = Mathf.RoundToInt(Mathf.Clamp01(heightMax01 / 1.5f) * AppearanceMaximum);
                abundance = Mathf.RoundToInt(Mathf.Clamp01(coverage01) * 100f);
                coherence = Mathf.RoundToInt(Mathf.Clamp01(massCoherence01) * 100f);
            }
            else
            {
                minAppearance = 0;
                maxAppearance = AppearanceMaximum;
                abundance = 100;
                coherence = 50;
            }

            operation = operation == PlanetLayerOperation.BaseMaterial
                ? PlanetLayerOperation.BaseMaterial
                : PlanetLayerOperation.ReplaceMaterial;
            underwaterAtlasColor = atlasColor;
            surfaceAtlasColor = atlasColor;
            underwaterProbability = 100;
            underwaterCoherence = coherence;
            surfaceProbability = 100;
            surfaceCoherence = coherence;
            surfaceMinAppearance = 0;
            surfaceMaxAppearance = AppearanceMaximum;
            authoringVersion = CurrentAuthoringVersion;
        }
    }
}
