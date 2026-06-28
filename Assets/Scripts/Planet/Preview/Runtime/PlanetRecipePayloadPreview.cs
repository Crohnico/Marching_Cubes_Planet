using System;
using MarchingCubesPlanet.Coordinates;
using MarchingCubesPlanet.Lab;
using UnityEngine;

namespace MarchingCubesPlanet.Preview
{
    [DisallowMultipleComponent]
    public sealed class PlanetRecipePayloadPreview : MonoBehaviour
    {
        private const string VertexColorShaderName = "MarchingCubesPlanet/Debug/Vertex Color";
        private const string VertexColorMaterialResourcePath = "PlanetRecipePayloadPreview_VertexColorDebug";
        private const string UrpUnlitShaderName = "Universal Render Pipeline/Unlit";

        [Header("Recipe")]
        [SerializeField] private PlanetRecipe recipe = PlanetRecipe.Default();
        [SerializeField] private PlanetPlacement placement = PlanetPlacement.Default();

        [Header("Payload")]
        [SerializeField] private int requestedTrianglePayload = 126000;
        [SerializeField] private PlanetSpherePayloadColorMode colorMode = PlanetSpherePayloadColorMode.TrianglePalette;
        [SerializeField] private Material materialOverride;

        [Header("Memory Diagnostics")]
        [SerializeField] private PlanetLabResourceRegistry resourceRegistry;
        [SerializeField] private PlanetMemoryLab memoryLab;
        [SerializeField] private PlanetMemoryBudget memoryBudget = PlanetMemoryBudget.CreateDefault();
        [SerializeField] private PlanetMemorySnapshot beforeSnapshot;
        [SerializeField] private PlanetMemorySnapshot afterSnapshot;
        [SerializeField] private PlanetMemorySnapshotComparison snapshotComparison;
        [SerializeField, TextArea] private string snapshotComparisonSummary;

        [Header("Runtime State")]
        [SerializeField, HideInInspector] private int derivedGeodesicFrequency;
        [SerializeField, HideInInspector] private int derivedTriangleCount;
        [SerializeField, HideInInspector] private int derivedVertexCount;
        [SerializeField, HideInInspector] private int derivedIndexCount;
        [SerializeField, HideInInspector] private float derivedWorldRadius;
        [SerializeField, HideInInspector] private float derivedSurfaceRadius;
        [SerializeField, HideInInspector] private bool hasLiveMesh;
        [SerializeField, TextArea] private string lastDiagnostic;

        private Mesh runtimeMesh;
        private Material runtimeMaterial;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private int meshResourceId;
        private int materialResourceId;

        public PlanetRecipe Recipe => recipe;
        public PlanetPlacement Placement => placement;
        public float IsoLevel => recipe.IsoLevel;
        public int RequestedTrianglePayload => requestedTrianglePayload;
        public PlanetSpherePayloadColorMode ColorMode => colorMode;
        public int DerivedGeodesicFrequency => derivedGeodesicFrequency;
        public int DerivedTriangleCount => derivedTriangleCount;
        public int DerivedVertexCount => derivedVertexCount;
        public int DerivedIndexCount => derivedIndexCount;
        public float DerivedWorldRadius => derivedWorldRadius;
        public float DerivedSurfaceRadius => derivedSurfaceRadius;
        public Vector3 TransformPlanetWorldCenter => transform.position;
        public Quaternion TransformPlanetRotation => transform.rotation;
        public bool HasLiveMesh => hasLiveMesh;
        public string LastDiagnostic => lastDiagnostic;
        public PlanetMemorySnapshot BeforeSnapshot => beforeSnapshot;
        public PlanetMemorySnapshot AfterSnapshot => afterSnapshot;
        public PlanetMemorySnapshotComparison SnapshotComparison => snapshotComparison;
        public PlanetMemoryBudget MemoryBudget => ResolveMemoryBudget();
        public string SnapshotComparisonSummary => snapshotComparisonSummary;
        public bool HasResourceRegistry => ResolveResourceRegistry(false) != null;

        private void OnValidate()
        {
            requestedTrianglePayload = Mathf.Max(PlanetSpherePayloadMeshBuilder.BaseIcosahedronTriangleCount, requestedTrianglePayload);
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
            lastDiagnostic = "Demo recipe reset. PlanetPlacement was synced from Transform.position.";
        }

