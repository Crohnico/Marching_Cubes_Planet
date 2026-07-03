using System;
using MarchingCubesPlanet.Coordinates;
using MarchingCubesPlanet.Lab;
using MarchingCubesPlanet.TrianglePools;
using UnityEngine;

namespace MarchingCubesPlanet.Preview
{
    [DisallowMultipleComponent]
    public sealed class PlanetRecipePayloadPreview : MonoBehaviour
    {
        [Header("Recipe")]
        [SerializeField] private PlanetRecipe recipe = PlanetRecipe.Default();
        [SerializeField] private PlanetPlacement placement = PlanetPlacement.Default();

        [Header("09 Environment Budget")]
        [SerializeField] private int requestedTrianglePayload = 126000;

        [Header("Memory Diagnostics")]
        [SerializeField] private PlanetLabResourceRegistry resourceRegistry;
        [SerializeField] private PlanetMemoryLab memoryLab;
        [SerializeField] private PlanetMemoryBudget memoryBudget = PlanetMemoryBudget.CreateDefault();
        [SerializeField] private PlanetMemorySnapshot beforeSnapshot;
        [SerializeField] private PlanetMemorySnapshot afterSnapshot;
        [SerializeField] private PlanetMemorySnapshotComparison snapshotComparison;
        [SerializeField, TextArea] private string snapshotComparisonSummary;

        [Header("Runtime State")]
        [SerializeField, HideInInspector] private float derivedWorldRadius;
        [SerializeField, TextArea] private string lastDiagnostic;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;

        public PlanetRecipe Recipe => recipe;
        public PlanetPlacement Placement => placement;
        public float IsoLevel => recipe.IsoLevel;
        public int RequestedTrianglePayload => requestedTrianglePayload;
        public float DerivedWorldRadius => derivedWorldRadius;
        public Vector3 TransformPlanetWorldCenter => transform.position;
        public Quaternion TransformPlanetRotation => transform.rotation;

        public bool HasLiveMesh
        {
            get
            {
                CacheRendererComponents();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    return true;
                }

                PlanetMarchingCubesPaintLab paintLab = FindFirstObjectByType<PlanetMarchingCubesPaintLab>();
                return paintLab != null && paintLab.HasLiveMesh;
            }
        }

        public string LastDiagnostic => lastDiagnostic;
        public PlanetMemorySnapshot BeforeSnapshot => beforeSnapshot;
        public PlanetMemorySnapshot AfterSnapshot => afterSnapshot;
        public PlanetMemorySnapshotComparison SnapshotComparison => snapshotComparison;
        public PlanetMemoryBudget MemoryBudget => ResolveMemoryBudget();
        public string SnapshotComparisonSummary => snapshotComparisonSummary;
        public bool HasResourceRegistry => ResolveResourceRegistry(false) != null;

        private void OnValidate()
        {
            requestedTrianglePayload = Mathf.Max(1, requestedTrianglePayload);
            RefreshDerivedValues();
        }

        private void Start()
        {
            SyncPlacementFromTransform();
            RefreshDerivedValues();
        }

        private void OnDestroy()
        {
            Release();
        }

        public void ApplyPayload126k()
        {
            SetRequestedTrianglePayload(126000);
        }

        public void ApplyPayload250k()
        {
            SetRequestedTrianglePayload(250000);
        }

        public void ApplyPayload500k()
        {
            SetRequestedTrianglePayload(500000);
        }

        public void ApplyPayload1M()
        {
            SetRequestedTrianglePayload(1000000);
        }

        public void ApplyPayload2M()
        {
            SetRequestedTrianglePayload(2000000);
        }

        public void ApplyPayload5M()
        {
            SetRequestedTrianglePayload(5000000);
        }

        public void ResetDemoRecipe()
        {
            recipe = PlanetRecipe.Default();
            SyncPlacementFromTransform();
            SetRequestedTrianglePayload(126000);
            lastDiagnostic = "Demo recipe reset for managed 09 generation. PlanetPlacement was synced from Transform.position.";
        }

