using System;
using UnityEngine;

namespace MarchingCubesPlanet.Coordinates
{
    public static class GridCellQuery
    {
        private const float CellHalfDiagonal = 0.86602540378f;

        public static int Sphere(
            Vector3 center,
            float radius,
            GridCellQueryMode mode,
            GridCellCoordinates[] resultBuffer,
            out GridCellQueryResult result)
        {
            if (resultBuffer == null)
            {
                throw new ArgumentNullException(nameof(resultBuffer));
            }

            if (radius < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "Sphere query radius cannot be negative.");
            }

            GridCellCoordinates minCell = GridCellCoordinates.FromGridPosition(center - Vector3.one * radius);
            GridCellCoordinates maxCell = GridCellCoordinates.FromGridPosition(center + Vector3.one * radius);

            int written = 0;
            int matched = 0;

            for (int z = minCell.Z; z <= maxCell.Z; z++)
            {
                for (int y = minCell.Y; y <= maxCell.Y; y++)
                {
                    for (int x = minCell.X; x <= maxCell.X; x++)
                    {
                        GridCellCoordinates cell = new GridCellCoordinates(x, y, z);
                        if (!MatchesSphere(cell, center, radius, mode))
                        {
                            continue;
                        }

                        matched++;
                        if (written >= resultBuffer.Length)
                        {
                            continue;
                        }

                        resultBuffer[written++] = cell;
                    }
                }
            }

            result = new GridCellQueryResult(written, matched, matched > resultBuffer.Length, minCell, maxCell);
            return written;
        }

        public static bool MatchesSphere(
            GridCellCoordinates cell,
            Vector3 center,
            float radius,
            GridCellQueryMode mode)
        {
            float radiusSquared = radius * radius;

            switch (mode)
            {
                case GridCellQueryMode.CenterInside:
                    return (cell.Center - center).sqrMagnitude <= radiusSquared;
                case GridCellQueryMode.Intersects:
                    float conservativeRadius = radius + CellHalfDiagonal;
                    return (cell.Center - center).sqrMagnitude <= conservativeRadius * conservativeRadius;
                case GridCellQueryMode.FullyContained:
                    return IsFullyContained(cell, center, radiusSquared);
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown grid cell query mode.");
            }
        }

        private static bool IsFullyContained(GridCellCoordinates cell, Vector3 center, float radiusSquared)
        {
            Vector3 min = cell.Min;
            Vector3 max = cell.Max;

            return (new Vector3(min.x, min.y, min.z) - center).sqrMagnitude <= radiusSquared &&
                   (new Vector3(max.x, min.y, min.z) - center).sqrMagnitude <= radiusSquared &&
                   (new Vector3(min.x, max.y, min.z) - center).sqrMagnitude <= radiusSquared &&
                   (new Vector3(max.x, max.y, min.z) - center).sqrMagnitude <= radiusSquared &&
                   (new Vector3(min.x, min.y, max.z) - center).sqrMagnitude <= radiusSquared &&
                   (new Vector3(max.x, min.y, max.z) - center).sqrMagnitude <= radiusSquared &&
                   (new Vector3(min.x, max.y, max.z) - center).sqrMagnitude <= radiusSquared &&
                   (new Vector3(max.x, max.y, max.z) - center).sqrMagnitude <= radiusSquared;
        }
    }
}
