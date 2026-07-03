using UnityEngine;

namespace MarchingCubesPlanet.TrianglePools
{
    public sealed class PlanetTrianglePoolController
    {
        private const int InitialAllocationCapacity = 128;

        private struct TriangleAllocation
        {
            public uint ownerId;
            public uint meshId;
            public int allocationId;
            public int triangleCount;
            public int priorityBucket;
            public float score;
            public uint version;
            public Vector3 representativeWorldPosition;
            public bool occupied;
        }

        private PlanetTriangleBudget budget;
        private TriangleAllocation[] allocations;
        private uint[] ownerScratch;
        private int allocationCapacity;
        private int usedTriangleSlots;
        private int allocationCount;
        private int ownerCount;
        private int worstResidentBucket;
        private float worstResidentScore;
        private Vector3 priorityOriginWorld;
        private PlanetTrianglePoolMetrics metrics;
        private int nextAllocationId;
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
            allocations = null;
            ownerScratch = null;
            allocationCapacity = 0;
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
            allocations = null;
            ownerScratch = null;
            allocationCapacity = 0;
            usedTriangleSlots = 0;
            allocationCount = 0;
            ownerCount = 0;
            worstResidentBucket = 0;
            worstResidentScore = 0f;
            nextAllocationId = 0;
            metrics = new PlanetTrianglePoolMetrics
            {
                artistId = budget.ArtistId,
                totalTriangleBudget = budget.TotalTriangleBudget,
                freeTriangleSlots = budget.TotalTriangleBudget,
                usedTriangleSlots = 0,
                allocationCount = 0,
                ownerCount = 0,
                distanceBucketCount = budget.DistanceBucketCount,
                worstResidentBucket = 0,
                worstResidentScore = 0f,
                lastDiagnostic = "Triangle pool released."
            };
        }

        public int BeginAllocation(uint ownerId, uint meshId)
        {
            ReleasePublication(ownerId, meshId);
            nextAllocationId++;
            if (nextAllocationId <= 0)
            {
                nextAllocationId = 1;
            }

            return nextAllocationId;
        }

        public bool TryReserveAllocation(
            uint ownerId,
            uint meshId,
            int allocationId,
            int requestedTriangleCount,
            int priorityBucket,
            float score,
            Vector3 representativeWorldPosition,
            out int grantedTriangleCount,
            out int reclaimedTriangleCount)
        {
            requestedTriangleCount = Mathf.Max(0, requestedTriangleCount);
            priorityBucket = ClampBucket(priorityBucket);
            grantedTriangleCount = 0;
            reclaimedTriangleCount = 0;

            if (requestedTriangleCount <= 0 || budget.TotalTriangleBudget <= 0)
            {
                return false;
            }

            int missingForFullGrant = Mathf.Max(0, requestedTriangleCount - FreeTriangleSlots);
            if (missingForFullGrant > 0)
            {
                reclaimedTriangleCount = ReclaimWorseAllocations(score, missingForFullGrant);
            }

            grantedTriangleCount = Mathf.Min(requestedTriangleCount, FreeTriangleSlots);
            if (grantedTriangleCount <= 0)
            {
                RefreshResidentSummary();
                return false;
            }

            EnsureAllocationCapacity();
            int index = FindFreeAllocationIndex();
            TriangleAllocation allocation = allocations[index];
            allocation.ownerId = ownerId;
            allocation.meshId = meshId;
            allocation.allocationId = allocationId;
            allocation.triangleCount = grantedTriangleCount;
            allocation.priorityBucket = priorityBucket;
            allocation.score = score;
            allocation.version++;
            allocation.representativeWorldPosition = representativeWorldPosition;
            allocation.occupied = true;
            allocations[index] = allocation;

            usedTriangleSlots += grantedTriangleCount;
            allocationCount++;
            RefreshResidentSummary();
            return true;
        }

        public void UpdateAllocationPriority(
            uint ownerId,
            uint meshId,
            int allocationId,
            int priorityBucket,
            float score)
        {
            if (allocations == null)
            {
                return;
            }

            priorityBucket = ClampBucket(priorityBucket);
            for (int i = 0; i < allocationCapacity; i++)
            {
                if (!allocations[i].occupied ||
                    allocations[i].ownerId != ownerId ||
                    allocations[i].meshId != meshId ||
                    allocations[i].allocationId != allocationId)
                {
                    continue;
                }

                allocations[i].priorityBucket = priorityBucket;
                allocations[i].score = score;
                allocations[i].version++;
                RefreshResidentSummary();
                return;
            }
        }

