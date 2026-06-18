using System;
using UnityEngine;

namespace MarchingCubesPlanet.Noise
{
    public sealed class SphericalVoronoiContinents
    {
        private readonly Vector3[] centers;
        private readonly bool[] landCells;

        public SphericalVoronoiContinents(int seed, int cellCount, int landCellCount)
        {
            int safeCellCount = Mathf.Max(1, cellCount);
            LandCellCount = Mathf.Clamp(landCellCount, 0, safeCellCount);

            centers = new Vector3[safeCellCount];
            landCells = new bool[safeCellCount];

            GenerateCenters(seed);
            SelectLandCells(seed);
        }

        public int CellCount => centers.Length;
        public int LandCellCount { get; }

        public ContinentSample Sample(
            Vector3 localPosition,
            float referenceRadius,
            float landElevation,
            float oceanDepth,
            float edgeBlend,
            AnimationCurve blendCurve)
        {
            Vector3 direction = localPosition.sqrMagnitude > 0.000001f
                ? localPosition.normalized
                : Vector3.up;

            FindClosestCells(direction, out int nearestIndex, out int secondIndex, out float nearestDot, out float secondDot);

            float nearestOffset = GetCellRadiusOffset(nearestIndex, referenceRadius, landElevation, oceanDepth);
            float secondOffset = GetCellRadiusOffset(secondIndex, referenceRadius, landElevation, oceanDepth);
            float interiorBlend = CalculateInteriorBlend(nearestDot - secondDot, edgeBlend, blendCurve);
            float boundaryOffset = (nearestOffset + secondOffset) * 0.5f;
            float radiusOffset = Mathf.Lerp(boundaryOffset, nearestOffset, interiorBlend);

            return new ContinentSample(
                radiusOffset,
                landCells[nearestIndex],
                nearestIndex,
                secondIndex,
                interiorBlend);
        }

        private void GenerateCenters(int seed)
        {
            System.Random random = new System.Random(MixSeed(seed, 0x4f1bbcdd));
            for (int i = 0; i < centers.Length; i++)
            {
                centers[i] = RandomUnitVector(random);
            }
        }

        private void SelectLandCells(int seed)
        {
            int[] indices = new int[centers.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = i;
            }

            System.Random random = new System.Random(MixSeed(seed, 0x13a5ba1d));
            for (int i = indices.Length - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (indices[i], indices[swapIndex]) = (indices[swapIndex], indices[i]);
            }

            for (int i = 0; i < LandCellCount; i++)
            {
                landCells[indices[i]] = true;
            }
        }

        private void FindClosestCells(Vector3 direction, out int nearestIndex, out int secondIndex, out float nearestDot, out float secondDot)
        {
            nearestIndex = 0;
            secondIndex = 0;
            nearestDot = float.NegativeInfinity;
            secondDot = float.NegativeInfinity;

            for (int i = 0; i < centers.Length; i++)
            {
                float dot = Vector3.Dot(direction, centers[i]);
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

            if (centers.Length == 1)
            {
                secondIndex = nearestIndex;
                secondDot = nearestDot;
            }
        }

        private float GetCellRadiusOffset(int cellIndex, float referenceRadius, float landElevation, float oceanDepth)
        {
            float safeRadius = Mathf.Max(0f, referenceRadius);
            return landCells[cellIndex]
                ? safeRadius * Mathf.Max(0f, landElevation)
                : -safeRadius * Mathf.Max(0f, oceanDepth);
        }

        private static float CalculateInteriorBlend(float dotDelta, float edgeBlend, AnimationCurve blendCurve)
        {
            if (edgeBlend <= 0.0001f)
            {
                return 1f;
            }

            float rawBlend = Mathf.Clamp01(dotDelta / edgeBlend);
            float curvedBlend = blendCurve != null && blendCurve.length > 0
                ? blendCurve.Evaluate(rawBlend)
                : Mathf.SmoothStep(0f, 1f, rawBlend);

            return Mathf.Clamp01(curvedBlend);
        }

        private static Vector3 RandomUnitVector(System.Random random)
        {
            float z = (float)(random.NextDouble() * 2.0 - 1.0);
            float angle = (float)(random.NextDouble() * Math.PI * 2.0);
            float horizontalRadius = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
            return new Vector3(
                Mathf.Cos(angle) * horizontalRadius,
                z,
                Mathf.Sin(angle) * horizontalRadius);
        }

        private static int MixSeed(int seed, int salt)
        {
            unchecked
            {
                int hash = seed;
                hash = (hash * 397) ^ salt;
                hash ^= hash << 13;
                hash ^= hash >> 17;
                hash ^= hash << 5;
                return hash;
            }
        }
    }

    public readonly struct ContinentSample
    {
        public ContinentSample(float radiusOffset, bool isLandCell, int cellIndex, int neighborCellIndex, float interiorBlend)
        {
            RadiusOffset = radiusOffset;
            IsLandCell = isLandCell;
            CellIndex = cellIndex;
            NeighborCellIndex = neighborCellIndex;
            InteriorBlend = interiorBlend;
        }

        public float RadiusOffset { get; }
        public bool IsLandCell { get; }
        public bool IsOceanCell => !IsLandCell;
        public int CellIndex { get; }
        public int NeighborCellIndex { get; }
        public float InteriorBlend { get; }
    }
}
