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
        [SerializeField] private PlanetRecipe chunkLodBaseRecipe = PlanetRecipe.Default();
        [SerializeField] private bool hasChunkLodBaseRecipe;

        [Header("State")]
        [SerializeField] private bool hasLiveMesh;
        [SerializeField] private int lastSourceTriangleCount;
        [SerializeField] private int lastPaintedTriangleCount;
        [SerializeField] private int lastPaintedVertexCount;
        [SerializeField] private int lastWaterTriangleCount;
        [SerializeField] private int lastWaterVertexCount;
        [SerializeField] private int lastPaintedChunkCount;
        [SerializeField] private int lastDesiredLod0ChunkCount;
        [SerializeField] private int lastDesiredLod1ChunkCount;
        [SerializeField] private int lastDesiredLod2ChunkCount;
        [SerializeField] private float lastBestChunkLodScore;
        [SerializeField] private float lastAverageChunkLodScore;
        [SerializeField] private PlanetChunkCachePayloadMode lastChunkCachePayloadMode;
        [SerializeField] private int lastChunkCacheRequestedChunkCount;
        [SerializeField] private int lastChunkCacheLoadedChunkCount;
        [SerializeField] private int lastChunkCacheMeshOnlyLoadCount;
        [SerializeField] private int lastChunkCacheChunkDataLoadCount;
        [SerializeField] private int lastChunkCacheMissingChunkDataCount;
        [SerializeField] private long lastMeshEstimatedBytes;
        [SerializeField] private long lastWaterMeshEstimatedBytes;
        [SerializeField] private string lastAction;
        [SerializeField] private PlanetLabMetricsSnapshot lastSnapshot;
        [SerializeField] private PlanetLabDiagnostic lastDiagnostic;

        private readonly Stopwatch stopwatch = new Stopwatch();
        private readonly PlanetMarchingCubesMeshPainter painter = new PlanetMarchingCubesMeshPainter();
        private readonly List<PlanetCachedChunkMesh> cachedChunks = new List<PlanetCachedChunkMesh>();

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
        public int LastSourceTriangleCount => lastSourceTriangleCount;
        public int LastPaintedTriangleCount => lastPaintedTriangleCount;
        public int LastPaintedVertexCount => lastPaintedVertexCount;
        public int LastWaterTriangleCount => lastWaterTriangleCount;
        public int LastWaterVertexCount => lastWaterVertexCount;
        public int LastPaintedChunkCount => lastPaintedChunkCount;
        public int LastDesiredLod0ChunkCount => lastDesiredLod0ChunkCount;
        public int LastDesiredLod1ChunkCount => lastDesiredLod1ChunkCount;
        public int LastDesiredLod2ChunkCount => lastDesiredLod2ChunkCount;
        public float LastBestChunkLodScore => lastBestChunkLodScore;
        public float LastAverageChunkLodScore => lastAverageChunkLodScore;
        public PlanetChunkCachePayloadMode LastChunkCachePayloadMode => lastChunkCachePayloadMode;
        public int LastChunkCacheRequestedChunkCount => lastChunkCacheRequestedChunkCount;
        public int LastChunkCacheLoadedChunkCount => lastChunkCacheLoadedChunkCount;
        public int LastChunkCacheMeshOnlyLoadCount => lastChunkCacheMeshOnlyLoadCount;
        public int LastChunkCacheChunkDataLoadCount => lastChunkCacheChunkDataLoadCount;
        public int LastChunkCacheMissingChunkDataCount => lastChunkCacheMissingChunkDataCount;
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

        public void SetChunkLodBaseRecipe(in PlanetRecipe lod1Recipe)
        {
            chunkLodBaseRecipe = lod1Recipe;
            hasChunkLodBaseRecipe = true;
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

        public bool TryPaintCachedChunks(
            PlanetChunkMeshCache cache,
            int lod,
            MeshFilter targetMeshFilter,
            MeshRenderer targetMeshRenderer,
            PlanetPlacement targetPlacement,
            in PlanetRecipe recipe)
        {
            stopwatch.Restart();
            ReleaseModule();
            stopwatch.Restart();

            if (cache == null)
            {
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "Chunk cache is missing",
                    "10 step 2 needs a cache instance to load chunk meshes.",
                    "Create and prepare PlanetChunkMeshCache before calling TryPaintCachedChunks.",
                    "cache=null");
                lastAction = "Paint Cached Chunks failed.";
                stopwatch.Stop();
                return false;
            }

            if (targetMeshFilter == null)
            {
                lastDiagnostic = PlanetLabDiagnostic.Critical(
                    "Paint target MeshFilter is missing",
                    "10 step 2 needs a MeshFilter target to display cached chunks.",
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
                    "10 step 2 needs a MeshRenderer target to display cached chunks.",
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
                    "Fix the preview recipe before loading cached chunks.",
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
                    "Fix paint settings before loading cached chunks.",
                    settings.ToString());
                lastAction = "Paint Cached Chunks failed.";
                stopwatch.Stop();
                return false;
            }

            PlanetChunkCachePayloadMode payloadMode = PlanetChunkCachePayloadMode.MeshOnly;
            if (!cache.TryLoadAllChunkMeshes(lod, payloadMode, cachedChunks, out PlanetChunkCacheLoadSummary cacheLoadSummary))
            {
                ApplyChunkCacheLoadSummary(cacheLoadSummary);
                lastDiagnostic = PlanetLabDiagnostic.Warning(
                    "Chunk cache miss",
                    cache.LastDiagnostic,
                    "Generate will continue through 06 -> 07 and write the chunk cache afterwards.",
                    "LOD=" + lod);
                lastAction = "Paint Cached Chunks cache miss.";
                stopwatch.Stop();
                CaptureMetrics("Paint Cached Chunks Cache Miss", stopwatch.Elapsed.TotalMilliseconds);
                return false;
            }

            ApplyChunkCacheLoadSummary(cacheLoadSummary);
            activeMeshFilter = targetMeshFilter;
            activeMeshRenderer = targetMeshRenderer;
            placement = targetPlacement;

            try
            {
                PlanetMarchingCubesPaintResult result = painter.PaintCachedChunks(
                    activeMeshFilter,
                    activeMeshRenderer,
                    materialOverride,
                    cachedChunks,
                    in recipe,
                    settings);
                ApplyResultSummary(result);
                ApplyChunkLodScoring(in recipe);
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
                BuildResultMetrics() + "\ncache=" + cache.RootPath + "\nLOD=" + lod);
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
            lastAction = "Save Live Chunks To Cache finished.";
            return savedChunkCount;
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
            PlanetRecipe scoringRecipe = shapeLab.Recipe;
            ApplyChunkLodScoring(in scoringRecipe);
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
            lastDesiredLod0ChunkCount = 0;
            lastDesiredLod1ChunkCount = 0;
            lastDesiredLod2ChunkCount = 0;
            lastBestChunkLodScore = 0f;
            lastAverageChunkLodScore = 0f;
            lastChunkCachePayloadMode = PlanetChunkCachePayloadMode.MeshOnly;
            lastChunkCacheRequestedChunkCount = 0;
            lastChunkCacheLoadedChunkCount = 0;
            lastChunkCacheMeshOnlyLoadCount = 0;
            lastChunkCacheChunkDataLoadCount = 0;
            lastChunkCacheMissingChunkDataCount = 0;
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

        private void ApplyChunkLodScoring(in PlanetRecipe renderRecipe)
        {
            PlanetRecipe lod1Recipe = hasChunkLodBaseRecipe ? chunkLodBaseRecipe : renderRecipe;
            float baseChunkWorldSize = PlanetChunkLodUtility.CalculateBaseChunkWorldSize(in lod1Recipe);
            PlanetChunkLodScoringContext context = new PlanetChunkLodScoringContext(
                PlanetTrianglePoolRegistry.PlayerPositionWorld,
                PlanetTrianglePoolRegistry.PlayerForwardWorld,
                baseChunkWorldSize);
            PlanetChunkLodSummary summary = painter.ScoreRuntimeChunks(in context);
            lastDesiredLod0ChunkCount = summary.lod0Count;
            lastDesiredLod1ChunkCount = summary.lod1Count;
            lastDesiredLod2ChunkCount = summary.lod2Count;
            lastBestChunkLodScore = summary.bestScore;
            lastAverageChunkLodScore = summary.averageScore;
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
                   "\ndesiredLOD0ChunkCount=" + lastDesiredLod0ChunkCount +
                   "\ndesiredLOD1ChunkCount=" + lastDesiredLod1ChunkCount +
                   "\ndesiredLOD2ChunkCount=" + lastDesiredLod2ChunkCount +
                   "\nbestChunkLodScore=" + lastBestChunkLodScore.ToString("0.000") +
                   "\naverageChunkLodScore=" + lastAverageChunkLodScore.ToString("0.000") +
                   "\nchunkCachePayloadMode=" + lastChunkCachePayloadMode +
                   "\nchunkCacheRequestedChunkCount=" + lastChunkCacheRequestedChunkCount +
                   "\nchunkCacheLoadedChunkCount=" + lastChunkCacheLoadedChunkCount +
                   "\nchunkCacheMeshOnlyLoadCount=" + lastChunkCacheMeshOnlyLoadCount +
                   "\nchunkCacheChunkDataLoadCount=" + lastChunkCacheChunkDataLoadCount +
                   "\nchunkCacheMissingChunkDataCount=" + lastChunkCacheMissingChunkDataCount +
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