        public void SetAllocationTriangleCount(
            uint ownerId,
            uint meshId,
            int allocationId,
            int triangleCount)
        {
            if (allocations == null)
            {
                return;
            }

            triangleCount = Mathf.Max(0, triangleCount);
            for (int i = 0; i < allocationCapacity; i++)
            {
                if (!allocations[i].occupied ||
                    allocations[i].ownerId != ownerId ||
                    allocations[i].meshId != meshId ||
                    allocations[i].allocationId != allocationId)
                {
                    continue;
                }

                if (triangleCount == 0)
                {
                    ReleaseAllocationAt(i);
                }
                else
                {
                    int delta = triangleCount - allocations[i].triangleCount;
                    allocations[i].triangleCount = triangleCount;
                    allocations[i].version++;
                    usedTriangleSlots = Mathf.Clamp(
                        usedTriangleSlots + delta,
                        0,
                        budget.TotalTriangleBudget);
                }

                RefreshResidentSummary();
                return;
            }
        }

        public int ReleaseOwner(uint ownerId)
        {
            if (allocations == null)
            {
                return 0;
            }

            int released = 0;
            for (int i = 0; i < allocationCapacity; i++)
            {
                if (allocations[i].occupied && allocations[i].ownerId == ownerId)
                {
                    released += ReleaseAllocationAt(i);
                }
            }

            RefreshResidentSummary();
            RefreshMetricOccupancy();
            return released;
        }

        public int ReleasePublication(uint ownerId, uint meshId)
        {
            if (allocations == null)
            {
                return 0;
            }

            int released = 0;
            for (int i = 0; i < allocationCapacity; i++)
            {
                if (allocations[i].occupied &&
                    allocations[i].ownerId == ownerId &&
                    allocations[i].meshId == meshId)
                {
                    released += ReleaseAllocationAt(i);
                }
            }

            RefreshResidentSummary();
            RefreshMetricOccupancy();
            return released;
        }

        public void ApplyDrawResult(PlanetTriangleDrawResult result)
        {
            RefreshResidentSummary();

            metrics.artistId = budget.ArtistId;
            metrics.totalTriangleBudget = budget.TotalTriangleBudget;
            metrics.freeTriangleSlots = FreeTriangleSlots;
            metrics.usedTriangleSlots = usedTriangleSlots;
            metrics.requestedTriangleCount += result.RequestedTriangleCount;
            metrics.grantedTriangleCount += result.GrantedTriangleCount;
            metrics.deniedTriangleCount += result.DeniedTriangleCount;
            metrics.reclaimedTriangleCount += result.ReclaimedTriangleCount;
            metrics.allocationCount = allocationCount;
            metrics.ownerCount = ownerCount;
            metrics.distanceBucketCount = budget.DistanceBucketCount;
            metrics.worstResidentBucket = worstResidentBucket;
            metrics.worstResidentScore = worstResidentScore;
            metrics.lastDiagnostic = result.Diagnostic;
        }

        public int ClampBucket(int bucket)
        {
            return Mathf.Clamp(bucket, 0, Mathf.Max(0, budget.DistanceBucketCount - 1));
        }

        private int ReclaimWorseAllocations(float score, int desiredTriangleCount)
        {
            int reclaimed = 0;
            while (reclaimed < desiredTriangleCount)
            {
                int worstIndex = FindWorstReclaimableAllocation(score);
                if (worstIndex < 0)
                {
                    break;
                }

                reclaimed += ReleaseAllocationAt(worstIndex);
            }

            return reclaimed;
        }

        private int FindWorstReclaimableAllocation(float score)
        {
            if (allocations == null)
            {
                return -1;
            }

            int worstIndex = -1;
            int worstBucket = -1;
            float worstScore = float.MinValue;
            for (int i = 0; i < allocationCapacity; i++)
            {
                if (!allocations[i].occupied || allocations[i].score <= score)
                {
                    continue;
                }

                if (worstIndex < 0 ||
                    allocations[i].priorityBucket > worstBucket ||
                    allocations[i].priorityBucket == worstBucket && allocations[i].score > worstScore)
                {
                    worstIndex = i;
                    worstBucket = allocations[i].priorityBucket;
                    worstScore = allocations[i].score;
                }
            }

            return worstIndex;
        }

