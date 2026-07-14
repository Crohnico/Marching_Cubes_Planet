using System;
using UnityEngine;

namespace MarchingCubesPlanet.MarchingCubes
{
    [Serializable]
    public struct PlanetMarchingCubesVertex
    {
        public const int Stride = 32;

        public Vector4 positionAndMaterial;
        public Vector4 normalAndDiagnostic;
    }

    public enum PlanetMaterialAppearanceState
    {
        Base = 0,
        Underwater = 1,
        Surface = 2
    }

    public static class PlanetMaterialVertexEncoding
    {
        public const int PackedMarker = 16384;
        private const int MaterialMask = 255;

        public static int DecodeMaterialId(float packedMaterial)
        {
            return Mathf.Max(0, Mathf.RoundToInt(packedMaterial)) & MaterialMask;
        }

        public static int DecodeLayerIndex(float packedMaterial)
        {
            int encoded = Mathf.Max(0, Mathf.RoundToInt(packedMaterial) - PackedMarker);
            return (encoded >> 8) & 15;
        }

        public static PlanetMaterialAppearanceState DecodeAppearanceState(float packedMaterial)
        {
            int encoded = Mathf.Max(0, Mathf.RoundToInt(packedMaterial) - PackedMarker);
            return (PlanetMaterialAppearanceState)((encoded >> 12) & 3);
        }
    }
}
