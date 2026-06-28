using UnityEngine;

namespace MarchingCubesPlanet.Coordinates
{
    public static class PlanetCoordinateConverter
    {
        public static Vector3 GridToWorld(Vector3 gridPosition, in PlanetRecipe recipe, in PlanetPlacement placement)
        {
            UniversePosition stellarPosition = GridToStellar(gridPosition, in recipe, in placement);
            return StellarToWorld(stellarPosition, in placement);
        }

        public static Vector3 WorldToGrid(Vector3 worldPosition, in PlanetRecipe recipe, in PlanetPlacement placement)
        {
            UniversePosition stellarPosition = WorldToStellar(worldPosition, in placement);
            return StellarToGrid(stellarPosition, in recipe, in placement);
        }

        public static UniversePosition GridToStellar(Vector3 gridPosition, in PlanetRecipe recipe, in PlanetPlacement placement)
        {
            Vector3 planetLocal = placement.PlanetRotation * (gridPosition * recipe.WorldScale);
            return placement.PlanetStellarCenter + planetLocal;
        }

        public static Vector3 StellarToGrid(UniversePosition stellarPosition, in PlanetRecipe recipe, in PlanetPlacement placement)
        {
            Vector3 planetLocal = stellarPosition - placement.PlanetStellarCenter;
            return Quaternion.Inverse(placement.PlanetRotation) * planetLocal / recipe.WorldScale;
        }

        public static Vector3 StellarToWorld(UniversePosition stellarPosition, in PlanetPlacement placement)
        {
            return stellarPosition - placement.ActiveOrigin;
        }

        public static UniversePosition WorldToStellar(Vector3 worldPosition, in PlanetPlacement placement)
        {
            return placement.ActiveOrigin + worldPosition;
        }

        public static Bounds GridCellToWorldBounds(
            GridCellCoordinates cell,
            in PlanetRecipe recipe,
            in PlanetPlacement placement)
        {
            Vector3 min = cell.Min;
            Vector3 max = cell.Max;
            Vector3 firstCorner = GridToWorld(new Vector3(min.x, min.y, min.z), in recipe, in placement);
            Bounds bounds = new Bounds(firstCorner, Vector3.zero);
            bounds.Encapsulate(GridToWorld(new Vector3(max.x, min.y, min.z), in recipe, in placement));
            bounds.Encapsulate(GridToWorld(new Vector3(min.x, max.y, min.z), in recipe, in placement));
            bounds.Encapsulate(GridToWorld(new Vector3(max.x, max.y, min.z), in recipe, in placement));
            bounds.Encapsulate(GridToWorld(new Vector3(min.x, min.y, max.z), in recipe, in placement));
            bounds.Encapsulate(GridToWorld(new Vector3(max.x, min.y, max.z), in recipe, in placement));
            bounds.Encapsulate(GridToWorld(new Vector3(min.x, max.y, max.z), in recipe, in placement));
            bounds.Encapsulate(GridToWorld(new Vector3(max.x, max.y, max.z), in recipe, in placement));
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
