using System;

namespace MarchingCubesPlanet.TrianglePools
{
    [Serializable]
    public readonly struct PlanetTriangleArtistId : IEquatable<PlanetTriangleArtistId>
    {
        public const uint EnvironmentValue = 0u;
        public const uint ParticlesValue = 1u;

        public static readonly PlanetTriangleArtistId Environment = new PlanetTriangleArtistId(EnvironmentValue);
        public static readonly PlanetTriangleArtistId Particles = new PlanetTriangleArtistId(ParticlesValue);

        public PlanetTriangleArtistId(uint value)
        {
            Value = value;
        }

        public uint Value { get; }

        public bool Equals(PlanetTriangleArtistId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is PlanetTriangleArtistId other && Equals(other);
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
