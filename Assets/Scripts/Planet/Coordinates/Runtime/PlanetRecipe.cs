using System;
using UnityEngine;

namespace MarchingCubesPlanet.Coordinates
{
    [Serializable]
    public struct PlanetRecipe
    {
        [SerializeField] private int gridRadius;
        [SerializeField] private float worldScale;
        [SerializeField] private int seed;
        [SerializeField] private float isoLevel;
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

        public static PlanetRecipe Default()
        {
            return new PlanetRecipe
            {
                gridRadius = 1000,
                worldScale = 4f,
                seed = 12345,
                isoLevel = 0f,
                recipeVersion = 1
            };
        }
    }
}
