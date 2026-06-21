using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;

namespace MarchingCubesPlanet.VoxelEngine.Data
{
    [StructLayout(LayoutKind.Sequential)]
    public struct PlanetNoiseSettings
    {
        private const int CurveSampleCount = 16;

        public int enabled;
        public int seed;
        public int voronoiDivision;
        public int landCellCount;
        public float landMinElevation;
        public float landMaxElevation;
        public float oceanDepth;
        public float minimumOceanDepth;
        public float continentEdgeBlend;
        public float surfaceNoiseAmplitude;
        public float surfaceNoiseFrequency;
        public float seaLevelAtlasV;
        public float cellHeightModifierMin;
        public float cellHeightModifierMax;
        public float cellRoughnessModifierMin;
        public float cellRoughnessModifierMax;
        public uint landMask0;
        public uint landMask1;
        public uint landMask2;
        public uint landMask3;
        public uint landMask4;
        public uint landMask5;
        public uint landMask6;
        public uint landMask7;
        public float curve00;
        public float curve01;
        public float curve02;
        public float curve03;
        public float curve04;
        public float curve05;
        public float curve06;
        public float curve07;
        public float curve08;
        public float curve09;
        public float curve10;
        public float curve11;
        public float curve12;
        public float curve13;
        public float curve14;
        public float curve15;
        public float blendCurve00;
        public float blendCurve01;
        public float blendCurve02;
        public float blendCurve03;
        public float blendCurve04;
        public float blendCurve05;
        public float blendCurve06;
        public float blendCurve07;
        public float blendCurve08;
        public float blendCurve09;
        public float blendCurve10;
        public float blendCurve11;
        public float blendCurve12;
        public float blendCurve13;
        public float blendCurve14;
        public float blendCurve15;
        public FixedList4096Bytes<float3> voronoiCenters;
        public FixedList4096Bytes<float> landElevations;

        public static PlanetNoiseSettings Default
        {
            get
            {
                PlanetNoiseSettings settings = new PlanetNoiseSettings
                {
                    enabled = 0,
                    seed = 1,
                    voronoiDivision = 1,
                    landCellCount = 1,
                    landMinElevation = 0.015f,
                    landMaxElevation = 0.127f,
                    oceanDepth = 0.16f,
                    minimumOceanDepth = 0.03f,
                    continentEdgeBlend = 0.16f,
                    surfaceNoiseAmplitude = 0.308f,
                    surfaceNoiseFrequency = 2f,
                    seaLevelAtlasV = 0.337f,
                    cellHeightModifierMin = 1f,
                    cellHeightModifierMax = 1f,
                    cellRoughnessModifierMin = 1f,
                    cellRoughnessModifierMax = 1f
                };

                for (int i = 0; i < CurveSampleCount; i++)
                {
                    float t = i / (CurveSampleCount - 1f);
                    settings.SetCurveSample(i, t);
                    settings.SetBlendCurveSample(i, t);
                }

                settings.SetLandCell(0);
                return settings;
            }
        }

        public float SampleSurfaceOffset(float3 worldPosition, float3 sphereCenter, float referenceRadius)
        {
            float safeRadius = math.max(0.0001f, referenceRadius);
            float3 localPosition = worldPosition - sphereCenter;
            float3 direction = math.normalizesafe(localPosition, new float3(0f, 1f, 0f));

            FindClosestCells(direction, out int nearestIndex, out int secondIndex, out float nearestDot, out float secondDot);

            float nearestOffset = GetCellRadiusOffset(nearestIndex, safeRadius);
            float secondOffset = GetCellRadiusOffset(secondIndex, safeRadius);
            float interiorBlend = CalculateInteriorBlend(nearestDot - secondDot);
            float boundaryOffset = (nearestOffset + secondOffset) * 0.5f;
            float offset = math.lerp(boundaryOffset, nearestOffset, interiorBlend);

            int modifierCell = nearestIndex;
            float heightModifier = GetCellModifier(modifierCell, 0x85ebca6bu, cellHeightModifierMin, cellHeightModifierMax);
            float roughnessModifier = GetCellModifier(modifierCell, 0x9e3779b9u, cellRoughnessModifierMin, cellRoughnessModifierMax);
            offset *= heightModifier;

            if (surfaceNoiseAmplitude > 0f)
            {
                float3 normalizedPosition = localPosition / safeRadius;
                float noiseValue = noise.cnoise(normalizedPosition * math.max(0.01f, surfaceNoiseFrequency) * roughnessModifier);
                offset += noiseValue * safeRadius * surfaceNoiseAmplitude;
            }

            if (!IsLandCell(nearestIndex) && minimumOceanDepth > 0f)
            {
                offset = math.min(offset, -safeRadius * minimumOceanDepth);
            }

            return offset;
        }

