using System;
using UnityEngine;

namespace MarchingCubesPlanet.TrianglePools
{
    public sealed class PlanetTrianglePoolWriter
    {
        private readonly PlanetTrianglePoolController controller;
        private bool[] selectedSourceTriangles;
        private float[] triangleScores;
        private int[] triangleCosts;
        private int[] triangleBuckets;
        private int[] bucketTriangleCosts;

        public PlanetTrianglePoolWriter(PlanetTrianglePoolController controller)
        {
            this.controller = controller;
        }

        public PlanetTriangleDrawResult LastDrawResult { get; private set; }

        public bool IsSourceTriangleSelected(int sourceTriangleIndex)
        {
            return selectedSourceTriangles != null &&
                   sourceTriangleIndex >= 0 &&
                   sourceTriangleIndex < selectedSourceTriangles.Length &&
                   selectedSourceTriangles[sourceTriangleIndex];
        }

        public PlanetTriangleDrawResult Draw(
            int sourceTriangleCount,
            Func<int, float> scoreProvider,
            Func<int, int> triangleCostProvider,
            uint ownerId,
            uint meshId = 0u)
        {
            sourceTriangleCount = Math.Max(0, sourceTriangleCount);
            EnsureBuffers(sourceTriangleCount, controller.Budget.DistanceBucketCount);
            ClearSelection(sourceTriangleCount);
            int allocationId = controller.BeginAllocation(ownerId, meshId);

            if (sourceTriangleCount <= 0 || controller.TotalTriangleBudget <= 0)
            {
                LastDrawResult = new PlanetTriangleDrawResult(
                    controller.Budget.ArtistId,
                    ownerId,
                    0,
                    0,
                    0,
                    0,
                    0,
                    sourceTriangleCount,
                    0,
                    0f,
                    "Draw had no source triangles.");
                controller.ApplyDrawResult(LastDrawResult);
                return LastDrawResult;
            }

            controller.SetPriorityOriginWorld(PlanetTrianglePoolRegistry.PriorityOriginWorld);

            float minScore = float.MaxValue;
            float maxScore = float.MinValue;
            for (int triangleIndex = 0; triangleIndex < sourceTriangleCount; triangleIndex++)
            {
                float score = scoreProvider(triangleIndex);
                triangleScores[triangleIndex] = score;
                if (score < minScore)
                {
                    minScore = score;
                }

                if (score > maxScore)
                {
                    maxScore = score;
                }
            }

            ClearBucketCosts();
            int requestedTriangleCost = 0;
            for (int triangleIndex = 0; triangleIndex < sourceTriangleCount; triangleIndex++)
            {
                int cost = Math.Max(0, triangleCostProvider(triangleIndex));
                triangleCosts[triangleIndex] = cost;
                requestedTriangleCost += cost;
                int bucket = ScoreToBucket(
                    triangleScores[triangleIndex],
                    minScore,
                    maxScore);
                triangleBuckets[triangleIndex] = bucket;
                bucketTriangleCosts[bucket] += cost;
            }

            int selectedSourceTriangleCount = 0;
            int grantedTriangleCost = 0;
            int worstBucket = 0;
            float worstScore = 0f;
            int packageBucket = 0;
            float packageScore = minScore;
            if (!controller.TryReserveAllocation(
                    ownerId,
                    meshId,
                    allocationId,
                    requestedTriangleCost,
                    packageBucket,
                    packageScore,
                    Vector3.zero,
                    out int grantedTriangleCapacity,
                    out int reclaimedTriangleCost))
            {
                grantedTriangleCapacity = 0;
            }

            int remainingGrantedCapacity = grantedTriangleCapacity;
            for (int bucket = 0; bucket < bucketTriangleCosts.Length; bucket++)
            {
                if (bucketTriangleCosts[bucket] <= 0)
                {
                    continue;
                }

                for (int triangleIndex = 0; triangleIndex < sourceTriangleCount; triangleIndex++)
                {
                    if (triangleBuckets[triangleIndex] != bucket)
                    {
                        continue;
                    }

                    int cost = triangleCosts[triangleIndex];
                    if (cost <= 0)
                    {
                        continue;
                    }

                    if (cost > remainingGrantedCapacity)
                    {
                        continue;
                    }

                    float score = triangleScores[triangleIndex];
                    selectedSourceTriangles[triangleIndex] = true;
                    selectedSourceTriangleCount++;
                    grantedTriangleCost += cost;
                    remainingGrantedCapacity -= cost;
                    if (bucket >= worstBucket)
                    {
                        worstBucket = bucket;
                        worstScore = score;
                    }
                }
            }

            if (grantedTriangleCost > 0)
            {
                controller.SetAllocationTriangleCount(
                    ownerId,
                    meshId,
                    allocationId,
                    grantedTriangleCost);
                controller.UpdateAllocationPriority(
                    ownerId,
                    meshId,
                    allocationId,
                    worstBucket,
                    worstScore);
            }
            else
            {
                controller.SetAllocationTriangleCount(
                    ownerId,
                    meshId,
                    allocationId,
                    0);
            }

            int deniedTriangleCost = Math.Max(0, requestedTriangleCost - grantedTriangleCost);
            LastDrawResult = new PlanetTriangleDrawResult(
                controller.Budget.ArtistId,
                ownerId,
                requestedTriangleCost,
                grantedTriangleCost,
                deniedTriangleCost,
                reclaimedTriangleCost,
                selectedSourceTriangleCount,
                sourceTriangleCount,
                worstBucket,
                worstScore,
                "Draw requested=" + requestedTriangleCost +
                ", painted=" + grantedTriangleCost +
                ", denied=" + deniedTriangleCost +
                ", budget=" + controller.TotalTriangleBudget + ".");
            controller.ApplyDrawResult(LastDrawResult);
            return LastDrawResult;
        }

        private void EnsureBuffers(int sourceTriangleCount, int bucketCount)
        {
            if (selectedSourceTriangles == null || selectedSourceTriangles.Length < sourceTriangleCount)
            {
                selectedSourceTriangles = new bool[sourceTriangleCount];
            }

            if (triangleScores == null || triangleScores.Length < sourceTriangleCount)
            {
                triangleScores = new float[sourceTriangleCount];
            }

            if (triangleCosts == null || triangleCosts.Length < sourceTriangleCount)
            {
                triangleCosts = new int[sourceTriangleCount];
            }

            if (triangleBuckets == null || triangleBuckets.Length < sourceTriangleCount)
            {
                triangleBuckets = new int[sourceTriangleCount];
            }

            if (bucketTriangleCosts == null || bucketTriangleCosts.Length != bucketCount)
            {
                bucketTriangleCosts = new int[bucketCount];
            }
        }

        private void ClearSelection(int sourceTriangleCount)
        {
            for (int i = 0; i < sourceTriangleCount; i++)
            {
                selectedSourceTriangles[i] = false;
            }
        }

        private void ClearBucketCosts()
        {
            for (int i = 0; i < bucketTriangleCosts.Length; i++)
            {
                bucketTriangleCosts[i] = 0;
            }
        }

        private int ScoreToBucket(float score, float minScore, float maxScore)
        {
            if (bucketTriangleCosts.Length <= 1 || maxScore - minScore <= 0.0001f)
            {
                return 0;
            }

            float t = (score - minScore) / (maxScore - minScore);
            int bucket = (int)Math.Floor(t * bucketTriangleCosts.Length);
            return controller.ClampBucket(bucket);
        }
    }
}
