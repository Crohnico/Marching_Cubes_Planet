using System;

namespace MarchingCubesPlanet.TrianglePools
{
    public sealed class PlanetTrianglePoolWriter
    {
        private readonly PlanetTrianglePoolController controller;
        private bool[] selectedSourceTriangles;
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
            uint ownerId)
        {
            sourceTriangleCount = Math.Max(0, sourceTriangleCount);
            EnsureBuffers(sourceTriangleCount, controller.Budget.DistanceBucketCount);
            ClearSelection(sourceTriangleCount);

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
                requestedTriangleCost += cost;
                int bucket = ScoreToBucket(
                    scoreProvider(triangleIndex),
                    minScore,
                    maxScore);
                bucketTriangleCosts[bucket] += cost;
            }

            int budgetRemaining = Math.Min(controller.TotalTriangleBudget, requestedTriangleCost);
            int highestFullBucket = -1;
            int partialBucket = -1;
            int partialBucketRemaining = 0;
            for (int bucket = 0; bucket < bucketTriangleCosts.Length; bucket++)
            {
                int bucketCost = bucketTriangleCosts[bucket];
                if (bucketCost <= budgetRemaining)
                {
                    budgetRemaining -= bucketCost;
                    highestFullBucket = bucket;
                    continue;
                }

                partialBucket = bucket;
                partialBucketRemaining = budgetRemaining;
                break;
            }

            int selectedSourceTriangleCount = 0;
            int grantedTriangleCost = 0;
            int worstBucket = 0;
            float worstScore = 0f;
            for (int triangleIndex = 0; triangleIndex < sourceTriangleCount; triangleIndex++)
            {
                float score = scoreProvider(triangleIndex);
                int bucket = ScoreToBucket(score, minScore, maxScore);
                int cost = Math.Max(0, triangleCostProvider(triangleIndex));
                bool selected = bucket <= highestFullBucket;
                if (!selected && bucket == partialBucket && cost <= partialBucketRemaining)
                {
                    selected = true;
                    partialBucketRemaining -= cost;
                }

                if (!selected)
                {
                    continue;
                }

                selectedSourceTriangles[triangleIndex] = true;
                selectedSourceTriangleCount++;
                grantedTriangleCost += cost;
                if (bucket >= worstBucket)
                {
                    worstBucket = bucket;
                    worstScore = score;
                }
            }

            int deniedTriangleCost = Math.Max(0, requestedTriangleCost - grantedTriangleCost);
            int reclaimedTriangleCost = Math.Max(0, grantedTriangleCost - controller.FreeTriangleSlots);
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