        private int ReleaseAllocationAt(int index)
        {
            TriangleAllocation allocation = allocations[index];
            if (!allocation.occupied)
            {
                return 0;
            }

            int releasedTriangles = Mathf.Max(0, allocation.triangleCount);
            allocation.ownerId = PlanetTriangleOwnerId.AnonymousValue;
            allocation.meshId = 0u;
            allocation.allocationId = 0;
            allocation.triangleCount = 0;
            allocation.priorityBucket = 0;
            allocation.score = 0f;
            allocation.version++;
            allocation.representativeWorldPosition = Vector3.zero;
            allocation.occupied = false;
            allocations[index] = allocation;

            usedTriangleSlots = Mathf.Max(0, usedTriangleSlots - releasedTriangles);
            allocationCount = Mathf.Max(0, allocationCount - 1);
            return releasedTriangles;
        }

        private void EnsureAllocationCapacity()
        {
            if (allocations != null && FindFreeAllocationIndex() >= 0)
            {
                return;
            }

            int newCapacity = allocations == null
                ? InitialAllocationCapacity
                : Mathf.Max(InitialAllocationCapacity, allocations.Length * 2);
            TriangleAllocation[] newAllocations = new TriangleAllocation[newCapacity];
            if (allocations != null)
            {
                for (int i = 0; i < allocations.Length; i++)
                {
                    newAllocations[i] = allocations[i];
                }
            }

            allocations = newAllocations;
            if (ownerScratch == null || ownerScratch.Length != Mathf.Max(1, budget.MaxTrackedOwners))
            {
                ownerScratch = new uint[Mathf.Max(1, budget.MaxTrackedOwners)];
            }

            allocationCapacity = newCapacity;
        }

        private int FindFreeAllocationIndex()
        {
            if (allocations == null)
            {
                return -1;
            }

            for (int i = 0; i < allocationCapacity; i++)
            {
                if (!allocations[i].occupied)
                {
                    return i;
                }
            }

            return -1;
        }

        private void RefreshResidentSummary()
        {
            ownerCount = 0;
            worstResidentBucket = 0;
            worstResidentScore = 0f;
            bool foundWorst = false;

            if (allocations == null)
            {
                return;
            }

            int ownerScratchCount = 0;
            if (ownerScratch != null)
            {
                for (int i = 0; i < ownerScratch.Length; i++)
                {
                    ownerScratch[i] = PlanetTriangleOwnerId.AnonymousValue;
                }
            }

            for (int i = 0; i < allocationCapacity; i++)
            {
                if (!allocations[i].occupied)
                {
                    continue;
                }

                if (!foundWorst ||
                    allocations[i].priorityBucket > worstResidentBucket ||
                    allocations[i].priorityBucket == worstResidentBucket && allocations[i].score > worstResidentScore)
                {
                    worstResidentBucket = allocations[i].priorityBucket;
                    worstResidentScore = allocations[i].score;
                    foundWorst = true;
                }

                if (allocations[i].ownerId == PlanetTriangleOwnerId.AnonymousValue)
                {
                    continue;
                }

                bool ownerFound = false;
                for (int ownerIndex = 0; ownerIndex < ownerScratchCount; ownerIndex++)
                {
                    if (ownerScratch[ownerIndex] == allocations[i].ownerId)
                    {
                        ownerFound = true;
                        break;
                    }
                }

                if (!ownerFound && ownerScratchCount < ownerScratch.Length)
                {
                    ownerScratch[ownerScratchCount++] = allocations[i].ownerId;
                }
            }

            ownerCount = ownerScratchCount;
        }

        private void RefreshMetricOccupancy()
        {
            metrics.freeTriangleSlots = FreeTriangleSlots;
            metrics.usedTriangleSlots = usedTriangleSlots;
            metrics.allocationCount = allocationCount;
            metrics.ownerCount = ownerCount;
            metrics.worstResidentBucket = worstResidentBucket;
            metrics.worstResidentScore = worstResidentScore;
        }
    }
}