        public void SetRequestedTrianglePayload(int value)
        {
            requestedTrianglePayload = Mathf.Max(PlanetSpherePayloadMeshBuilder.BaseIcosahedronTriangleCount, value);
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
                Release();
                EnsureRendererComponents();

                runtimeMesh = new Mesh();
                PlanetSpherePayloadBuildResult result = PlanetSpherePayloadMeshBuilder.Build(
                    runtimeMesh,
                    in recipe,
                    derivedGeodesicFrequency,
                    colorMode);

                meshFilter.sharedMesh = runtimeMesh;
                meshRenderer.sharedMaterial = ResolveMaterial();

                transform.localScale = Vector3.one;

                derivedVertexCount = result.VertexCount;
                derivedTriangleCount = result.TriangleCount;
                derivedIndexCount = result.IndexCount;
                hasLiveMesh = true;
                RegisterRuntimeResources();
                lastDiagnostic = "Generated payload isosphere: requestedTriangles=" + requestedTrianglePayload +
                                 ", geodesicFrequency=" + derivedGeodesicFrequency +
                                 ", triangles=" + derivedTriangleCount +
                                 ", vertices=" + derivedVertexCount +
                                 ", colorMode=" + colorMode +
                                 ", WorldRadius=" + derivedWorldRadius +
                                 ", SurfaceRadius=" + derivedSurfaceRadius +
                                 ", IsoLevel=" + recipe.IsoLevel +
                                 ", center=" + placement.PlanetWorldCenter +
                                 ", rotation=" + placement.PlanetRotation.eulerAngles + ".";
            }
            catch (Exception exception)
            {
                Release();
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
            CacheRendererComponents();

            if (meshFilter != null && meshFilter.sharedMesh == runtimeMesh)
            {
                meshFilter.sharedMesh = null;
            }

            DestroyRuntimeObject(runtimeMesh);
            runtimeMesh = null;
            MarkReleased(ref meshResourceId);

            if (runtimeMaterial != null)
            {
                if (meshRenderer != null && meshRenderer.sharedMaterial == runtimeMaterial)
                {
                    meshRenderer.sharedMaterial = null;
                }

                DestroyRuntimeObject(runtimeMaterial);
                runtimeMaterial = null;
                MarkReleased(ref materialResourceId);
            }

            hasLiveMesh = false;
            RefreshDerivedValues();
            lastDiagnostic = "Payload preview released.";
        }

        private void RefreshDerivedValues()
        {
            SyncPlacementFromTransform();
            derivedWorldRadius = recipe.WorldRadius;
            derivedSurfaceRadius = PlanetSpherePayloadMeshBuilder.CalculateSurfaceRadius(in recipe);
            derivedGeodesicFrequency = PlanetSpherePayloadMeshBuilder.CalculateGeodesicFrequencyForPayload(requestedTrianglePayload);
            derivedTriangleCount = PlanetSpherePayloadMeshBuilder.CalculateTriangleCount(derivedGeodesicFrequency);
            derivedVertexCount = PlanetSpherePayloadMeshBuilder.CalculateVertexCount(derivedGeodesicFrequency, colorMode);
            derivedIndexCount = PlanetSpherePayloadMeshBuilder.CalculateIndexCount(derivedGeodesicFrequency);
        }

        private void SyncPlacementFromTransform()
        {
            placement.PlanetWorldCenter = transform.position;
            placement.PlanetRotation = transform.rotation;
        }

        private Material ResolveMaterial()
        {
            if (materialOverride != null)
            {
                return materialOverride;
            }

            if (colorMode != PlanetSpherePayloadColorMode.None)
            {
                Material vertexColorMaterial = Resources.Load<Material>(VertexColorMaterialResourcePath);
                if (vertexColorMaterial != null)
                {
                    return vertexColorMaterial;
                }
            }

            Shader shader = colorMode == PlanetSpherePayloadColorMode.None ? null : Shader.Find(VertexColorShaderName);
            if (shader == null)
            {
                shader = Shader.Find(UrpUnlitShaderName);
            }

            if (shader == null)
            {
                throw new InvalidOperationException("No debug material shader was found for PlanetRecipePayloadPreview.");
            }

            runtimeMaterial = new Material(shader)
            {
                name = "PlanetRecipePayloadPreview_Material_Runtime"
            };

            if (shader.name == UrpUnlitShaderName)
            {
                runtimeMaterial.color = Color.white;
            }

            return runtimeMaterial;
        }

        private void RegisterRuntimeResources()
        {
            PlanetLabResourceRegistry registry = ResolveResourceRegistry(true);
            if (registry == null)
            {
                return;
            }

            if (runtimeMesh != null)
            {
                meshResourceId = registry.RegisterResource(
                    "PlanetRecipePayloadPreview Mesh",
                    PlanetLabResourceType.Mesh,
                    "PlanetRecipePayloadPreview",
                    EstimateMeshBytes(),
                    derivedVertexCount,
                    0);
            }

            if (runtimeMaterial != null)
            {
                materialResourceId = registry.RegisterResource(
                    "PlanetRecipePayloadPreview Material",
                    PlanetLabResourceType.RuntimeMaterial,
                    "PlanetRecipePayloadPreview",
                    1,
                    1,
                    0);
            }
        }

        private long EstimateMeshBytes()
        {
            long vertexBytes = derivedVertexCount * 24L;
            if (colorMode != PlanetSpherePayloadColorMode.None)
            {
                vertexBytes += derivedVertexCount * 4L;
            }

            int indexStride = derivedVertexCount > ushort.MaxValue ? 4 : 2;
            long indexBytes = derivedIndexCount * (long)indexStride;
            return vertexBytes + indexBytes;
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
                lastDiagnostic = "Memory snapshot cannot see PayloadPreview resources: PlanetLabResourceRegistry was not found in the scene.";
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

        private void MarkReleased(ref int resourceId)
        {
            PlanetLabResourceRegistry registry = ResolveResourceRegistry(false);
            if (registry != null && resourceId != 0)
            {
                registry.MarkReleased(resourceId);
            }

            resourceId = 0;
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

        private static void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
