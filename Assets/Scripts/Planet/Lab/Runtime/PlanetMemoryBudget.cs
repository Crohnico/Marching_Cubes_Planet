using System;

namespace MarchingCubesPlanet.Lab
{
    [Serializable]
    public sealed class PlanetMemoryBudget
    {
        public string profileName = "Quest 3 Lab Defaults";
        public string targetPlatform = "Quest3";
        public int ownedCpuSoftMiB = 384;
        public int ownedCpuHardMiB = 512;
        public int ownedGpuSoftMiB = 384;
        public int ownedGpuHardMiB = 512;
        public int ownedCombinedSoftMiB = 768;
        public int ownedCombinedHardMiB = 1024;
        public int singleResourceSoftMiB = 128;
        public int singleResourceHardMiB = 256;
        public int liveGraphicsBuffersHard = 128;
        public int liveComputeBuffersHard = 64;
        public int liveRenderTexturesHard = 16;
        public int liveRuntimeMeshesHard = 128;
        public int liveRuntimeTexturesHard = 16;
        public int liveRuntimeMaterialsHard = 32;

        public long OwnedCpuSoftBytes => MiBToBytes(ownedCpuSoftMiB);
        public long OwnedCpuHardBytes => MiBToBytes(ownedCpuHardMiB);
        public long OwnedGpuSoftBytes => MiBToBytes(ownedGpuSoftMiB);
        public long OwnedGpuHardBytes => MiBToBytes(ownedGpuHardMiB);
        public long OwnedCombinedSoftBytes => MiBToBytes(ownedCombinedSoftMiB);
        public long OwnedCombinedHardBytes => MiBToBytes(ownedCombinedHardMiB);
        public long SingleResourceSoftBytes => MiBToBytes(singleResourceSoftMiB);
        public long SingleResourceHardBytes => MiBToBytes(singleResourceHardMiB);

        public static PlanetMemoryBudget CreateDefault()
        {
            return new PlanetMemoryBudget();
        }

        public PlanetMemoryBudget Clone()
        {
            return new PlanetMemoryBudget
            {
                profileName = profileName,
                targetPlatform = targetPlatform,
                ownedCpuSoftMiB = ownedCpuSoftMiB,
                ownedCpuHardMiB = ownedCpuHardMiB,
                ownedGpuSoftMiB = ownedGpuSoftMiB,
                ownedGpuHardMiB = ownedGpuHardMiB,
                ownedCombinedSoftMiB = ownedCombinedSoftMiB,
                ownedCombinedHardMiB = ownedCombinedHardMiB,
                singleResourceSoftMiB = singleResourceSoftMiB,
                singleResourceHardMiB = singleResourceHardMiB,
                liveGraphicsBuffersHard = liveGraphicsBuffersHard,
                liveComputeBuffersHard = liveComputeBuffersHard,
                liveRenderTexturesHard = liveRenderTexturesHard,
                liveRuntimeMeshesHard = liveRuntimeMeshesHard,
                liveRuntimeTexturesHard = liveRuntimeTexturesHard,
                liveRuntimeMaterialsHard = liveRuntimeMaterialsHard
            };
        }

        public bool IsValid(out string message)
        {
            if (ownedCpuSoftMiB <= 0 || ownedCpuHardMiB <= 0 ||
                ownedGpuSoftMiB <= 0 || ownedGpuHardMiB <= 0 ||
                ownedCombinedSoftMiB <= 0 || ownedCombinedHardMiB <= 0 ||
                singleResourceSoftMiB <= 0 || singleResourceHardMiB <= 0)
            {
                message = "All memory budgets must be greater than zero.";
                return false;
            }

            if (ownedCpuSoftMiB > ownedCpuHardMiB ||
                ownedGpuSoftMiB > ownedGpuHardMiB ||
                ownedCombinedSoftMiB > ownedCombinedHardMiB ||
                singleResourceSoftMiB > singleResourceHardMiB)
            {
                message = "Soft memory budgets must be lower than or equal to hard budgets.";
                return false;
            }

            if (liveGraphicsBuffersHard < 0 || liveComputeBuffersHard < 0 ||
                liveRenderTexturesHard < 0 || liveRuntimeMeshesHard < 0 ||
                liveRuntimeTexturesHard < 0 || liveRuntimeMaterialsHard < 0)
            {
                message = "Resource count hard limits cannot be negative.";
                return false;
            }

            message = "Budget is valid.";
            return true;
        }

        public static long MiBToBytes(int mib)
        {
            return (long)mib * 1024L * 1024L;
        }
    }
}
