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

            if (recipe.RecipeVersion <= 0)
            {
                message = "RecipeVersion must be greater than zero.";
                return false;
            }

            message = "PlanetRecipe is valid.";
            return true;
        }
    }
}
