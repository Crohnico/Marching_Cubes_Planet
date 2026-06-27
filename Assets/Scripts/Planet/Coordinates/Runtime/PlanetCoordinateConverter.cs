using UnityEngine;

namespace MarchingCubesPlanet.Coordinates
{
    public static class PlanetCoordinateConverter
    {
        public static Vector3 GridToWorld(Vector3 gridPosition, in PlanetRecipe recipe, in PlanetPlacement placement)
        {
            return placement.PlanetWorldCenter + gridPosition * recipe.WorldScale;
        }

        public static Vector3 WorldToGrid(Vector3 worldPosition, in PlanetRecipe recipe, in PlanetPlacement placement)
        {
            return (worldPosition - placement.PlanetWorldCenter) / recipe.WorldScale;
        }

        public static Bounds GridCellToWorldBounds(
            GridCellCoordinates cell,
            in PlanetRecipe recipe,
            in PlanetPlacement placement)
        {
            Vector3 worldMin = GridToWorld(cell.Min, recipe, placement);
            Vector3 worldMax = GridToWorld(cell.Max, recipe, placement);
            Bounds bounds = new Bounds();
            bounds.SetMinMax(worldMin, worldMax);
            return bounds;
        }

        public static Vector3 GridCellToGridCenter(GridCellCoordinates cell)
        {
            return cell.Center;
        }

        public static Vector3 GridCellToWorldCenter(
            GridCellCoordinates cell,
            in PlanetRecipe recipe,
            in PlanetPlacement placement)
        {
            return GridToWorld(cell.Center, recipe, placement);
        }

        public static float GridDistanceToWorldDistance(float gridDistance, in PlanetRecipe recipe)
        {
            return gridDistance * recipe.WorldScale;
        }

        public static float WorldDistanceToGridDistance(float worldDistance, in PlanetRecipe recipe)
        {
            return worldDistance / recipe.WorldScale;
        }
    }
}
