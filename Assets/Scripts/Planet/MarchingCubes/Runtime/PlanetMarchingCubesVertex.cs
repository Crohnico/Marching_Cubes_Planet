using System;
using UnityEngine;

namespace MarchingCubesPlanet.MarchingCubes
{
    [Serializable]
    public struct PlanetMarchingCubesVertex
    {
        public const int Stride = 32;

        public Vector4 positionAndCase;
        public Vector4 normalAndDiagnostic;
    }
}
