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
                PlanetMaterialLayer.Base("Tierra basica", new Color(0.78f, 0.36f, 0.55f, 1f)),
                PlanetMaterialLayer.Paint(
                    "Cesped",
                    PlanetLayerMaterial.Grass,
                    new Color(0.24f, 0.58f, 0.20f, 1f),
                    0.78f,
                    1.5f,
                    0.08f,
                    0.22f,
                    0.92f,
                    18f,
                    260f,
                    0.72f,
                    1f,
                    0.05f,
                    23),
                PlanetMaterialLayer.Paint(
                    "Arena",
                    PlanetLayerMaterial.Sand,
                    new Color(0.78f, 0.68f, 0.42f, 1f),
                    0.70f,
                    1.01f,
                    0.12f,
                    0.06f,
                    0.72f,
                    160f,
                    1400f,
                    0.9f,
                    0.95f,
                    -0.65f,
                    37),
                PlanetMaterialLayer.Overlay(
                    "Roca",
                    PlanetLayerMaterial.Rock,
                    new Color(0.46f, 0.46f, 0.43f, 1f),
                    0f,
                    1.5f,
                    0.08f,
                    0.08f,
                    0.34f,
                    1f,
                    1000f,
                    0.52f,
                    0.82f,
                    0.85f,
                    11)
            };
        }
    }

    public enum PlanetLayerOperation
    {
        BaseSurface = 0,
        PaintMaterial = 1,
        OverlayMaterial = 2,
        SubtractDensity = 3
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
        Air = 100
    }

    [Serializable]
    public struct PlanetMaterialLayer
    {
        [SerializeField] private string name;
        [SerializeField] private bool enabled;
        [SerializeField] private PlanetLayerOperation operation;
        [SerializeField] private PlanetLayerMaterial material;
        [SerializeField] private Color atlasColor;
        [SerializeField] private float heightMin01;
        [SerializeField] private float heightMax01;
        [SerializeField] private float falloffMin01;
        [SerializeField] private float falloffMax01;
        [SerializeField] private float coverage01;
        [SerializeField] private float massScaleMinMeters;
        [SerializeField] private float massScaleMaxMeters;
        [SerializeField] private float massCoherence01;
        [SerializeField] private float strength01;
        [SerializeField] private float altitudeBias;
        [SerializeField] private int seedOffset;

        public string Name => name;
        public bool Enabled => enabled;
        public PlanetLayerOperation Operation => operation;
        public PlanetLayerMaterial Material => material;
        public Color AtlasColor => atlasColor;
        public float HeightMin01 => heightMin01;
        public float HeightMax01 => heightMax01;
        public float FalloffMin01 => falloffMin01;
        public float FalloffMax01 => falloffMax01;
        public float Coverage01 => coverage01;
        public float MassScaleMinMeters => massScaleMinMeters;
        public float MassScaleMaxMeters => massScaleMaxMeters;
        public float MassCoherence01 => massCoherence01;
        public float Strength01 => strength01;
        public float AltitudeBias => altitudeBias;
        public int SeedOffset => seedOffset;

        public static PlanetMaterialLayer Base(string name, Color color)
        {
            return Paint(
                name,
                PlanetLayerMaterial.BasicTerrain,
                color,
                0f,
                1.5f,
                0f,
                0f,
                1f,
                1f,
                1f,
                1f,
                1f,
                0f,
                0).AsRequiredBaseLayer();
        }

        public static PlanetMaterialLayer Paint(
            string name,
            PlanetLayerMaterial material,
            Color color,
            float heightMin01,
            float heightMax01,
            float falloffMin01,
            float falloffMax01,
            float coverage01,
            float massScaleMinMeters,
            float massScaleMaxMeters,
            float massCoherence01,
            float strength01,
            float altitudeBias,
            int seedOffset)
        {
            PlanetMaterialLayer layer = new PlanetMaterialLayer
            {
                name = name,
                enabled = true,
                operation = PlanetLayerOperation.PaintMaterial,
                material = material,
                atlasColor = color,
                heightMin01 = heightMin01,
                heightMax01 = heightMax01,
                falloffMin01 = falloffMin01,
                falloffMax01 = falloffMax01,
                coverage01 = coverage01,
                massScaleMinMeters = massScaleMinMeters,
                massScaleMaxMeters = massScaleMaxMeters,
                massCoherence01 = massCoherence01,
                strength01 = strength01,
                altitudeBias = altitudeBias,
                seedOffset = seedOffset
            };
            layer.EnsureValid();
            return layer;
        }

        public static PlanetMaterialLayer Overlay(
            string name,
            PlanetLayerMaterial material,
            Color color,
            float heightMin01,
            float heightMax01,
            float falloffMin01,
            float falloffMax01,
            float coverage01,
            float massScaleMinMeters,
            float massScaleMaxMeters,
            float massCoherence01,
            float strength01,
            float altitudeBias,
            int seedOffset)
        {
            PlanetMaterialLayer layer = Paint(
                name,
                material,
                color,
                heightMin01,
                heightMax01,
                falloffMin01,
                falloffMax01,
                coverage01,
                massScaleMinMeters,
                massScaleMaxMeters,
                massCoherence01,
                strength01,
                altitudeBias,
                seedOffset);
            layer.operation = PlanetLayerOperation.OverlayMaterial;
            return layer;
        }

        public PlanetMaterialLayer AsRequiredBaseLayer()
        {
            enabled = true;
            operation = PlanetLayerOperation.BaseSurface;
            if (atlasColor.a <= 0f)
            {
                atlasColor = new Color(0.78f, 0.36f, 0.55f, 1f);
            }

            heightMin01 = 0f;
            heightMax01 = 1.5f;
            falloffMin01 = 0f;
            falloffMax01 = 0f;
            coverage01 = 1f;
            massScaleMinMeters = 1f;
            massScaleMaxMeters = 1f;
            massCoherence01 = 1f;
            strength01 = 1f;
            altitudeBias = 0f;
            return this;
        }

        public void EnsureValid()
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                name = material.ToString();
            }

            heightMin01 = Mathf.Clamp(heightMin01, 0f, 1.5f);
            heightMax01 = Mathf.Clamp(heightMax01, heightMin01, 1.5f);
            falloffMin01 = Mathf.Clamp01(falloffMin01);
            falloffMax01 = Mathf.Clamp01(falloffMax01);
            coverage01 = Mathf.Clamp01(coverage01);
            massScaleMinMeters = Mathf.Max(0.01f, massScaleMinMeters);
            massScaleMaxMeters = Mathf.Max(massScaleMinMeters, massScaleMaxMeters);
            massCoherence01 = Mathf.Clamp01(massCoherence01);
            strength01 = Mathf.Clamp01(strength01);
            altitudeBias = Mathf.Clamp(altitudeBias, -1f, 1f);
            if (atlasColor.a <= 0f)
            {
                atlasColor.a = 1f;
            }
        }
    }
}
