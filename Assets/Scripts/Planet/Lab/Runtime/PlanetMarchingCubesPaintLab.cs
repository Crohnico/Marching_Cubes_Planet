using System.Diagnostics;
using MarchingCubesPlanet.Coordinates;
using MarchingCubesPlanet.MarchingCubes;
using UnityEngine;

namespace MarchingCubesPlanet.Lab
{
    [DisallowMultipleComponent]
    public sealed class PlanetMarchingCubesPaintLab : PlanetLabModule
    {
        private const string OwnerName = "PlanetMarchingCubesPaintLab";

        [Header("References")]
        [SerializeField] private PlanetMarchingCubesLab marchingCubesLab;
        [SerializeField] private PlanetGpuShapeLab shapeLab;
        [SerializeField] private PlanetLabResourceRegistry resourceRegistry;
        [SerializeField] private Material materialOverride;

        [Header("Placement")]
        [SerializeField] private PlanetPlacement placement = PlanetPlacement.Default();

        [Header("Paint")]
        [SerializeField] private PlanetMarchingCubesPaintSettings settings = PlanetMarchingCubesPaintSettings.Default();

        [Header("State")]
        [SerializeField] private bool hasLiveMesh;
        [SerializeField] private int lastSourceTriangleCount;
        [SerializeField] private int lastPaintedTriangleCount;
        [SerializeField] private int lastPaintedVertexCount;
        [SerializeField] private long lastMeshEstimatedBytes;
        [SerializeField] private string lastAction;
        [SerializeField] private PlanetLabMetricsSnapshot lastSnapshot;
        [SerializeField] private PlanetLabDiagnostic lastDiagnostic;

        private readonly Stopwatch stopwatch = new Stopwatch();
        private readonly PlanetMarchingCubesMeshPainter painter = new PlanetMarchingCubesMeshPainter();

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MeshFilter activeMeshFilter;
        private MeshRenderer activeMeshRenderer;
        private int meshResourceId;
        private int materialResourceId;
        private int surfaceAtlasResourceId;

        public override string ModuleName => "Planet Marching Cubes Paint Lab";
        public override bool HasLiveResources => hasLiveMesh;

        public PlanetMarchingCubesPaintSettings Settings => settings;
        public PlanetLabDiagnostic LastDiagnostic => lastDiagnostic;
        public PlanetLabMetricsSnapshot LastSnapshot => lastSnapshot;
        public string LastAction => lastAction;
        public bool HasLiveMesh => hasLiveMesh;
        public int LastSourceTriangleCount => lastSourceTriangleCount;
        public int LastPaintedTriangleCount => lastPaintedTriangleCount;
        public int LastPaintedVertexCount => lastPaintedVertexCount;
        public long LastMeshEstimatedBytes => lastMeshEstimatedBytes;

        private void OnValidate()
        {
            if (settings.meshTriangleCapacity <= 0)
            {
                settings.meshTriangleCapacity = PlanetMarchingCubesPaintSettings.Default().meshTriangleCapacity;
            }
        }

        public override bool ValidateModule()
        {
            ResolveReferences();

            if (marchingCubesLab == null)
            {
                lastDiagnostic = PlanetLabDiagnostic.Critical(
                    "Marching Cubes Lab reference is missing",
                    "08-1 paints the CPU vertex result produced by 07.",
                    "Assign PlanetMarchingCubesLab from the same PlanetImplementationLab scene.",
                    "marchingCubesLab=null");
                lastAction = "Validate Marching Cubes Paint failed.";
                return false;
            }

            if (shapeLab == null)
            {
                lastDiagnostic = PlanetLabDiagnostic.Critical(
                    "Shape Lab reference is missing",
                    "08-1 needs the PlanetRecipe used by 06 to convert 07 grid vertices to world space.",
                    "Assign PlanetGpuShapeLab from the same PlanetImplementationLab scene.",
                    "shapeLab=null");
                lastAction = "Validate Marching Cubes Paint failed.";
                return false;
            }

            if (!shapeLab.Recipe.IsValid(out string recipeMessage))
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "PlanetRecipe is invalid",
                    recipeMessage,
                    "Fix PlanetGpuShapeLab recipe before painting the extracted mesh.",
                    "shapeLab.Recipe invalid");
                lastAction = "Validate Marching Cubes Paint failed.";
                return false;
            }

