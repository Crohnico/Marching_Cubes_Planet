using System.Text;

namespace MarchingCubesPlanet.Lab
{
    public static class PlanetMemoryDiagnostics
    {
        public static PlanetLabDiagnostic Evaluate(
            PlanetMemorySnapshot snapshot,
            PlanetMemoryBudget budget,
            bool requireCleanRelease)
        {
            string metrics = BuildMetrics(snapshot, budget);

            if (requireCleanRelease && snapshot.liveResourceCount > 0)
            {
                return PlanetLabDiagnostic.Critical(
                    "Release left live registered resources",
                    "A release validation expected the registry to be clean, but resources are still alive.",
                    "Inspect resources by owner and verify that each Release path marks its handles as released.",
                    metrics);
            }

            if (budget != null)
            {
                if (snapshot.ownedCpuEstimatedBytes > budget.OwnedCpuHardBytes ||
                    snapshot.ownedGpuEstimatedBytes > budget.OwnedGpuHardBytes ||
                    snapshot.ownedCombinedEstimatedBytes > budget.OwnedCombinedHardBytes ||
                    snapshot.largestSingleResourceBytes > budget.SingleResourceHardBytes ||
                    snapshot.liveGraphicsBuffers > budget.liveGraphicsBuffersHard ||
                    snapshot.liveComputeBuffers > budget.liveComputeBuffersHard ||
                    snapshot.liveRenderTextures > budget.liveRenderTexturesHard ||
                    snapshot.liveRuntimeMeshes > budget.liveRuntimeMeshesHard ||
                    snapshot.liveRuntimeTextures > budget.liveRuntimeTexturesHard ||
                    snapshot.liveRuntimeMaterials > budget.liveRuntimeMaterialsHard)
                {
                    return PlanetLabDiagnostic.Critical(
                        "Memory hard budget exceeded",
                        "At least one owned estimate or live resource count is over its hard budget.",
                        "Stop growing this test, release resources, and lower the payload or document a deliberate budget change.",
                        metrics);
                }

                if (snapshot.ownedCpuEstimatedBytes > budget.OwnedCpuSoftBytes ||
                    snapshot.ownedGpuEstimatedBytes > budget.OwnedGpuSoftBytes ||
                    snapshot.ownedCombinedEstimatedBytes > budget.OwnedCombinedSoftBytes ||
                    snapshot.largestSingleResourceBytes > budget.SingleResourceSoftBytes)
                {
                    return PlanetLabDiagnostic.Warning(
                        "Memory soft budget exceeded",
                        "The operation is still below hard limits, but it crossed a warning threshold.",
                        "Keep the result visible and decide whether this payload should degrade or remain manual-only.",
                        metrics);
                }
            }

            if (snapshot.unityGcAllocFrameBytes > 0 || snapshot.unityGcAllocFrameCount > 0)
            {
                return PlanetLabDiagnostic.Warning(
                    "GC allocation detected",
                    "Unity reported managed allocations in the captured frame.",
                    "Check whether the allocation happened in a hot path or only in this manual diagnostic action.",
                    metrics);
            }

            return PlanetLabDiagnostic.Ok("Memory snapshot is within budget", metrics);
        }

        public static PlanetLabDiagnostic EvaluateComparison(
            PlanetMemorySnapshotComparison comparison,
            PlanetMemoryBudget budget,
            bool requireCleanRelease)
        {
            if (requireCleanRelease && comparison.after.liveResourceCount > 0)
            {
                return PlanetLabDiagnostic.Critical(
                    "Snapshot comparison found live resources after release",
                    "The after snapshot still contains registered resources.",
                    "Inspect resources by owner and fix the module Release path before continuing.",
                    BuildComparisonMetrics(comparison));
            }

            if (comparison.ownedCpuDeltaBytes > 0 || comparison.ownedGpuDeltaBytes > 0)
            {
                return PlanetLabDiagnostic.Warning(
                    "Owned memory increased between snapshots",
                    "The after snapshot owns more registered memory than the before snapshot.",
                    "Confirm whether this growth is expected. If this comparison follows Release All, treat it as a leak.",
                    BuildComparisonMetrics(comparison));
            }

            PlanetLabDiagnostic afterDiagnostic = Evaluate(comparison.after, budget, requireCleanRelease);
            if (afterDiagnostic.severity != PlanetLabDiagnosticSeverity.OK)
            {
                return afterDiagnostic;
            }

            return PlanetLabDiagnostic.Ok(
                "Snapshot comparison is clean",
                BuildComparisonMetrics(comparison));
        }

        private static string BuildMetrics(PlanetMemorySnapshot snapshot, PlanetMemoryBudget budget)
        {
            StringBuilder builder = new StringBuilder(512);
            builder.Append("Operation: ").Append(snapshot.operationName);
            builder.Append("\nOwned CPU bytes: ").Append(snapshot.ownedCpuEstimatedBytes);
            builder.Append("\nOwned GPU bytes: ").Append(snapshot.ownedGpuEstimatedBytes);
            builder.Append("\nOwned combined bytes: ").Append(snapshot.ownedCombinedEstimatedBytes);
            builder.Append("\nLive resources: ").Append(snapshot.liveResourceCount);
            builder.Append("\nLargest resource bytes: ").Append(snapshot.largestSingleResourceBytes);
            builder.Append("\nLargest resource: ").Append(snapshot.largestSingleResourceName);
            builder.Append("\nLargest owner: ").Append(snapshot.largestSingleResourceOwner);
            builder.Append("\nGraphics buffers: ").Append(snapshot.liveGraphicsBuffers);
            builder.Append("\nCompute buffers: ").Append(snapshot.liveComputeBuffers);
            builder.Append("\nRender textures: ").Append(snapshot.liveRenderTextures);
            builder.Append("\nRuntime meshes: ").Append(snapshot.liveRuntimeMeshes);
            builder.Append("\nRuntime textures: ").Append(snapshot.liveRuntimeTextures);
            builder.Append("\nRuntime materials: ").Append(snapshot.liveRuntimeMaterials);
            builder.Append("\nGC allocated frame bytes: ").Append(snapshot.unityGcAllocFrameBytes);
            builder.Append("\nGC allocation frame count: ").Append(snapshot.unityGcAllocFrameCount);

            if (budget != null)
            {
                builder.Append("\nBudget profile: ").Append(budget.profileName);
                builder.Append("\nCPU soft/hard bytes: ").Append(budget.OwnedCpuSoftBytes).Append('/').Append(budget.OwnedCpuHardBytes);
                builder.Append("\nGPU soft/hard bytes: ").Append(budget.OwnedGpuSoftBytes).Append('/').Append(budget.OwnedGpuHardBytes);
                builder.Append("\nCombined soft/hard bytes: ").Append(budget.OwnedCombinedSoftBytes).Append('/').Append(budget.OwnedCombinedHardBytes);
            }

            return builder.ToString();
        }

        private static string BuildComparisonMetrics(PlanetMemorySnapshotComparison comparison)
        {
            return "Comparison: " + comparison.comparisonName +
                   "\nBefore: " + comparison.before.operationName +
                   "\nAfter: " + comparison.after.operationName +
                   "\nOwned CPU delta bytes: " + comparison.ownedCpuDeltaBytes +
                   "\nOwned GPU delta bytes: " + comparison.ownedGpuDeltaBytes +
                   "\nOwned combined delta bytes: " + comparison.ownedCombinedDeltaBytes +
                   "\nLive resource delta: " + comparison.liveResourceDelta +
                   "\nAfter live resources: " + comparison.after.liveResourceCount;
        }
    }
}
