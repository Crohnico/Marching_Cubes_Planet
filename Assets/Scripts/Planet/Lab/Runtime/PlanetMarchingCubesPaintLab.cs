using System.Diagnostics;
using System.Collections.Generic;
using MarchingCubesPlanet.Coordinates;
using MarchingCubesPlanet.MarchingCubes;
using MarchingCubesPlanet.TrianglePools;
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
        [SerializeField] private int lastWaterTriangleCount;
        [SerializeField] private int lastWaterVertexCount;
        [SerializeField] private int lastPaintedChunkCount;
        [SerializeField] private PlanetChunkCachePayloadMode lastChunkCachePayloadMode;
        [SerializeField] private int lastChunkCacheRequestedChunkCount;
        [SerializeField] private int lastChunkCacheLoadedChunkCount;
        [SerializeField] private int lastChunkCacheMeshOnlyLoadCount;
        [SerializeField] private int lastChunkCacheChunkDataLoadCount;
        [SerializeField] private int lastChunkCacheMissingChunkDataCount;
        [SerializeField] private PlanetChunkWorkPackageMode lastChunkWorkPackageMode;
        [SerializeField] private int lastChunkWorkPackageSize;
        [SerializeField] private int lastChunkWorkPackageCount;
        [SerializeField] private bool lastChunkWorkImmediateShell;
        [SerializeField] private long lastMeshEstimatedBytes;
        [SerializeField] private long lastWaterMeshEstimatedBytes;
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
        private int waterMeshResourceId;
        private int chunkMeshesResourceId;
        private int chunkWaterMeshesResourceId;
        private int materialResourceId;
        private int waterMaterialResourceId;
        private int surfaceAtlasResourceId;

        public override string ModuleName => "Planet Marching Cubes Paint Lab";
        public override bool HasLiveResources => hasLiveMesh;

        public PlanetMarchingCubesPaintSettings Settings => settings;
        public PlanetLabDiagnostic LastDiagnostic => lastDiagnostic;
        public PlanetLabMetricsSnapshot LastSnapshot => lastSnapshot;
        public string LastAction => lastAction;
        public bool HasLiveMesh => hasLiveMesh;
        public int RuntimeTerrainVertexCount => painter.RuntimeMesh != null
            ? painter.RuntimeMesh.vertexCount + painter.RuntimeChunkVertexCount
            : painter.RuntimeChunkVertexCount;
        public int LastSourceTriangleCount => lastSourceTriangleCount;
        public int LastPaintedTriangleCount => lastPaintedTriangleCount;
        public int LastPaintedVertexCount => lastPaintedVertexCount;
        public int LastWaterTriangleCount => lastWaterTriangleCount;
        public int LastWaterVertexCount => lastWaterVertexCount;
        public int LastPaintedChunkCount => lastPaintedChunkCount;
        public PlanetChunkCachePayloadMode LastChunkCachePayloadMode => lastChunkCachePayloadMode;
        public int LastChunkCacheRequestedChunkCount => lastChunkCacheRequestedChunkCount;
        public int LastChunkCacheLoadedChunkCount => lastChunkCacheLoadedChunkCount;
        public int LastChunkCacheMeshOnlyLoadCount => lastChunkCacheMeshOnlyLoadCount;
        public int LastChunkCacheChunkDataLoadCount => lastChunkCacheChunkDataLoadCount;
        public int LastChunkCacheMissingChunkDataCount => lastChunkCacheMissingChunkDataCount;
        public PlanetChunkWorkPackageMode LastChunkWorkPackageMode => lastChunkWorkPackageMode;
        public int LastChunkWorkPackageSize => lastChunkWorkPackageSize;
        public int LastChunkWorkPackageCount => lastChunkWorkPackageCount;
        public bool LastChunkWorkImmediateShell => lastChunkWorkImmediateShell;
        public long LastMeshEstimatedBytes => lastMeshEstimatedBytes;
        public long LastWaterMeshEstimatedBytes => lastWaterMeshEstimatedBytes;

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
            PaintLastExtractionInternal(targetMeshFilter, targetMeshRenderer, targetPlacement, false);
        }

        public void PaintLastExtractionByChunks()
        {
            EnsureRendererComponents();
            PaintLastExtractionByChunks(meshFilter, meshRenderer, placement);
        }

        public void PaintLastExtractionByChunks(MeshFilter targetMeshFilter, MeshRenderer targetMeshRenderer, PlanetPlacement targetPlacement)
        {
            PaintLastExtractionInternal(targetMeshFilter, targetMeshRenderer, targetPlacement, true);
        }

        public bool PaintCachedChunks(
            List<PlanetCachedChunkMesh> chunks,
            PlanetChunkCacheLoadSummary cacheLoadSummary,
            int lod,
            MeshFilter targetMeshFilter,
            MeshRenderer targetMeshRenderer,
            PlanetPlacement targetPlacement,
            in PlanetRecipe recipe)
        {
            stopwatch.Restart();
            ReleaseModule();
            stopwatch.Restart();

            if (chunks == null || chunks.Count <= 0)
            {
                ApplyChunkCacheLoadSummary(cacheLoadSummary);
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "Cached chunk list is empty",
                    "09 can only paint meshes that 10 has already loaded.",
                    "Load chunk meshes in 10 before calling PaintCachedChunks.",
                    "chunks=0");
                lastAction = "Paint Cached Chunks failed.";
                stopwatch.Stop();
                CaptureMetrics("Paint Cached Chunks Empty", stopwatch.Elapsed.TotalMilliseconds);
                return false;
            }

            if (targetMeshFilter == null)
            {
                lastDiagnostic = PlanetLabDiagnostic.Critical(
                    "Paint target MeshFilter is missing",
                    "09 needs a MeshFilter target to display cached chunks.",
                    "Pass the PlanetRecipePayloadPreview MeshFilter or assign the Lab MeshFilter.",
                    "targetMeshFilter=null");
                lastAction = "Paint Cached Chunks failed.";
                stopwatch.Stop();
                return false;
            }

            if (targetMeshRenderer == null)
            {
                lastDiagnostic = PlanetLabDiagnostic.Critical(
                    "Paint target MeshRenderer is missing",
                    "09 needs a MeshRenderer target to display cached chunks.",
                    "Pass the PlanetRecipePayloadPreview MeshRenderer or assign the Lab MeshRenderer.",
                    "targetMeshRenderer=null");
                lastAction = "Paint Cached Chunks failed.";
                stopwatch.Stop();
                return false;
            }

            if (!recipe.IsValid(out string recipeMessage))
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "PlanetRecipe is invalid",
                    recipeMessage,
                    "Fix the preview recipe before painting cached chunks.",
                    "recipe invalid");
                lastAction = "Paint Cached Chunks failed.";
                stopwatch.Stop();
                return false;
            }

            if (!settings.Validate(out string settingsMessage))
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "Marching Cubes paint settings are invalid",
                    settingsMessage,
                    "Fix paint settings before painting cached chunks.",
                    settings.ToString());
                lastAction = "Paint Cached Chunks failed.";
                stopwatch.Stop();
                return false;
            }

            ApplyChunkCacheLoadSummary(cacheLoadSummary);
            ApplyChunkWorkPackageSummary(PlanetChunkWorkPackagePlanner.BuildSummary(lod, cacheLoadSummary.LoadedChunkCount));
            activeMeshFilter = targetMeshFilter;
            activeMeshRenderer = targetMeshRenderer;
            placement = targetPlacement;

            try
            {
                PlanetMarchingCubesPaintResult result = painter.PaintCachedChunks(
                    activeMeshFilter,
                    activeMeshRenderer,
                    materialOverride,
                    chunks,
                    in recipe,
                    settings);
                ApplyResultSummary(result);
                RegisterRuntimeResources();
            }
            catch (System.Exception exception)
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "Cached chunk paint blocked",
                    exception.Message,
                    "Delete the chunk cache for this planet or regenerate it from 06 -> 07.",
                    "LOD=" + lod);
                lastAction = "Paint Cached Chunks blocked.";
                stopwatch.Stop();
                CaptureMetrics("Paint Cached Chunks Blocked", stopwatch.Elapsed.TotalMilliseconds);
                return false;
            }

            stopwatch.Stop();
            lastDiagnostic = PlanetLabDiagnostic.Ok(
                "Cached chunk meshes painted",
                BuildResultMetrics() + "\nLOD=" + lod);
            lastAction = "Paint Cached Chunks finished.";
            CaptureMetrics("Paint Cached Chunks", stopwatch.Elapsed.TotalMilliseconds);
            return hasLiveMesh;
        }

        public int SaveLiveChunksToCache(PlanetChunkMeshCache cache, int lod)
        {
            if (cache == null || !hasLiveMesh)
            {
                return 0;
            }

            int savedChunkCount = painter.SaveRuntimeChunksToCache(cache, lod);
            ApplyChunkWorkPackageSummary(PlanetChunkWorkPackagePlanner.BuildSummary(lod, painter.RuntimeChunkCount));
            lastAction = "Save Live Chunks To Cache finished.";
            return savedChunkCount;
        }

        public bool PaintNamedMesh(
            string meshId,
            Mesh surfaceMesh,
            Mesh waterMesh,
            MeshFilter targetMeshFilter,
            MeshRenderer targetMeshRenderer,
            PlanetPlacement targetPlacement,
            in PlanetRecipe recipe,
            int cacheChunkId = -1)
        {
            stopwatch.Restart();

            if (targetMeshFilter == null)
            {
                lastDiagnostic = PlanetLabDiagnostic.Critical(
                    "Paint target MeshFilter is missing",
                    "09 needs a MeshFilter target to display a named mesh.",
                    "Pass the PlanetRecipePayloadPreview MeshFilter.",
                    "targetMeshFilter=null");
                lastAction = "Paint Named Mesh failed.";
                stopwatch.Stop();
                return false;
            }

            if (targetMeshRenderer == null)
            {
                lastDiagnostic = PlanetLabDiagnostic.Critical(
                    "Paint target MeshRenderer is missing",
                    "09 needs a MeshRenderer target to display a named mesh.",
                    "Pass the PlanetRecipePayloadPreview MeshRenderer.",
                    "targetMeshRenderer=null");
                lastAction = "Paint Named Mesh failed.";
                stopwatch.Stop();
                return false;
            }

            if (!recipe.IsValid(out string recipeMessage))
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "PlanetRecipe is invalid",
                    recipeMessage,
                    "Fix the preview recipe before painting a named mesh.",
                    "recipe invalid");
                lastAction = "Paint Named Mesh failed.";
                stopwatch.Stop();
                return false;
            }

            if (!settings.Validate(out string settingsMessage))
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "Marching Cubes paint settings are invalid",
                    settingsMessage,
                    "Fix paint settings before painting a named mesh.",
                    settings.ToString());
                lastAction = "Paint Named Mesh failed.";
                stopwatch.Stop();
                return false;
            }

            activeMeshFilter = targetMeshFilter;
            activeMeshRenderer = targetMeshRenderer;
            placement = targetPlacement;

            PlanetMarchingCubesPaintResult result;
            try
            {
                result = painter.PaintNamedMesh(
                    activeMeshFilter,
                    activeMeshRenderer,
                    materialOverride,
                    meshId,
                    surfaceMesh,
                    waterMesh,
                    in recipe,
                    settings,
                    cacheChunkId);
            }
            catch (System.Exception exception)
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "Named mesh paint blocked",
                    exception.Message,
                    "Send 09 a valid meshId and meshes already prepared by 10.",
                    "meshId=" + meshId);
                lastAction = "Paint Named Mesh blocked.";
                stopwatch.Stop();
                CaptureMetrics("Paint Named Mesh Blocked", stopwatch.Elapsed.TotalMilliseconds);
                return false;
            }

            hasLiveMesh = painter.RuntimeMesh != null ||
                          painter.RuntimeWaterMesh != null ||
                          painter.HasVisibleGpuWater ||
                          painter.RuntimeChunkMeshCount > 0 ||
                          painter.RuntimeChunkWaterMeshCount > 0;
            lastSourceTriangleCount = result.SourceTriangleCount;
            lastPaintedTriangleCount = result.PaintedTriangleCount;
            lastPaintedVertexCount = result.PaintedVertexCount;
            lastWaterTriangleCount = result.WaterTriangleCount;
            lastWaterVertexCount = result.WaterVertexCount;
            lastPaintedChunkCount = result.ChunkCount;
            lastMeshEstimatedBytes = result.MeshEstimatedBytes;
            lastWaterMeshEstimatedBytes = result.WaterMeshEstimatedBytes;
            ReregisterRuntimeResources();

            stopwatch.Stop();
            lastDiagnostic = PlanetLabDiagnostic.Ok(
                "Named mesh painted",
                "meshId=" + meshId);
            lastAction = "Paint Named Mesh finished.";
            CaptureMetrics("Paint Named Mesh", stopwatch.Elapsed.TotalMilliseconds);
            return result.HasVisibleMesh;
        }

        public bool PaintNamedCachedMesh(
            string meshId,
            PlanetChunkMeshCache cache,
            int cacheChunkId,
            int cacheLod,
            MeshFilter targetMeshFilter,
            MeshRenderer targetMeshRenderer,
            PlanetPlacement targetPlacement,
            in PlanetRecipe recipe,
            bool stagedGpuPublish = false)
        {
            stopwatch.Restart();

            if (targetMeshFilter == null)
            {
                lastDiagnostic = PlanetLabDiagnostic.Critical(
                    "Paint target MeshFilter is missing",
                    "09 needs a MeshFilter target to display a named cached mesh.",
                    "Pass the PlanetRecipePayloadPreview MeshFilter.",
                    "targetMeshFilter=null");
                lastAction = "Paint Named Cached Mesh failed.";
                stopwatch.Stop();
                return false;
            }

            if (targetMeshRenderer == null)
            {
                lastDiagnostic = PlanetLabDiagnostic.Critical(
                    "Paint target MeshRenderer is missing",
                    "09 needs a MeshRenderer target to display a named cached mesh.",
                    "Pass the PlanetRecipePayloadPreview MeshRenderer.",
                    "targetMeshRenderer=null");
                lastAction = "Paint Named Cached Mesh failed.";
                stopwatch.Stop();
                return false;
            }

            if (cache == null)
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "Chunk cache is missing",
                    "09 cannot load a named cached mesh without a cache instance.",
                    "Pass the active PlanetChunkMeshCache.",
                    "cache=null");
                lastAction = "Paint Named Cached Mesh failed.";
                stopwatch.Stop();
                return false;
            }

            if (!recipe.IsValid(out string recipeMessage))
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "PlanetRecipe is invalid",
                    recipeMessage,
                    "Fix the preview recipe before painting a named cached mesh.",
                    "recipe invalid");
                lastAction = "Paint Named Cached Mesh failed.";
                stopwatch.Stop();
                return false;
            }

            if (!settings.Validate(out string settingsMessage))
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "Marching Cubes paint settings are invalid",
                    settingsMessage,
                    "Fix paint settings before painting a named cached mesh.",
                    settings.ToString());
                lastAction = "Paint Named Cached Mesh failed.";
                stopwatch.Stop();
                return false;
            }

            activeMeshFilter = targetMeshFilter;
            activeMeshRenderer = targetMeshRenderer;
            placement = targetPlacement;

            PlanetMarchingCubesPaintResult result;
            try
            {
                result = painter.PaintNamedCachedMesh(
                    activeMeshFilter,
                    activeMeshRenderer,
                    materialOverride,
                    meshId,
                    cache,
                    cacheChunkId,
                    cacheLod,
                    in recipe,
                    settings,
                    stagedGpuPublish);
            }
            catch (System.Exception exception)
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "Named cached mesh paint blocked",
                    exception.Message,
                    "Send 09 a valid meshId, cache, chunkId and LOD.",
                    "meshId=" + meshId +
                    "\nchunkId=" + cacheChunkId +
                    "\nlod=" + cacheLod);
                lastAction = "Paint Named Cached Mesh blocked.";
                stopwatch.Stop();
                CaptureMetrics("Paint Named Cached Mesh Blocked", stopwatch.Elapsed.TotalMilliseconds);
                return false;
            }

            hasLiveMesh = painter.RuntimeMesh != null ||
                          painter.RuntimeWaterMesh != null ||
                          painter.HasVisibleGpuWater ||
                          painter.RuntimeChunkMeshCount > 0 ||
                          painter.RuntimeChunkWaterMeshCount > 0;
            lastSourceTriangleCount = result.SourceTriangleCount;
            lastPaintedTriangleCount = result.PaintedTriangleCount;
            lastPaintedVertexCount = result.PaintedVertexCount;
            lastWaterTriangleCount = result.WaterTriangleCount;
            lastWaterVertexCount = result.WaterVertexCount;
            lastPaintedChunkCount = result.ChunkCount;
            lastMeshEstimatedBytes = result.MeshEstimatedBytes;
            lastWaterMeshEstimatedBytes = result.WaterMeshEstimatedBytes;
            ReregisterRuntimeResources();

            stopwatch.Stop();
            lastDiagnostic = PlanetLabDiagnostic.Ok(
                "Named cached mesh painted",
                "meshId=" + meshId +
                "\nchunkId=" + cacheChunkId +
                "\nlod=" + cacheLod);
            lastAction = "Paint Named Cached Mesh finished.";
            CaptureMetrics("Paint Named Cached Mesh", stopwatch.Elapsed.TotalMilliseconds);
            return result.HasVisibleMesh;
        }

        public bool PaintLastExtractionAsNamedMesh(
            string meshId,
            int chunkId,
            MeshFilter targetMeshFilter,
            MeshRenderer targetMeshRenderer,
            PlanetPlacement targetPlacement,
            in PlanetRecipe recipe,
            PlanetChunkMeshCache cache,
            int lod,
            bool stagedGpuPublish = false)
        {
            stopwatch.Restart();

            if (marchingCubesLab == null)
            {
                ResolveReferences();
            }

            if (marchingCubesLab == null)
            {
                lastDiagnostic = PlanetLabDiagnostic.Critical(
                    "Marching Cubes Lab reference is missing",
                    "09 needs the latest 07 extraction to paint a named mesh.",
                    "Assign PlanetMarchingCubesLab from the same scene.",
                    "marchingCubesLab=null");
                lastAction = "Paint Named Extraction failed.";
                stopwatch.Stop();
                return false;
            }

            if (targetMeshFilter == null || targetMeshRenderer == null)
            {
                lastDiagnostic = PlanetLabDiagnostic.Critical(
                    "Paint target is missing",
                    "09 needs a MeshFilter and MeshRenderer target to display a named extraction.",
                    "Pass the PlanetRecipePayloadPreview render target.",
                    "targetMeshFilter=" + targetMeshFilter + "\ntargetMeshRenderer=" + targetMeshRenderer);
                lastAction = "Paint Named Extraction failed.";
                stopwatch.Stop();
                return false;
            }

            if (!recipe.IsValid(out string recipeMessage))
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "PlanetRecipe is invalid",
                    recipeMessage,
                    "Fix the recipe before painting a named extraction.",
                    "recipe invalid");
                lastAction = "Paint Named Extraction failed.";
                stopwatch.Stop();
                return false;
            }

            if (!settings.Validate(out string settingsMessage))
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "Marching Cubes paint settings are invalid",
                    settingsMessage,
                    "Fix paint settings before painting a named extraction.",
                    settings.ToString());
                lastAction = "Paint Named Extraction failed.";
                stopwatch.Stop();
                return false;
            }

            activeMeshFilter = targetMeshFilter;
            activeMeshRenderer = targetMeshRenderer;
            placement = targetPlacement;

            PlanetMarchingCubesPaintResult result;
            try
            {
                result = painter.PaintNamedExtraction(
                    activeMeshFilter,
                    activeMeshRenderer,
                    materialOverride,
                    meshId,
                    chunkId,
                    marchingCubesLab.LastResult,
                    in recipe,
                    in placement,
                    settings,
                    cache,
                    lod,
                    stagedGpuPublish);
            }
            catch (System.Exception exception)
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "Named extraction paint blocked",
                    exception.Message,
                    "Check the 07 extraction result for this chunk.",
                    "meshId=" + meshId + "\nchunkId=" + chunkId + "\nLOD=" + lod);
                lastAction = "Paint Named Extraction blocked.";
                stopwatch.Stop();
                CaptureMetrics("Paint Named Extraction Blocked", stopwatch.Elapsed.TotalMilliseconds);
                return false;
            }

            hasLiveMesh = painter.RuntimeMesh != null ||
                          painter.RuntimeWaterMesh != null ||
                          painter.HasVisibleGpuWater ||
                          painter.RuntimeChunkMeshCount > 0 ||
                          painter.RuntimeChunkWaterMeshCount > 0;
            lastSourceTriangleCount = result.SourceTriangleCount;
            lastPaintedTriangleCount = result.PaintedTriangleCount;
            lastPaintedVertexCount = result.PaintedVertexCount;
            lastWaterTriangleCount = result.WaterTriangleCount;
            lastWaterVertexCount = result.WaterVertexCount;
            lastPaintedChunkCount = result.ChunkCount;
            lastMeshEstimatedBytes = result.MeshEstimatedBytes;
            lastWaterMeshEstimatedBytes = result.WaterMeshEstimatedBytes;
            ReregisterRuntimeResources();

            stopwatch.Stop();
            lastDiagnostic = PlanetLabDiagnostic.Ok(
                "Named extraction painted",
                "meshId=" + meshId + "\nchunkId=" + chunkId + "\nLOD=" + lod);
            lastAction = "Paint Named Extraction finished.";
            CaptureMetrics("Paint Named Extraction", stopwatch.Elapsed.TotalMilliseconds);
            return result.HasVisibleMesh;
        }

        public bool PaintGlobalWater(
            MeshFilter targetMeshFilter,
            MeshRenderer targetMeshRenderer,
            PlanetPlacement targetPlacement,
            in PlanetRecipe recipe)
        {
            stopwatch.Restart();

            if (targetMeshFilter == null || targetMeshRenderer == null)
            {
                lastDiagnostic = PlanetLabDiagnostic.Critical(
                    "Paint target is missing",
                    "09 needs a MeshFilter and MeshRenderer target to display global water.",
                    "Pass the PlanetRecipePayloadPreview render target.",
                    "targetMeshFilter=" + targetMeshFilter + "\ntargetMeshRenderer=" + targetMeshRenderer);
                lastAction = "Paint Global Water failed.";
                stopwatch.Stop();
                return false;
            }

            if (!recipe.IsValid(out string recipeMessage))
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "PlanetRecipe is invalid",
                    recipeMessage,
                    "Fix the recipe before painting global water.",
                    "recipe invalid");
                lastAction = "Paint Global Water failed.";
                stopwatch.Stop();
                return false;
            }

            if (!settings.Validate(out string settingsMessage))
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "Marching Cubes paint settings are invalid",
                    settingsMessage,
                    "Fix paint settings before painting global water.",
                    settings.ToString());
                lastAction = "Paint Global Water failed.";
                stopwatch.Stop();
                return false;
            }

            activeMeshFilter = targetMeshFilter;
            activeMeshRenderer = targetMeshRenderer;
            placement = targetPlacement;

            PlanetMarchingCubesPaintResult result;
            try
            {
                result = painter.PaintGlobalWater(
                    activeMeshFilter,
                    activeMeshRenderer,
                    in recipe,
                    in placement,
                    settings);
            }
            catch (System.Exception exception)
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "Global water paint blocked",
                    exception.Message,
                    "Check the recipe and water material.",
                    "targetMeshFilter=" + targetMeshFilter + "\ntargetMeshRenderer=" + targetMeshRenderer);
                lastAction = "Paint Global Water blocked.";
                stopwatch.Stop();
                CaptureMetrics("Paint Global Water Blocked", stopwatch.Elapsed.TotalMilliseconds);
                return false;
            }

            hasLiveMesh = painter.RuntimeMesh != null ||
                          painter.RuntimeWaterMesh != null ||
                          painter.HasVisibleGpuWater ||
                          painter.RuntimeChunkMeshCount > 0 ||
                          painter.RuntimeChunkWaterMeshCount > 0;
            lastWaterTriangleCount = result.WaterTriangleCount;
            lastWaterVertexCount = result.WaterVertexCount;
            lastWaterMeshEstimatedBytes = result.WaterMeshEstimatedBytes;
            ReregisterRuntimeResources();

            stopwatch.Stop();
            lastDiagnostic = PlanetLabDiagnostic.Ok(
                "Global water painted",
                "waterTriangleCount=" + result.WaterTriangleCount +
                "\nwaterVertexCount=" + result.WaterVertexCount);
            lastAction = "Paint Global Water finished.";
            CaptureMetrics("Paint Global Water", stopwatch.Elapsed.TotalMilliseconds);
            return result.HasVisibleMesh;
        }

        private void PaintLastExtractionInternal(
            MeshFilter targetMeshFilter,
            MeshRenderer targetMeshRenderer,
            PlanetPlacement targetPlacement,
            bool paintByChunks)
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
                result = paintByChunks
                    ? painter.PaintChunks(
                        activeMeshFilter,
                        activeMeshRenderer,
                        materialOverride,
                        marchingCubesLab.LastResult,
                        shapeLab.Recipe,
                        placement,
                        settings)
                    : painter.Paint(
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
                    "Check the 09 Environment painter budget and the source extraction diagnostics.",
                    settings.ToString());
                lastAction = paintByChunks ? "Paint Last Extraction By Chunks blocked." : "Paint Last Extraction blocked.";
                stopwatch.Stop();
                CaptureMetrics(paintByChunks ? "Paint Last Extraction By Chunks Blocked" : "Paint Last Extraction Blocked", stopwatch.Elapsed.TotalMilliseconds);
                return;
            }

            ApplyResultSummary(result);
            RegisterRuntimeResources();

            stopwatch.Stop();
            lastDiagnostic = PlanetLabDiagnostic.Ok(
                paintByChunks ? "Marching Cubes chunk meshes painted" : "Marching Cubes mesh painted",
                BuildResultMetrics());
            lastAction = paintByChunks ? "Paint Last Extraction By Chunks finished." : "Paint Last Extraction finished.";
            CaptureMetrics(paintByChunks ? "Paint Last Extraction By Chunks" : "Paint Last Extraction", stopwatch.Elapsed.TotalMilliseconds);
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
            MarkReleased(ref waterMaterialResourceId);
            MarkReleased(ref materialResourceId);
            MarkReleased(ref chunkWaterMeshesResourceId);
            MarkReleased(ref chunkMeshesResourceId);
            MarkReleased(ref waterMeshResourceId);
            MarkReleased(ref meshResourceId);
            hasLiveMesh = false;
            lastSourceTriangleCount = 0;
            lastPaintedTriangleCount = 0;
            lastPaintedVertexCount = 0;
            lastWaterTriangleCount = 0;
            lastWaterVertexCount = 0;
            lastPaintedChunkCount = 0;
            lastChunkCachePayloadMode = PlanetChunkCachePayloadMode.MeshOnly;
            lastChunkCacheRequestedChunkCount = 0;
            lastChunkCacheLoadedChunkCount = 0;
            lastChunkCacheMeshOnlyLoadCount = 0;
            lastChunkCacheChunkDataLoadCount = 0;
            lastChunkCacheMissingChunkDataCount = 0;
            lastChunkWorkPackageMode = PlanetChunkWorkPackageMode.ImmediateLod2Shell;
            lastChunkWorkPackageSize = 0;
            lastChunkWorkPackageCount = 0;
            lastChunkWorkImmediateShell = false;
            lastMeshEstimatedBytes = 0L;
            lastWaterMeshEstimatedBytes = 0L;

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
            lastWaterTriangleCount = result.WaterTriangleCount;
            lastWaterVertexCount = result.WaterVertexCount;
            lastPaintedChunkCount = result.ChunkCount;
            lastMeshEstimatedBytes = result.MeshEstimatedBytes;
            lastWaterMeshEstimatedBytes = result.WaterMeshEstimatedBytes;
        }

        private void ApplyChunkCacheLoadSummary(PlanetChunkCacheLoadSummary summary)
        {
            lastChunkCachePayloadMode = summary.PayloadMode;
            lastChunkCacheRequestedChunkCount = summary.RequestedChunkCount;
            lastChunkCacheLoadedChunkCount = summary.LoadedChunkCount;
            lastChunkCacheMeshOnlyLoadCount = summary.MeshOnlyLoadCount;
            lastChunkCacheChunkDataLoadCount = summary.ChunkDataLoadCount;
            lastChunkCacheMissingChunkDataCount = summary.MissingChunkDataCount;
        }

        private void ApplyChunkWorkPackageSummary(PlanetChunkWorkPackageSummary summary)
        {
            lastChunkWorkPackageMode = summary.Mode;
            lastChunkWorkPackageSize = summary.PackageSize;
            lastChunkWorkPackageCount = summary.PackageCount;
            lastChunkWorkImmediateShell = summary.IsImmediateShell;
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

            if (painter.RuntimeWaterMesh != null)
            {
                waterMeshResourceId = resourceRegistry.RegisterResource(
                    "PlanetMarchingCubesPaint Water Mesh",
                    PlanetLabResourceType.Mesh,
                    OwnerName,
                    lastWaterMeshEstimatedBytes,
                    lastWaterVertexCount,
                    0);
            }
            else if (painter.HasVisibleGpuWater)
            {
                waterMeshResourceId = resourceRegistry.RegisterResource(
                    "PlanetMarchingCubesPaint GPU Water Buffer",
                    PlanetLabResourceType.GraphicsBuffer,
                    OwnerName,
                    painter.RuntimeGpuWaterEstimatedBytes,
                    lastWaterVertexCount,
                    PlanetTriangleGpuVertex.Stride);
            }

            if (painter.RuntimeChunkMeshCount > 0)
            {
                chunkMeshesResourceId = resourceRegistry.RegisterResource(
                    "PlanetMarchingCubesPaint Chunk Surface Meshes",
                    PlanetLabResourceType.Mesh,
                    OwnerName,
                    painter.RuntimeChunkMeshEstimatedBytes,
                    painter.RuntimeChunkVertexCount,
                    0);
            }

            if (painter.RuntimeChunkWaterMeshCount > 0)
            {
                chunkWaterMeshesResourceId = resourceRegistry.RegisterResource(
                    "PlanetMarchingCubesPaint Chunk Water Meshes",
                    PlanetLabResourceType.Mesh,
                    OwnerName,
                    painter.RuntimeChunkWaterMeshEstimatedBytes,
                    painter.RuntimeChunkWaterVertexCount,
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

            if (painter.OwnsRuntimeWaterMaterial)
            {
                waterMaterialResourceId = resourceRegistry.RegisterResource(
                    "PlanetMarchingCubesPaint Water Material",
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

        private void ReregisterRuntimeResources()
        {
            MarkReleased(ref surfaceAtlasResourceId);
            MarkReleased(ref waterMaterialResourceId);
            MarkReleased(ref materialResourceId);
            MarkReleased(ref chunkWaterMeshesResourceId);
            MarkReleased(ref chunkMeshesResourceId);
            MarkReleased(ref waterMeshResourceId);
            MarkReleased(ref meshResourceId);
            RegisterRuntimeResources();
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
                   "\npaintedChunkCount=" + lastPaintedChunkCount +
                   "\nchunkCachePayloadMode=" + lastChunkCachePayloadMode +
                   "\nchunkCacheRequestedChunkCount=" + lastChunkCacheRequestedChunkCount +
                   "\nchunkCacheLoadedChunkCount=" + lastChunkCacheLoadedChunkCount +
                   "\nchunkCacheMeshOnlyLoadCount=" + lastChunkCacheMeshOnlyLoadCount +
                   "\nchunkCacheChunkDataLoadCount=" + lastChunkCacheChunkDataLoadCount +
                   "\nchunkCacheMissingChunkDataCount=" + lastChunkCacheMissingChunkDataCount +
                   "\nchunkWorkPackageMode=" + lastChunkWorkPackageMode +
                   "\nchunkWorkPackageSize=" + lastChunkWorkPackageSize +
                   "\nchunkWorkPackageCount=" + lastChunkWorkPackageCount +
                   "\nchunkWorkImmediateShell=" + lastChunkWorkImmediateShell +
                   "\nwaterTriangleCount=" + lastWaterTriangleCount +
                   "\nwaterVertexCount=" + lastWaterVertexCount +
                   "\nmeshEstimatedBytes=" + lastMeshEstimatedBytes +
                   "\nwaterMeshEstimatedBytes=" + lastWaterMeshEstimatedBytes +
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