        public float2 GetAtlasUv(float3 worldPosition, float3 sphereCenter, float referenceRadius)
        {
            float safeRadius = math.max(0.0001f, referenceRadius);
            float heightOffset = SampleSurfaceOffset(worldPosition, sphereCenter, safeRadius);
            float minHeightAtlasOffset = -safeRadius * math.max(oceanDepth, minimumOceanDepth) - safeRadius * surfaceNoiseAmplitude;
            float maxHeightAtlasOffset = safeRadius * landMaxElevation + safeRadius * surfaceNoiseAmplitude;

            if (heightOffset <= 0f)
            {
                float depthRange = math.max(0.0001f, -minHeightAtlasOffset);
                float underwaterHeight = math.saturate((heightOffset - minHeightAtlasOffset) / depthRange);
                return new float2(0.5f, math.lerp(0f, seaLevelAtlasV, underwaterHeight));
            }

            float landRange = math.max(0.0001f, maxHeightAtlasOffset);
            float landHeight = math.saturate(heightOffset / landRange);
            return new float2(0.5f, math.lerp(seaLevelAtlasV, 1f, landHeight));
        }

        public void FindClosestCells(float3 direction, out int nearestIndex, out int secondIndex, out float nearestDot, out float secondDot)
        {
            int count = math.max(1, voronoiDivision);
            nearestIndex = 0;
            secondIndex = 0;
            nearestDot = -2f;
            secondDot = -2f;

            for (int i = 0; i < count; i++)
            {
                float dot = math.dot(direction, GetVoronoiCellDirection(i));
                if (dot > nearestDot)
                {
                    secondIndex = nearestIndex;
                    secondDot = nearestDot;
                    nearestIndex = i;
                    nearestDot = dot;
                    continue;
                }

                if (dot > secondDot)
                {
                    secondIndex = i;
                    secondDot = dot;
                }
            }
        }

        public float3 GetVoronoiCellDirection(int index)
        {
            if (index >= 0 && index < voronoiCenters.Length)
            {
                return voronoiCenters[index];
            }

            float z = HashToUnit01(seed, index, 0x4f1bbcdd) * 2f - 1f;
            float angle = HashToUnit01(seed, index, 0x13a5ba1d) * math.PI * 2f;
            float horizontalRadius = math.sqrt(math.max(0f, 1f - z * z));
            return new float3(
                math.cos(angle) * horizontalRadius,
                z,
                math.sin(angle) * horizontalRadius);
        }

        public void ClearLandMask()
        {
            landMask0 = 0u;
            landMask1 = 0u;
            landMask2 = 0u;
            landMask3 = 0u;
            landMask4 = 0u;
            landMask5 = 0u;
            landMask6 = 0u;
            landMask7 = 0u;
        }

        public void SetLandCell(int cellIndex)
        {
            uint bit = 1u << (cellIndex & 31);
            switch ((cellIndex >> 5) & 7)
            {
                case 0: landMask0 |= bit; break;
                case 1: landMask1 |= bit; break;
                case 2: landMask2 |= bit; break;
                case 3: landMask3 |= bit; break;
                case 4: landMask4 |= bit; break;
                case 5: landMask5 |= bit; break;
                case 6: landMask6 |= bit; break;
                default: landMask7 |= bit; break;
            }
        }

        public bool IsLandCell(int cellIndex)
        {
            uint bit = 1u << (cellIndex & 31);
            switch ((cellIndex >> 5) & 7)
            {
                case 0: return (landMask0 & bit) != 0u;
                case 1: return (landMask1 & bit) != 0u;
                case 2: return (landMask2 & bit) != 0u;
                case 3: return (landMask3 & bit) != 0u;
                case 4: return (landMask4 & bit) != 0u;
                case 5: return (landMask5 & bit) != 0u;
                case 6: return (landMask6 & bit) != 0u;
                default: return (landMask7 & bit) != 0u;
            }
        }

