using System;
using UnityEngine;

namespace MarchingCubesPlanet.Coordinates
{
    [Serializable]
    public struct UniversePosition : IEquatable<UniversePosition>
    {
        [SerializeField] private double x;
        [SerializeField] private double y;
        [SerializeField] private double z;

        public UniversePosition(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public double X
        {
            get => x;
            set => x = value;
        }

        public double Y
        {
            get => y;
            set => y = value;
        }

        public double Z
        {
            get => z;
            set => z = value;
        }

        public static UniversePosition Zero => new UniversePosition(0d, 0d, 0d);

        public static UniversePosition FromVector3(Vector3 value)
        {
            return new UniversePosition(value.x, value.y, value.z);
        }

        public Vector3 ToVector3()
        {
            return new Vector3((float)x, (float)y, (float)z);
        }

        public static UniversePosition operator +(UniversePosition position, Vector3 localOffset)
        {
            return new UniversePosition(
                position.x + localOffset.x,
                position.y + localOffset.y,
                position.z + localOffset.z);
        }

        public static Vector3 operator -(UniversePosition a, UniversePosition b)
        {
            return new Vector3(
                (float)(a.x - b.x),
                (float)(a.y - b.y),
                (float)(a.z - b.z));
        }

        public bool Equals(UniversePosition other)
        {
            return x.Equals(other.x) && y.Equals(other.y) && z.Equals(other.z);
        }

        public override bool Equals(object obj)
        {
            return obj is UniversePosition other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = x.GetHashCode();
                hashCode = (hashCode * 397) ^ y.GetHashCode();
                hashCode = (hashCode * 397) ^ z.GetHashCode();
                return hashCode;
            }
        }

        public override string ToString()
        {
            return "(" + x + ", " + y + ", " + z + ")";
        }
    }
}
