using UnityEngine;

namespace MarchingCubesPlanet.TrianglePools
{
    public sealed class PlanetTrianglePoolController
    {
        private struct TriangleSlot
        {
            public uint ownerId;
            public int distanceBucket;
            public float score;
            public uint version;
            public Vector3 representativeWorldPosition;
            public bool occupied;
        }

        private PlanetTriangleBudget budget;
        private TriangleSlot[] slots;
        private int[] freelist;
        private int[] slotsByBucket;
        private int usedTriangleSlots;
        private int worstResidentBucket;
        private float worstResidentScore;
        private Vector3 priorityOriginWorld;
        private PlanetTrianglePoolMetrics metrics;
        private bool initialized;

        public PlanetTrianglePoolController(PlanetTriangleBudget initialBudget)
        {
            Initialize(initialBudget);
        }

        public PlanetTriangleBudget Budget => budget;
        public bool IsInitialized => initialized;
        public int TotalTriangleBudget => budget.TotalTriangleBudget;
        public int FreeTriangleSlots => Mathf.Max(0, budget.TotalTriangleBudget - usedTriangleSlots);
        public int UsedTriangleSlots => usedTriangleSlots;
        public Vector3 PriorityOriginWorld => priorityOriginWorld;
        public PlanetTrianglePoolMetrics Metrics => metrics;

        public void Initialize(PlanetTriangleBudget value)
        {
            budget = value.TotalTriangleBudget > 0 ? value : PlanetTriangleBudget.EnvironmentDefault();
            slots = new TriangleSlot[budget.TotalTriangleBudget];
            freelist = new int[budget.TotalTriangleBudget];
            slotsByBucket = new int[budget.DistanceBucketCount];
            priorityOriginWorld = Vector3.zero;
            initialized = true;
            ReleaseAllSlots();
        }

        public void SetTotalTriangleBudget(int totalTriangleBudget)
        {
            Initialize(budget.WithTotalTriangleBudget(totalTriangleBudget));
        }

        public void SetPriorityOriginWorld(Vector3 value)
        {
            priorityOriginWorld = value;
        }

        public void ReleaseAllSlots()
        {
            if (slots == null)
            {
                slots = new TriangleSlot[budget.TotalTriangleBudget];
            }

            if (freelist == null)
            {
                freelist = new int[budget.TotalTriangleBudget];
            }

            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].occupied = false;
                slots[i].ownerId = PlanetTriangleOwnerId.AnonymousValue;
                slots[i].distanceBucket = 0;
                slots[i].score = 0f;
                slots[i].version++;
                slots[i].representativeWorldPosition = Vector3.zero;
                freelist[i] = i;
            }

            ClearBucketCounts();
            usedTriangleSlots = 0;
            worstResidentBucket = 0;
            worstResidentScore = 0f;
            metrics = new PlanetTrianglePoolMetrics
            {
                artistId = budget.ArtistId,
                totalTriangleBudget = budget.TotalTriangleBudget,
                freeTriangleSlots = budget.TotalTriangleBudget,
                usedTriangleSlots = 0,
                distanceBucketCount = budget.DistanceBucketCount,
                worstResidentBucket = 0,
                worstResidentScore = 0f,
                lastDiagnostic = "Triangle pool released."
            };
        }

        public void ApplyDrawResult(PlanetTriangleDrawResult result)
        {
            usedTriangleSlots = Mathf.Clamp(result.GrantedTriangleCount, 0, budget.TotalTriangleBudget);
            worstResidentBucket = result.WorstResidentBucket;
            worstResidentScore = result.WorstResidentScore;

            metrics.artistId = budget.ArtistId;
            metrics.totalTriangleBudget = budget.TotalTriangleBudget;
            metrics.freeTriangleSlots = FreeTriangleSlots;
            metrics.usedTriangleSlots = usedTriangleSlots;
            metrics.requestedTriangleCount += result.RequestedTriangleCount;
            metrics.grantedTriangleCount += result.GrantedTriangleCount;
            metrics.deniedTriangleCount += result.DeniedTriangleCount;
            metrics.reclaimedTriangleCount += result.ReclaimedTriangleCount;
            metrics.allocationCount++;
            metrics.ownerCount = result.OwnerId == PlanetTriangleOwnerId.AnonymousValue ? 0 : 1;
            metrics.distanceBucketCount = budget.DistanceBucketCount;
            metrics.worstResidentBucket = worstResidentBucket;
            metrics.worstResidentScore = worstResidentScore;
            metrics.lastDiagnostic = result.Diagnostic;
        }

        public int ClampBucket(int bucket)
        {
            return Mathf.Clamp(bucket, 0, Mathf.Max(0, budget.DistanceBucketCount - 1));
        }

        private void ClearBucketCounts()
        {
            if (slotsByBucket == null)
            {
                slotsByBucket = new int[budget.DistanceBucketCount];
            }

            for (int i = 0; i < slotsByBucket.Length; i++)
            {
                slotsByBucket[i] = 0;
            }
        }
    }
}