        public void SetCurveSample(int index, float value)
        {
            switch (index)
            {
                case 0: curve00 = value; break;
                case 1: curve01 = value; break;
                case 2: curve02 = value; break;
                case 3: curve03 = value; break;
                case 4: curve04 = value; break;
                case 5: curve05 = value; break;
                case 6: curve06 = value; break;
                case 7: curve07 = value; break;
                case 8: curve08 = value; break;
                case 9: curve09 = value; break;
                case 10: curve10 = value; break;
                case 11: curve11 = value; break;
                case 12: curve12 = value; break;
                case 13: curve13 = value; break;
                case 14: curve14 = value; break;
                default: curve15 = value; break;
            }
        }

        public void SetBlendCurveSample(int index, float value)
        {
            switch (index)
            {
                case 0: blendCurve00 = value; break;
                case 1: blendCurve01 = value; break;
                case 2: blendCurve02 = value; break;
                case 3: blendCurve03 = value; break;
                case 4: blendCurve04 = value; break;
                case 5: blendCurve05 = value; break;
                case 6: blendCurve06 = value; break;
                case 7: blendCurve07 = value; break;
                case 8: blendCurve08 = value; break;
                case 9: blendCurve09 = value; break;
                case 10: blendCurve10 = value; break;
                case 11: blendCurve11 = value; break;
                case 12: blendCurve12 = value; break;
                case 13: blendCurve13 = value; break;
                case 14: blendCurve14 = value; break;
                default: blendCurve15 = value; break;
            }
        }

        public static uint Hash(uint value, uint salt)
        {
            uint hash = value + salt * 0x9e3779b9u;
            hash ^= hash >> 16;
            hash *= 0x7feb352du;
            hash ^= hash >> 15;
            hash *= 0x846ca68bu;
            hash ^= hash >> 16;
            return hash;
        }

        public static float HashToUnit01(int seed, int index, uint salt)
        {
            uint hash = Hash((uint)index ^ (uint)seed, salt);
            return (hash & 0x00ffffffu) / 16777215f;
        }

        private float GetCellRadiusOffset(int cellIndex, float referenceRadius)
        {
            return IsLandCell(cellIndex)
                ? referenceRadius * GetLandElevation(cellIndex)
                : -referenceRadius * math.max(0f, oceanDepth);
        }

        private float GetLandElevation(int cellIndex)
        {
            float t = HashToUnit01(seed, cellIndex, 0x26cb5d35u);
            t = EvaluateCurve(t, false);
            if (cellIndex >= 0 && cellIndex < landElevations.Length)
            {
                return landElevations[cellIndex];
            }

            return math.lerp(landMinElevation, landMaxElevation, t);
        }

        private float CalculateInteriorBlend(float dotDelta)
        {
            if (continentEdgeBlend <= 0.0001f)
            {
                return 1f;
            }

            float rawBlend = math.saturate(dotDelta / continentEdgeBlend);
            return math.saturate(EvaluateCurve(rawBlend, true));
        }

        private float EvaluateCurve(float t, bool blendCurve)
        {
            float scaled = math.saturate(t) * (CurveSampleCount - 1);
            int index = (int)math.floor(scaled);
            int nextIndex = math.min(index + 1, CurveSampleCount - 1);
            float localT = scaled - index;
            return math.lerp(GetCurveSample(index, blendCurve), GetCurveSample(nextIndex, blendCurve), localT);
        }

        private float GetCurveSample(int index, bool blendCurve)
        {
            if (blendCurve)
            {
                switch (index)
                {
                    case 0: return blendCurve00;
                    case 1: return blendCurve01;
                    case 2: return blendCurve02;
                    case 3: return blendCurve03;
                    case 4: return blendCurve04;
                    case 5: return blendCurve05;
                    case 6: return blendCurve06;
                    case 7: return blendCurve07;
                    case 8: return blendCurve08;
                    case 9: return blendCurve09;
                    case 10: return blendCurve10;
                    case 11: return blendCurve11;
                    case 12: return blendCurve12;
                    case 13: return blendCurve13;
                    case 14: return blendCurve14;
                    default: return blendCurve15;
                }
            }

            switch (index)
            {
                case 0: return curve00;
                case 1: return curve01;
                case 2: return curve02;
                case 3: return curve03;
                case 4: return curve04;
                case 5: return curve05;
                case 6: return curve06;
                case 7: return curve07;
                case 8: return curve08;
                case 9: return curve09;
                case 10: return curve10;
                case 11: return curve11;
                case 12: return curve12;
                case 13: return curve13;
                case 14: return curve14;
                default: return curve15;
            }
        }

        private float GetCellModifier(int cellIndex, uint salt, float min, float max)
        {
            return math.lerp(min, max, HashToUnit01(seed, cellIndex, salt));
        }
    }
}