        public void SetRequestedTrianglePayload(int value)
        {
            requestedTrianglePayload = Mathf.Max(1, value);
            RefreshDerivedValues();
        }

        public void Generate()
        {
            SyncPlacementFromTransform();
            RefreshDerivedValues();

            if (!PlanetRecipeValidator.Validate(in recipe, out string recipeMessage))
            {
                lastDiagnostic = "Generate blocked: " + recipeMessage;
                return;
            }

            try
            {
                PlanetRecipePayloadPreviewGenerationFlow flow = ResolveGenerationFlow();
                flow.GenerateFromPreview(this, ResolvePriorityOriginWorld());
                lastDiagnostic = flow.LastDiagnostic;
            }
            catch (Exception exception)
            {
                PlanetRecipePayloadPreviewGenerationFlow flow = FindFirstObjectByType<PlanetRecipePayloadPreviewGenerationFlow>();
                if (flow != null)
                {
                    flow.Release();
                }

                lastDiagnostic = "Generate failed: " + exception.Message;
                throw;
            }
        }

        public void CaptureBeforeSnapshot()
        {
            beforeSnapshot = CaptureMemorySnapshot("PayloadPreview Before Snapshot");
            snapshotComparisonSummary = "Before snapshot captured. Generate or Release, then capture After Snapshot.";
        }

        public void CaptureAfterSnapshot()
        {
            afterSnapshot = CaptureMemorySnapshot("PayloadPreview After Snapshot");
            snapshotComparison = PlanetMemorySnapshotComparison.Compare(
                "PayloadPreview Before vs After",
                beforeSnapshot,
                afterSnapshot,
                ResolveMemoryBudget(),
                false);
            snapshotComparisonSummary = BuildComparisonSummary(snapshotComparison, ResolveMemoryBudget());
        }

        public void EnsureRenderTargets(out MeshFilter targetMeshFilter, out MeshRenderer targetMeshRenderer)
        {
            EnsureRendererComponents();
            targetMeshFilter = meshFilter;
            targetMeshRenderer = meshRenderer;
        }

        public void Release()
        {
            PlanetRecipePayloadPreviewGenerationFlow flow = FindFirstObjectByType<PlanetRecipePayloadPreviewGenerationFlow>();
            if (flow != null)
            {
                flow.Release();
            }

            RefreshDerivedValues();
            lastDiagnostic = "Managed 09 preview released.";
        }

        private PlanetRecipePayloadPreviewGenerationFlow ResolveGenerationFlow()
        {
            PlanetRecipePayloadPreviewGenerationFlow flow = FindFirstObjectByType<PlanetRecipePayloadPreviewGenerationFlow>();
            if (flow != null)
            {
                return flow;
            }

            return gameObject.AddComponent<PlanetRecipePayloadPreviewGenerationFlow>();
        }

        private Vector3 ResolvePriorityOriginWorld()
        {
            if (PlanetTrianglePoolRegistry.HasPlayerViewData)
            {
                return PlanetTrianglePoolRegistry.PlayerPositionWorld;
            }

            PlanetMinimalXrRig rig = FindFirstObjectByType<PlanetMinimalXrRig>();
            if (rig != null && rig.Head != null)
            {
                return rig.Head.position;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                return mainCamera.transform.position;
            }

            return transform.position;
        }

        private void RefreshDerivedValues()
        {
            SyncPlacementFromTransform();
            derivedWorldRadius = recipe.WorldRadius;
        }

        private void SyncPlacementFromTransform()
        {
            placement.PlanetWorldCenter = transform.position;
            placement.PlanetRotation = transform.rotation;
        }

        private PlanetMemorySnapshot CaptureMemorySnapshot(string operationName)
        {
            return PlanetMemorySnapshot.Capture(
                operationName,
                ResolveResourceRegistry(true),
                ResolveMemoryBudget(),
                null,
                0,
                false);
        }

