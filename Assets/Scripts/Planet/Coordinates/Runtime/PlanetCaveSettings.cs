using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace MarchingCubesPlanet.Coordinates
{
    [Serializable]
    public struct PlanetCaveSettings
    {
        public const int AppearanceMaximum = 255;
        private const int CurrentAuthoringVersion = 2;

        [SerializeField] private bool enabled;

        [SerializeField, Range(0, AppearanceMaximum)] private int minAppearance;
        [SerializeField, Range(0, AppearanceMaximum)] private int maxAppearance;

        [SerializeField, Range(0, 100)] private int porosity;
        [SerializeField, Range(0, 100)] private int connectivity;

        [FormerlySerializedAs("scaleMax")]
        [SerializeField, Min(1f)] private float cavernScale;

        [FormerlySerializedAs("scaleMin")]
        [SerializeField, Min(1f)] private float passageScale;

        [SerializeField, Range(0, 100)] private int tortuosity;

        [FormerlySerializedAs("chamberAbundance")]
        [SerializeField, Range(0, 100)] private int cavernAbundance;

        [FormerlySerializedAs("tunnelAbundance")]
        [SerializeField, Range(0, 100)] private int passageAbundance;

        [FormerlySerializedAs("faultAbundance")]
        [SerializeField, Range(0, 100)] private int fractureAbundance;

        [FormerlySerializedAs("entranceProbability")]
        [SerializeField, Range(0, 100)] private int entranceAbundance;

        [FormerlySerializedAs("wallRoughness")]
        [SerializeField, Range(0, 100)] private int wallDetail;

        [SerializeField] private int seedOffset;
        [SerializeField, HideInInspector] private int authoringVersion;

        public bool Enabled
        {
            get => enabled;
            set => enabled = value;
        }

        public int MinAppearance
        {
            get => minAppearance;
            set => minAppearance = value;
        }

        public int MaxAppearance
        {
            get => maxAppearance;
            set => maxAppearance = value;
        }

        public int Porosity
        {
            get => porosity;
            set => porosity = value;
        }

        public int Connectivity
        {
            get => connectivity;
            set => connectivity = value;
        }

        public float CavernScale
        {
            get => cavernScale;
            set => cavernScale = value;
        }

        public float PassageScale
        {
            get => passageScale;
            set => passageScale = value;
        }

        public int Tortuosity
        {
            get => tortuosity;
            set => tortuosity = value;
        }

        public int CavernAbundance
        {
            get => cavernAbundance;
            set => cavernAbundance = value;
        }

        public int PassageAbundance
        {
            get => passageAbundance;
            set => passageAbundance = value;
        }

        public int FractureAbundance
        {
            get => fractureAbundance;
            set => fractureAbundance = value;
        }

        public int EntranceAbundance
        {
            get => entranceAbundance;
            set => entranceAbundance = value;
        }

        public int WallDetail
        {
            get => wallDetail;
            set => wallDetail = value;
        }

        public int SeedOffset
        {
            get => seedOffset;
            set => seedOffset = value;
        }

        public static PlanetCaveSettings Default()
        {
            return new PlanetCaveSettings
            {
                enabled = true,
                minAppearance = 0,
                maxAppearance = AppearanceMaximum,
                porosity = 42,
                connectivity = 74,
                cavernScale = 72f,
                passageScale = 24f,
                tortuosity = 68,
                cavernAbundance = 58,
                passageAbundance = 88,
                fractureAbundance = 24,
                entranceAbundance = 22,
                wallDetail = 34,
                seedOffset = 1709,
                authoringVersion = CurrentAuthoringVersion
            };
        }

        public void EnsureInitialized()
        {
            if (authoringVersion <= 0)
            {
                this = Default();
                return;
            }

            if (authoringVersion < CurrentAuthoringVersion)
            {
                bool previousEnabled = enabled;
                int previousMinAppearance = minAppearance;
                int previousMaxAppearance = maxAppearance;
                int previousSeedOffset = seedOffset;
                this = Default();
                enabled = previousEnabled;
                minAppearance = Mathf.Clamp(previousMinAppearance, 0, AppearanceMaximum);
                maxAppearance = Mathf.Clamp(previousMaxAppearance, minAppearance, AppearanceMaximum);
                seedOffset = previousSeedOffset;
            }
        }

        public PlanetCaveSettings Scaled(float gridScale)
        {
            PlanetCaveSettings result = this;
            float safeScale = Mathf.Max(0.000001f, gridScale);
            result.cavernScale *= safeScale;
            result.passageScale *= safeScale;
            return result;
        }
    }
}
