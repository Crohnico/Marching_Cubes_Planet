using System;

namespace MarchingCubesPlanet.TrianglePools
{
    [Serializable]
    public readonly struct PlanetTriangleOwnerId : IEquatable<PlanetTriangleOwnerId>
    {
        public const uint AnonymousValue = 0u;
        public const uint PlanetSurfaceValue = 1u;
        public const uint PlanetWaterValue = 2u;

        public static readonly PlanetTriangleOwnerId Anonymous = new PlanetTriangleOwnerId(AnonymousValue);
        public static readonly PlanetTriangleOwnerId PlanetSurface = new PlanetTriangleOwnerId(PlanetSurfaceValue);
        public static readonly PlanetTriangleOwnerId PlanetWater = new PlanetTriangleOwnerId(PlanetWaterValue);

        public PlanetTriangleOwnerId(uint value)
        {
            Value = value;
        }

        public uint Value { get; }

        public bool Equals(PlanetTriangleOwnerId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is PlanetTriangleOwnerId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (int)Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