        private PlanetMemoryBudget ResolveMemoryBudget()
        {
            ResolveMemoryLab();
            if (memoryLab != null && memoryLab.RuntimeBudget != null)
            {
                return memoryLab.RuntimeBudget;
            }

            if (memoryBudget == null)
            {
                memoryBudget = PlanetMemoryBudget.CreateDefault();
            }

            return memoryBudget;
        }

        private PlanetLabResourceRegistry ResolveResourceRegistry(bool updateDiagnostic)
        {
            if (resourceRegistry != null)
            {
                return resourceRegistry;
            }

            ResolveMemoryLab();
            if (memoryLab != null)
            {
                resourceRegistry = memoryLab.GetComponentInParent<PlanetLabResourceRegistry>();
            }

            if (resourceRegistry == null)
            {
                resourceRegistry = FindFirstObjectByType<PlanetLabResourceRegistry>();
            }

            if (resourceRegistry == null && updateDiagnostic)
            {
                lastDiagnostic = "Memory snapshot cannot see managed 09 preview resources: PlanetLabResourceRegistry was not found in the scene.";
            }

            return resourceRegistry;
        }

        private void ResolveMemoryLab()
        {
            if (memoryLab == null)
            {
                memoryLab = FindFirstObjectByType<PlanetMemoryLab>();
            }
        }

        private static string BuildComparisonSummary(PlanetMemorySnapshotComparison comparison, PlanetMemoryBudget budget)
        {
            PlanetMemorySnapshot after = comparison.after;
            PlanetLabDiagnostic diagnostic = comparison.diagnostic;

            return "Status: " + diagnostic.severity +
                   "\nIssue: " + diagnostic.title +
                   "\nCPU delta: " + FormatBytes(comparison.ownedCpuDeltaBytes) +
                   "\nGPU delta: " + FormatBytes(comparison.ownedGpuDeltaBytes) +
                   "\nCombined delta: " + FormatBytes(comparison.ownedCombinedDeltaBytes) +
                   "\nLive resource delta: " + comparison.liveResourceDelta +
                   "\nAfter owned CPU: " + FormatBytes(after.ownedCpuEstimatedBytes) +
                   "\nAfter owned GPU: " + FormatBytes(after.ownedGpuEstimatedBytes) +
                   "\nGPU soft budget used: " + FormatBudgetPercent(after.ownedGpuEstimatedBytes, budget.OwnedGpuSoftBytes) +
                   "\nGPU hard budget used: " + FormatBudgetPercent(after.ownedGpuEstimatedBytes, budget.OwnedGpuHardBytes) +
                   "\nCombined soft budget used: " + FormatBudgetPercent(after.ownedCombinedEstimatedBytes, budget.OwnedCombinedSoftBytes) +
                   "\nCombined hard budget used: " + FormatBudgetPercent(after.ownedCombinedEstimatedBytes, budget.OwnedCombinedHardBytes) +
                   "\nAfter live resources: " + after.liveResourceCount +
                   "\nAfter runtime meshes: " + after.liveRuntimeMeshes +
                   "\nLargest resource: " + after.largestSingleResourceName +
                   "\nLargest resource bytes: " + FormatBytes(after.largestSingleResourceBytes) +
                   "\nMeaning: " + diagnostic.probableCause +
                   "\nAction: " + diagnostic.recommendedAction;
        }

        private static string FormatBudgetPercent(long bytes, long budgetBytes)
        {
            if (budgetBytes <= 0)
            {
                return "unavailable";
            }

            double percent = bytes * 100.0 / budgetBytes;
            return percent.ToString("0.0") + "% of " + FormatBytes(budgetBytes);
        }

        private static string FormatBytes(long bytes)
        {
            double mib = bytes / (1024.0 * 1024.0);
            return bytes + " bytes (" + mib.ToString("0.00") + " MiB)";
        }

        private void EnsureRendererComponents()
        {
            CacheRendererComponents();

            if (meshFilter == null)
            {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            if (meshRenderer == null)
            {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }
        }

        private void CacheRendererComponents()
        {
            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
            }

            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
            }
        }
    }
}
