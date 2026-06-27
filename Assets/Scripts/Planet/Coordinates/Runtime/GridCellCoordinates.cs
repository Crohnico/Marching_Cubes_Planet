using System;
using UnityEngine;

namespace MarchingCubesPlanet.Coordinates
{
    [Serializable]
    public readonly struct GridCellCoordinates : IEquatable<GridCellCoordinates>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public GridCellCoordinates(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Vector3 Min => new Vector3(X, Y, Z);
        public Vector3 Center => new Vector3(X + 0.5f, Y + 0.5f, Z + 0.5f);
        public Vector3 Max => new Vector3(X + 1f, Y + 1f, Z + 1f);

        public static GridCellCoordinates FromGridPosition(Vector3 gridPosition)
        {
            return new GridCellCoordinates(
                Mathf.FloorToInt(gridPosition.x),
                Mathf.FloorToInt(gridPosition.y),
                Mathf.FloorToInt(gridPosition.z));
        }

        public bool Equals(GridCellCoordinates other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is GridCellCoordinates other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = X;
                hashCode = (hashCode * 397) ^ Y;
                hashCode = (hashCode * 397) ^ Z;
                return hashCode;
            }
        }

        public override string ToString()
        {
            return "(" + X + ", " + Y + ", " + Z + ")";
        }
    }
}
