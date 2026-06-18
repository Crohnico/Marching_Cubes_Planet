using UnityEngine;

namespace MarchingCubesPlanet.MarchingCubes
{
    public readonly struct MarchingCubeCorner
    {
        public MarchingCubeCorner(Vector3 position, float value)
        {
            Position = position;
            Value = value;
        }

        public Vector3 Position { get; }
        public float Value { get; }
    }
}