            if (!settings.Validate(out string settingsMessage))
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "Marching Cubes paint settings are invalid",
                    settingsMessage,
                    "Fix paint settings before painting the extracted mesh.",
                    settings.ToString());
                lastAction = "Validate Marching Cubes Paint failed.";
                return false;
            }

            PlanetMarchingCubesExtractionResult extraction = marchingCubesLab.LastResult;
            if (extraction == null || extraction.VertexCount <= 0)
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "No Marching Cubes extraction is available",
                    "08-1 only paints the latest 07 extraction result; it does not run the extraction itself.",
                    "Run Extract Cartesian Planet Surface in PlanetMarchingCubesLab, then Paint Last Extraction.",
                    "lastVertexCount=0");
                lastAction = "Validate Marching Cubes Paint failed.";
                return false;
            }

            lastDiagnostic = PlanetLabDiagnostic.Ok(
                "Marching Cubes paint setup is valid",
                "sourceTriangles=" + extraction.TriangleCount + "\n" + settings);
            lastAction = "Validate Marching Cubes Paint OK.";
            return true;
        }

        public override void InitModule()
        {
            PaintLastExtraction();
        }

        public void ResetPaintSettings()
        {
            ReleaseModule();
            settings = PlanetMarchingCubesPaintSettings.Default();
            lastDiagnostic = PlanetLabDiagnostic.Ok("Marching Cubes paint settings reset", settings.ToString());
            lastAction = "Marching Cubes paint settings reset.";
            CaptureMetrics("Reset Marching Cubes Paint Settings", 0);
        }

        public void CycleColorMode()
        {
            int nextValue = ((int)settings.colorMode + 1) % 6;
            settings.colorMode = (PlanetMarchingCubesPaintColorMode)nextValue;
            lastDiagnostic = PlanetLabDiagnostic.Ok("Marching Cubes paint color mode changed", settings.ToString());
            lastAction = "Cycle Paint Color Mode finished.";
        }

        public void UsePlanetSurfaceAtlas(int requiredTriangleCapacity)
        {
            settings.colorMode = PlanetMarchingCubesPaintColorMode.PlanetSurfaceAtlas;
            if (requiredTriangleCapacity > settings.meshTriangleCapacity)
            {
                settings.meshTriangleCapacity = requiredTriangleCapacity;
            }

            lastDiagnostic = PlanetLabDiagnostic.Ok("Marching Cubes paint configured for planet surface atlas", settings.ToString());
            lastAction = "Use Planet Surface Atlas finished.";
        }

        public void SetPlacement(PlanetPlacement value)
        {
            placement = value;
        }

        public void PaintLastExtraction()
        {
            EnsureRendererComponents();
            PaintLastExtraction(meshFilter, meshRenderer, placement);
        }

        public void PaintLastExtraction(MeshFilter targetMeshFilter, MeshRenderer targetMeshRenderer, PlanetPlacement targetPlacement)
        {
            stopwatch.Restart();
            ReleaseModule();
            stopwatch.Restart();

            if (!ValidateModule())
            {
                stopwatch.Stop();
                return;
            }

            if (targetMeshFilter == null)
            {
                lastDiagnostic = PlanetLabDiagnostic.Critical(
                    "Paint target MeshFilter is missing",
                    "08 needs a MeshFilter target to display the Marching Cubes result.",
                    "Pass the PlanetRecipePayloadPreview MeshFilter or assign the Lab MeshFilter.",
                    "targetMeshFilter=null");
                lastAction = "Paint Last Extraction failed.";
                stopwatch.Stop();
                return;
            }

            if (targetMeshRenderer == null)
            {
                lastDiagnostic = PlanetLabDiagnostic.Critical(
                    "Paint target MeshRenderer is missing",
                    "08 needs a MeshRenderer target to display the Marching Cubes result.",
                    "Pass the PlanetRecipePayloadPreview MeshRenderer or assign the Lab MeshRenderer.",
                    "targetMeshRenderer=null");
                lastAction = "Paint Last Extraction failed.";
                stopwatch.Stop();
                return;
            }

            activeMeshFilter = targetMeshFilter;
            activeMeshRenderer = targetMeshRenderer;
            placement = targetPlacement;
            PlanetMarchingCubesPaintResult result;
            try
            {
                result = painter.Paint(
                    activeMeshFilter,
                    activeMeshRenderer,
                    materialOverride,
                    marchingCubesLab.LastResult,
                    shapeLab.Recipe,
                    placement,
                    settings);
            }
            catch (System.Exception exception)
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "Marching Cubes mesh paint blocked",
                    exception.Message,
                    "Increase meshTriangleCapacity. 08 does not paint partial meshes.",
                    settings.ToString());
                lastAction = "Paint Last Extraction blocked.";
                stopwatch.Stop();
                CaptureMetrics("Paint Last Extraction Blocked", stopwatch.Elapsed.TotalMilliseconds);
                return;
            }

            ApplyResultSummary(result);
            RegisterRuntimeResources();

            stopwatch.Stop();
            lastDiagnostic = PlanetLabDiagnostic.Ok(
                "Marching Cubes mesh painted",
                BuildResultMetrics());
            lastAction = "Paint Last Extraction finished.";
            CaptureMetrics("Paint Last Extraction", stopwatch.Elapsed.TotalMilliseconds);
        }

        public override void RunModuleTest()
        {
            PaintLastExtraction();
        }

        public override void RunModuleStress()
        {
            PaintLastExtraction();
            ReleaseModule();
        }

        public override void ReleaseModule()
        {
            stopwatch.Restart();
            CacheRendererComponents();
            MeshFilter releaseMeshFilter = activeMeshFilter != null ? activeMeshFilter : meshFilter;
            MeshRenderer releaseMeshRenderer = activeMeshRenderer != null ? activeMeshRenderer : meshRenderer;
            painter.Release(releaseMeshFilter, releaseMeshRenderer);
            activeMeshFilter = null;
            activeMeshRenderer = null;
            MarkReleased(ref surfaceAtlasResourceId);
            MarkReleased(ref materialResourceId);
            MarkReleased(ref meshResourceId);
            hasLiveMesh = false;
            lastSourceTriangleCount = 0;
            lastPaintedTriangleCount = 0;
            lastPaintedVertexCount = 0;
            lastMeshEstimatedBytes = 0L;

            if (resourceRegistry != null)
            {
                resourceRegistry.RecalculateLiveTotals();
            }

            stopwatch.Stop();
            CaptureMetrics("Release Marching Cubes Paint", stopwatch.Elapsed.TotalMilliseconds);
        }

        public override PlanetLabMetricsSnapshot CaptureMetrics()
        {
            CaptureMetrics("Capture Marching Cubes Paint Metrics", 0);
            return lastSnapshot;
        }

        private void ApplyResultSummary(PlanetMarchingCubesPaintResult result)
        {
            hasLiveMesh = result.HasVisibleMesh;
            lastSourceTriangleCount = result.SourceTriangleCount;
            lastPaintedTriangleCount = result.PaintedTriangleCount;
            lastPaintedVertexCount = result.PaintedVertexCount;
            lastMeshEstimatedBytes = result.MeshEstimatedBytes;
        }

        private void RegisterRuntimeResources()
        {
            ResolveReferences();
            if (resourceRegistry == null)
            {
                return;
            }

            if (painter.RuntimeMesh != null)
            {
                meshResourceId = resourceRegistry.RegisterResource(
                    "PlanetMarchingCubesPaint Mesh",
                    PlanetLabResourceType.Mesh,
                    OwnerName,
                    lastMeshEstimatedBytes,
                    lastPaintedVertexCount,
                    0);
            }

            if (painter.OwnsRuntimeMaterial)
            {
                materialResourceId = resourceRegistry.RegisterResource(
                    "PlanetMarchingCubesPaint Material",
                    PlanetLabResourceType.RuntimeMaterial,
                    OwnerName,
                    1,
                    1,
                    0);
            }

            if (painter.OwnsRuntimeSurfaceAtlas)
            {
                surfaceAtlasResourceId = resourceRegistry.RegisterResource(
                    "PlanetMarchingCubesPaint Surface Atlas",
                    PlanetLabResourceType.RuntimeTexture,
                    OwnerName,
                    painter.RuntimeSurfaceAtlasEstimatedBytes,
                    painter.RuntimeSurfaceAtlasPixelCount,
                    4);
            }
        }

        private void CaptureMetrics(string operationName, double operationMs)
        {
            if (resourceRegistry != null)
            {
                resourceRegistry.RecalculateLiveTotals();
            }

            lastSnapshot = PlanetLabMetricsSnapshot.Capture(operationName, resourceRegistry, operationMs);
            if (string.IsNullOrEmpty(lastDiagnostic.title))
            {
                lastDiagnostic = lastSnapshot.diagnostic;
            }
        }

        private string BuildResultMetrics()
        {
            return "sourceTriangleCount=" + lastSourceTriangleCount +
                   "\npaintedTriangleCount=" + lastPaintedTriangleCount +
                   "\npaintedVertexCount=" + lastPaintedVertexCount +
                   "\nmeshEstimatedBytes=" + lastMeshEstimatedBytes +
                   "\n" + settings;
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

        private void ResolveReferences()
        {
            if (marchingCubesLab == null)
            {
                marchingCubesLab = FindFirstObjectByType<PlanetMarchingCubesLab>();
            }

            if (shapeLab == null)
            {
                shapeLab = FindFirstObjectByType<PlanetGpuShapeLab>();
            }

            if (resourceRegistry == null)
            {
                resourceRegistry = FindFirstObjectByType<PlanetLabResourceRegistry>();
            }
        }

        private void MarkReleased(ref int resourceId)
        {
            if (resourceRegistry != null && resourceId != 0)
            {
                resourceRegistry.MarkReleased(resourceId);
            }

            resourceId = 0;
        }

        private void OnDisable()
        {
            ReleaseModule();
        }
    }
}
