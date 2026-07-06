using System;
using System.Collections.Generic;
using MarchingCubesPlanet.Coordinates;
using MarchingCubesPlanet.Shape;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarchingCubesPlanet.MarchingCubes
{
    public sealed class PlanetMarchingCubesMeshPainter
    {
        private const string SurfaceShaderName = "MarchingCubesPlanet/Planet/Surface";
        private const string UrpUnlitShaderName = "Universal Render Pipeline/Unlit";
        private const string DefaultSurfaceMaterialResourceName = "PlanetWorld_Surface";
        private const string DefaultOceanMaterialResourceName = "PlanetOcean";
        private const string SurfaceAtlasTexturePropertyName = "_PlanetSurfaceAtlas";
        private const string UseSurfaceAtlasPropertyName = "_UsePlanetSurfaceAtlas";
        private const string BaseMapTexturePropertyName = "_BaseMap";
        private const int SurfaceAtlasResolution = 256;
        private const float SurfaceAtlasSeaLevelV = 0.5f;
        private const float SurfaceAtlasLandRangeScale = 0.6f;
        private const int DefaultMeshUploadBufferCapacity = 65536;
        private const int GlobalWaterLongitudeSegments = 128;
        private const int GlobalWaterLatitudeSegments = 64;

        private readonly List<RuntimeChunkMesh> runtimeChunks = new List<RuntimeChunkMesh>();
        private readonly List<Vector3> meshUploadPositions = new List<Vector3>(DefaultMeshUploadBufferCapacity);
        private readonly List<Vector3> meshUploadNormals = new List<Vector3>(DefaultMeshUploadBufferCapacity);
        private readonly List<Vector2> meshUploadUvs = new List<Vector2>(DefaultMeshUploadBufferCapacity);
        private readonly List<Color32> meshUploadColors = new List<Color32>(DefaultMeshUploadBufferCapacity);
        private readonly List<int> meshUploadIndices = new List<int>(DefaultMeshUploadBufferCapacity);

        private Mesh runtimeMesh;
        private Mesh runtimeWaterMesh;
        private Material runtimeMaterial;
        private Material runtimeWaterMaterial;
        private Texture2D runtimeSurfaceAtlas;
        private GameObject runtimeWaterObject;

        public Mesh RuntimeMesh => runtimeMesh;
        public Mesh RuntimeWaterMesh => runtimeWaterMesh;
        public Material RuntimeMaterial => runtimeMaterial;
        public Material RuntimeWaterMaterial => runtimeWaterMaterial;
        public Texture2D RuntimeSurfaceAtlas => runtimeSurfaceAtlas;
        public bool HasVisibleGpuWater => false;
        public long RuntimeGpuWaterEstimatedBytes => 0L;
        public bool OwnsRuntimeMaterial => runtimeMaterial != null;
        public bool OwnsRuntimeWaterMaterial => runtimeWaterMaterial != null;
        public bool OwnsRuntimeSurfaceAtlas => runtimeSurfaceAtlas != null;
        public long RuntimeSurfaceAtlasEstimatedBytes => runtimeSurfaceAtlas != null
            ? (long)runtimeSurfaceAtlas.width * runtimeSurfaceAtlas.height * 4L
            : 0L;
        public int RuntimeSurfaceAtlasPixelCount => runtimeSurfaceAtlas != null
            ? runtimeSurfaceAtlas.width * runtimeSurfaceAtlas.height
            : 0;
        public int RuntimeChunkCount => runtimeChunks.Count;
        public int RuntimeChunkMeshCount => CountRuntimeChunkMeshes(false);
        public int RuntimeChunkWaterMeshCount => CountRuntimeChunkMeshes(true);
        public int RuntimeChunkVertexCount => SumRuntimeChunkVertices(false);
        public int RuntimeChunkTriangleCount => SumRuntimeChunkTriangles(false);
        public int RuntimeChunkWaterVertexCount => SumRuntimeChunkVertices(true);
        public int RuntimeChunkWaterTriangleCount => SumRuntimeChunkTriangles(true);
        public long RuntimeChunkMeshEstimatedBytes => SumRuntimeChunkBytes(false);
        public long RuntimeChunkWaterMeshEstimatedBytes => SumRuntimeChunkBytes(true);

        private sealed class RuntimeChunkMesh
        {
            public string meshId;
            public int chunkIndex;
            public GameObject surfaceObject;
            public MeshFilter surfaceMeshFilter;
            public MeshRenderer surfaceMeshRenderer;
            public Mesh surfaceMesh;
            public GameObject surfaceBackObject;
            public MeshFilter surfaceBackMeshFilter;
            public MeshRenderer surfaceBackMeshRenderer;
            public Mesh surfaceBackMesh;
            public GameObject waterObject;
            public MeshFilter waterMeshFilter;
            public MeshRenderer waterMeshRenderer;
            public Mesh waterMesh;
            public GameObject waterBackObject;
            public MeshFilter waterBackMeshFilter;
            public MeshRenderer waterBackMeshRenderer;
            public Mesh waterBackMesh;
            public int surfaceTriangleCount;
            public int surfaceVertexCount;
            public int waterTriangleCount;
            public int waterVertexCount;
            public long surfaceEstimatedBytes;
            public long waterEstimatedBytes;
        }

        public static string BuildChunkMeshId(int chunkId)
        {
            return "chunk_" + Mathf.Max(0, chunkId);
        }

        public PlanetMarchingCubesPaintResult Paint(
            MeshFilter meshFilter,
            MeshRenderer meshRenderer,
            Material materialOverride,
            PlanetMarchingCubesExtractionResult source,
            in PlanetRecipe recipe,
            in PlanetPlacement placement,
            PlanetMarchingCubesPaintSettings settings)
        {
            ValidatePaintInputs(meshFilter, meshRenderer, source, in recipe, settings);
            int sourceVertexCount = source.VertexCount - source.VertexCount % 3;
            int sourceTriangleCount = sourceVertexCount / 3;
            ValidateVertexCapacity(sourceVertexCount, settings);

            PlanetGpuShapeCell[] cells = BuildCells(in recipe);
            Release(meshFilter, meshRenderer);

            runtimeMesh = new Mesh
            {
                name = "PlanetMarchingCubesPaint_Mesh_Runtime",
                indexFormat = sourceVertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            BuildMeshFromExtraction(
                runtimeMesh,
                meshFilter.transform,
                source.Vertices,
                sourceVertexCount,
                in recipe,
                in placement,
                settings,
                cells);

            meshFilter.sharedMesh = runtimeMesh;
            meshRenderer.sharedMaterial = ResolveMaterial(materialOverride, settings.colorMode, cells);

            return new PlanetMarchingCubesPaintResult(
                sourceTriangleCount,
                sourceTriangleCount,
                sourceVertexCount,
                PlanetMarchingCubesPaintResult.CalculateMeshEstimatedBytes(sourceVertexCount, sourceTriangleCount),
                settings.colorMode);
        }

        public PlanetMarchingCubesPaintResult PaintChunks(
            MeshFilter meshFilter,
            MeshRenderer meshRenderer,
            Material materialOverride,
            PlanetMarchingCubesExtractionResult source,
            in PlanetRecipe recipe,
            in PlanetPlacement placement,
            PlanetMarchingCubesPaintSettings settings)
        {
            return Paint(meshFilter, meshRenderer, materialOverride, source, in recipe, in placement, settings);
        }

        public PlanetMarchingCubesPaintResult PaintGlobalWater(
            MeshFilter meshFilter,
            MeshRenderer meshRenderer,
            in PlanetRecipe recipe,
            in PlanetPlacement placement,
            PlanetMarchingCubesPaintSettings settings)
        {
            if (meshFilter == null)
            {
                throw new ArgumentNullException(nameof(meshFilter));
            }

            if (meshRenderer == null)
            {
                throw new ArgumentNullException(nameof(meshRenderer));
            }

            if (!settings.Validate(out string settingsMessage))
            {
                throw new ArgumentException(settingsMessage, nameof(settings));
            }

            if (!recipe.IsValid(out string recipeMessage))
            {
                throw new ArgumentException(recipeMessage, nameof(recipe));
            }

            ReleaseWater();
            int triangleCount = GlobalWaterLatitudeSegments * GlobalWaterLongitudeSegments * 2;
            int vertexCount = triangleCount * 3;
            runtimeWaterObject = new GameObject("PlanetGlobalWater_Mesh_Runtime");
            runtimeWaterObject.transform.SetParent(meshFilter.transform, false);

            MeshFilter waterFilter = runtimeWaterObject.AddComponent<MeshFilter>();
            MeshRenderer waterRenderer = runtimeWaterObject.AddComponent<MeshRenderer>();
            CopyRendererSettings(meshRenderer, waterRenderer);
            waterRenderer.sharedMaterial = ResolveWaterMaterial();

            runtimeWaterMesh = new Mesh
            {
                name = "PlanetGlobalWater_Mesh_Runtime",
                indexFormat = vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
            };

            EnsureListCapacity(meshUploadPositions, vertexCount);
            EnsureListCapacity(meshUploadNormals, vertexCount);
            EnsureListCapacity(meshUploadUvs, vertexCount);
            EnsureListCapacity(meshUploadColors, vertexCount);
            EnsureListCapacity(meshUploadIndices, vertexCount);
            meshUploadPositions.Clear();
            meshUploadNormals.Clear();
            meshUploadUvs.Clear();
            meshUploadColors.Clear();
            meshUploadIndices.Clear();

            for (int latitude = 0; latitude < GlobalWaterLatitudeSegments; latitude++)
            {
                for (int longitude = 0; longitude < GlobalWaterLongitudeSegments; longitude++)
                {
                    Vector3 upperLeft = EvaluateGlobalWaterDirection(latitude, longitude);
                    Vector3 upperRight = EvaluateGlobalWaterDirection(latitude, longitude + 1);
                    Vector3 lowerLeft = EvaluateGlobalWaterDirection(latitude + 1, longitude);
                    Vector3 lowerRight = EvaluateGlobalWaterDirection(latitude + 1, longitude + 1);
                    AddGlobalWaterTriangle(meshFilter.transform, upperLeft, upperRight, lowerLeft, in recipe, in placement);
                    AddGlobalWaterTriangle(meshFilter.transform, upperRight, lowerRight, lowerLeft, in recipe, in placement);
                }
            }

            ApplyMeshData(runtimeWaterMesh);
            waterFilter.sharedMesh = runtimeWaterMesh;
            return new PlanetMarchingCubesPaintResult(
                0,
                0,
                0,
                0L,
                triangleCount,
                vertexCount,
                PlanetMarchingCubesPaintResult.CalculateMeshEstimatedBytes(vertexCount, triangleCount),
                settings.colorMode);
        }

        public PlanetMarchingCubesPaintResult PaintCachedChunks(
            MeshFilter meshFilter,
            MeshRenderer meshRenderer,
            Material materialOverride,
            List<PlanetCachedChunkMesh> chunks,
            in PlanetRecipe recipe,
            PlanetMarchingCubesPaintSettings settings)
        {
            if (chunks == null)
            {
                throw new ArgumentNullException(nameof(chunks));
            }

            if (meshFilter == null)
            {
                throw new ArgumentNullException(nameof(meshFilter));
            }

            if (meshRenderer == null)
            {
                throw new ArgumentNullException(nameof(meshRenderer));
            }

            if (!settings.Validate(out string settingsMessage))
            {
                throw new ArgumentException(settingsMessage, nameof(settings));
            }

            if (!recipe.IsValid(out string recipeMessage))
            {
                throw new ArgumentException(recipeMessage, nameof(recipe));
            }

            Release(meshFilter, meshRenderer);
            PlanetGpuShapeCell[] cells = BuildCells(in recipe);
            Material surfaceMaterial = ResolveMaterial(materialOverride, settings.colorMode, cells);

            int surfaceTriangles = 0;
            int surfaceVertices = 0;
            int waterTriangles = 0;
            int waterVertices = 0;
            long surfaceBytes = 0L;
            long waterBytes = 0L;
            int visibleChunks = 0;

            for (int i = 0; i < chunks.Count; i++)
            {
                PlanetCachedChunkMesh cached = chunks[i];
                if (cached == null)
                {
                    continue;
                }

                RuntimeChunkMesh slot = GetOrCreateRuntimeMeshSlot(BuildChunkMeshId(cached.ChunkId), cached.ChunkId);
                CopyCachedMeshIntoSlot(slot, cached.SurfaceMesh, cached.WaterMesh, meshFilter, meshRenderer, surfaceMaterial);
                UpdateRuntimeMeshSlotMetrics(slot);
                if (slot.surfaceTriangleCount > 0 || slot.waterTriangleCount > 0)
                {
                    visibleChunks++;
                }

                surfaceTriangles += slot.surfaceTriangleCount;
                surfaceVertices += slot.surfaceVertexCount;
                waterTriangles += slot.waterTriangleCount;
                waterVertices += slot.waterVertexCount;
                surfaceBytes += slot.surfaceEstimatedBytes;
                waterBytes += slot.waterEstimatedBytes;
            }

            return new PlanetMarchingCubesPaintResult(
                surfaceTriangles,
                surfaceTriangles,
                surfaceVertices,
                surfaceBytes,
                waterTriangles,
                waterVertices,
                waterBytes,
                settings.colorMode,
                visibleChunks);
        }

        public PlanetMarchingCubesPaintResult PaintNamedMesh(
            MeshFilter meshFilter,
            MeshRenderer meshRenderer,
            Material materialOverride,
            string meshId,
            Mesh surfaceMesh,
            Mesh waterMesh,
            in PlanetRecipe recipe,
            PlanetMarchingCubesPaintSettings settings,
            int cacheChunkId = -1)
        {
            ValidateNamedInputs(meshFilter, meshRenderer, meshId, in recipe, settings);
            PlanetGpuShapeCell[] cells = BuildCells(in recipe);
            Material surfaceMaterial = ResolveMaterial(materialOverride, settings.colorMode, cells);
            RuntimeChunkMesh slot = GetOrCreateRuntimeMeshSlot(meshId, cacheChunkId);
            CopyCachedMeshIntoSlot(slot, surfaceMesh, waterMesh, meshFilter, meshRenderer, surfaceMaterial);
            UpdateRuntimeMeshSlotMetrics(slot);
            DestroyIfNotOwned(surfaceMesh, slot.surfaceMesh);
            DestroyIfNotOwned(waterMesh, slot.waterMesh);
            return BuildNamedPaintResult(slot, settings);
        }

        public PlanetMarchingCubesPaintResult PaintNamedCachedMesh(
            MeshFilter meshFilter,
            MeshRenderer meshRenderer,
            Material materialOverride,
            string meshId,
            PlanetChunkMeshCache cache,
            int cacheChunkId,
            int cacheLod,
            in PlanetRecipe recipe,
            PlanetMarchingCubesPaintSettings settings,
            bool stagedGpuPublish = false)
        {
            _ = stagedGpuPublish;
            ValidateNamedInputs(meshFilter, meshRenderer, meshId, in recipe, settings);
            if (cache == null)
            {
                throw new ArgumentNullException(nameof(cache));
            }

            RuntimeChunkMesh slot = GetOrCreateRuntimeMeshSlot(meshId, cacheChunkId);
            if (!cache.TryGetChunkMeshAvailability(cacheChunkId, cacheLod, out bool hasSurfaceMesh, out bool hasWaterMesh))
            {
                return BuildEmptyPaintResult(settings);
            }

            if (hasSurfaceMesh)
            {
                PlanetGpuShapeCell[] cells = BuildCells(in recipe);
                Material surfaceMaterial = ResolveMaterial(materialOverride, settings.colorMode, cells);
                EnsureNamedSurfaceBackSlot(
                    slot,
                    meshFilter.transform,
                    meshFilter.gameObject.layer,
                    SanitizeObjectName(meshId) + "_Surface_Runtime",
                    meshRenderer,
                    surfaceMaterial);
            }

            if (hasWaterMesh)
            {
                EnsureNamedWaterBackSlot(
                    slot,
                    meshFilter.transform,
                    meshFilter.gameObject.layer,
                    SanitizeObjectName(meshId) + "_Water_Runtime",
                    meshRenderer);
            }

            Mesh surfaceTarget = hasSurfaceMesh ? slot.surfaceBackMesh : null;
            Mesh waterTarget = hasWaterMesh ? slot.waterBackMesh : null;
            if (!cache.TryLoadChunkMeshInto(
                    cacheChunkId,
                    cacheLod,
                    PlanetChunkCachePayloadMode.MeshOnly,
                    surfaceTarget,
                    waterTarget,
                    out bool loadedSurfaceMesh,
                    out bool loadedWaterMesh,
                    out _))
            {
                return BuildEmptyPaintResult(settings);
            }

            if (loadedSurfaceMesh)
            {
                SwapNamedSurfaceSlot(slot);
            }
            else
            {
                ClearNamedSurfaceSlot(slot);
            }

            if (loadedWaterMesh)
            {
                SwapNamedWaterSlot(slot);
            }
            else
            {
                ClearNamedWaterSlot(slot);
            }

            UpdateRuntimeMeshSlotMetrics(slot);
            return BuildNamedPaintResult(slot, settings);
        }

        public PlanetMarchingCubesPaintResult PaintNamedExtraction(
            MeshFilter meshFilter,
            MeshRenderer meshRenderer,
            Material materialOverride,
            string meshId,
            int cacheChunkId,
            PlanetMarchingCubesExtractionResult source,
            in PlanetRecipe recipe,
            in PlanetPlacement placement,
            PlanetMarchingCubesPaintSettings settings,
            PlanetChunkMeshCache cache,
            int cacheLod,
            bool stagedGpuPublish = false)
        {
            _ = stagedGpuPublish;
            ValidatePaintInputs(meshFilter, meshRenderer, source, in recipe, settings);
            if (string.IsNullOrWhiteSpace(meshId))
            {
                throw new ArgumentException("meshId cannot be empty.", nameof(meshId));
            }

            int sourceVertexCount = source.VertexCount - source.VertexCount % 3;
            int sourceTriangleCount = sourceVertexCount / 3;
            ValidateVertexCapacity(sourceVertexCount, settings);

            RuntimeChunkMesh slot = GetOrCreateRuntimeMeshSlot(meshId, cacheChunkId);
            if (sourceTriangleCount <= 0)
            {
                ClearNamedSurfaceSlot(slot);
                ClearNamedWaterSlot(slot);
                UpdateRuntimeMeshSlotMetrics(slot);
                return BuildNamedPaintResult(slot, settings);
            }

            PlanetGpuShapeCell[] cells = BuildCells(in recipe);
            Material surfaceMaterial = ResolveMaterial(materialOverride, settings.colorMode, cells);
            EnsureNamedSurfaceBackSlot(
                slot,
                meshFilter.transform,
                meshFilter.gameObject.layer,
                SanitizeObjectName(meshId) + "_Surface_Runtime",
                meshRenderer,
                surfaceMaterial);
            BuildMeshFromExtraction(
                slot.surfaceBackMesh,
                slot.surfaceBackObject.transform,
                source.Vertices,
                sourceVertexCount,
                in recipe,
                in placement,
                settings,
                cells);
            SwapNamedSurfaceSlot(slot);
            ClearNamedWaterSlot(slot);
            UpdateRuntimeMeshSlotMetrics(slot);

            if (cache != null && cacheChunkId >= 0 && cacheLod >= 0)
            {
                cache.SaveChunk(cacheChunkId, cacheLod, slot.surfaceMesh, null);
            }

            return BuildNamedPaintResult(slot, settings);
        }

        public int SaveRuntimeChunksToCache(PlanetChunkMeshCache cache, int lod)
        {
            if (cache == null)
            {
                throw new ArgumentNullException(nameof(cache));
            }

            int savedChunkCount = 0;
            for (int i = 0; i < runtimeChunks.Count; i++)
            {
                RuntimeChunkMesh chunk = runtimeChunks[i];
                if (chunk == null || chunk.chunkIndex < 0 || chunk.surfaceMesh == null && chunk.waterMesh == null)
                {
                    continue;
                }

                if (cache.SaveChunk(chunk.chunkIndex, lod, chunk.surfaceMesh, chunk.waterMesh))
                {
                    savedChunkCount++;
                }
            }

            return savedChunkCount;
        }

        public void Release(MeshFilter meshFilter, MeshRenderer meshRenderer)
        {
            ReleaseMeshOnly(meshFilter);
            ReleaseWater();
            ReleaseChunks();

            if (runtimeMaterial != null)
            {
                if (meshRenderer != null && meshRenderer.sharedMaterial == runtimeMaterial)
                {
                    meshRenderer.sharedMaterial = null;
                }

                DestroyRuntimeObject(runtimeMaterial);
                runtimeMaterial = null;
            }

            if (runtimeSurfaceAtlas != null)
            {
                DestroyRuntimeObject(runtimeSurfaceAtlas);
                runtimeSurfaceAtlas = null;
            }
        }

        private static void ValidatePaintInputs(
            MeshFilter meshFilter,
            MeshRenderer meshRenderer,
            PlanetMarchingCubesExtractionResult source,
            in PlanetRecipe recipe,
            PlanetMarchingCubesPaintSettings settings)
        {
            if (meshFilter == null)
            {
                throw new ArgumentNullException(nameof(meshFilter));
            }

            if (meshRenderer == null)
            {
                throw new ArgumentNullException(nameof(meshRenderer));
            }

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (!settings.Validate(out string settingsMessage))
            {
                throw new ArgumentException(settingsMessage, nameof(settings));
            }

            if (!recipe.IsValid(out string recipeMessage))
            {
                throw new ArgumentException(recipeMessage, nameof(recipe));
            }
        }

        private static void ValidateNamedInputs(
            MeshFilter meshFilter,
            MeshRenderer meshRenderer,
            string meshId,
            in PlanetRecipe recipe,
            PlanetMarchingCubesPaintSettings settings)
        {
            if (meshFilter == null)
            {
                throw new ArgumentNullException(nameof(meshFilter));
            }

            if (meshRenderer == null)
            {
                throw new ArgumentNullException(nameof(meshRenderer));
            }

            if (string.IsNullOrWhiteSpace(meshId))
            {
                throw new ArgumentException("meshId cannot be empty.", nameof(meshId));
            }

            if (!settings.Validate(out string settingsMessage))
            {
                throw new ArgumentException(settingsMessage, nameof(settings));
            }

            if (!recipe.IsValid(out string recipeMessage))
            {
                throw new ArgumentException(recipeMessage, nameof(recipe));
            }
        }

        private static void ValidateVertexCapacity(int vertexCount, PlanetMarchingCubesPaintSettings settings)
        {
            if (vertexCount > settings.meshVertexCapacity)
            {
                throw new InvalidOperationException(
                    "Marching Cubes paint vertex capacity is too small. sourceVertices=" + vertexCount +
                    " meshVertexCapacity=" + settings.meshVertexCapacity + ".");
            }
        }

        private void BuildMeshFromExtraction(
            Mesh mesh,
            Transform targetTransform,
            PlanetMarchingCubesVertex[] sourceVertices,
            int sourceVertexCount,
            in PlanetRecipe recipe,
            in PlanetPlacement placement,
            PlanetMarchingCubesPaintSettings settings,
            PlanetGpuShapeCell[] cells)
        {
            EnsureListCapacity(meshUploadPositions, sourceVertexCount);
            EnsureListCapacity(meshUploadNormals, sourceVertexCount);
            EnsureListCapacity(meshUploadUvs, sourceVertexCount);
            EnsureListCapacity(meshUploadColors, sourceVertexCount);
            EnsureListCapacity(meshUploadIndices, sourceVertexCount);
            meshUploadPositions.Clear();
            meshUploadNormals.Clear();
            meshUploadUvs.Clear();
            meshUploadColors.Clear();
            meshUploadIndices.Clear();

            float minRadius = float.MaxValue;
            float maxRadius = 0f;
            for (int i = 0; i < sourceVertexCount; i++)
            {
                float radius = ReadGridPosition(sourceVertices[i]).magnitude;
                minRadius = Mathf.Min(minRadius, radius);
                maxRadius = Mathf.Max(maxRadius, radius);
            }

            for (int i = 0; i < sourceVertexCount; i++)
            {
                PlanetMarchingCubesVertex source = sourceVertices[i];
                Vector3 gridPosition = ReadGridPosition(source);
                Vector3 worldPosition = PlanetCoordinateConverter.GridToWorld(gridPosition, in recipe, in placement);
                Vector3 localPosition = targetTransform != null
                    ? targetTransform.InverseTransformPoint(worldPosition)
                    : worldPosition;
                Vector3 normal = new Vector3(
                    source.normalAndDiagnostic.x,
                    source.normalAndDiagnostic.y,
                    source.normalAndDiagnostic.z).normalized;
                Vector3 worldNormal = placement.PlanetRotation * normal;
                Vector3 localNormal = targetTransform != null
                    ? targetTransform.InverseTransformDirection(worldNormal).normalized
                    : worldNormal.normalized;
                int triangleIndex = i / 3;
                int caseIndex = Mathf.RoundToInt(source.positionAndCase.w);
                float radiusValue = gridPosition.magnitude;
                Vector2 uv = EvaluateSurfaceAtlasUv(radiusValue, in recipe);
                float height01 = EvaluateHeight01(radiusValue, minRadius, maxRadius);

                meshUploadPositions.Add(localPosition);
                meshUploadNormals.Add(localNormal.sqrMagnitude > 0.0001f ? localNormal : Vector3.up);
                meshUploadUvs.Add(uv);
                meshUploadColors.Add(EvaluateColor(settings, gridPosition, normal, caseIndex, triangleIndex, minRadius, maxRadius, height01));
                meshUploadIndices.Add(i);
            }

            ApplyMeshData(mesh);
        }

        private void CopyCachedMeshIntoSlot(
            RuntimeChunkMesh slot,
            Mesh surfaceMesh,
            Mesh waterMesh,
            MeshFilter targetMeshFilter,
            MeshRenderer targetMeshRenderer,
            Material surfaceMaterial)
        {
            if (surfaceMesh != null)
            {
                EnsureNamedSurfaceBackSlot(
                    slot,
                    targetMeshFilter.transform,
                    targetMeshFilter.gameObject.layer,
                    SanitizeObjectName(slot.meshId) + "_Surface_Runtime",
                    targetMeshRenderer,
                    surfaceMaterial);
                CopyMeshData(surfaceMesh, slot.surfaceBackMesh);
                SwapNamedSurfaceSlot(slot);
            }
            else
            {
                ClearNamedSurfaceSlot(slot);
            }

            if (waterMesh != null)
            {
                EnsureNamedWaterBackSlot(
                    slot,
                    targetMeshFilter.transform,
                    targetMeshFilter.gameObject.layer,
                    SanitizeObjectName(slot.meshId) + "_Water_Runtime",
                    targetMeshRenderer);
                CopyMeshData(waterMesh, slot.waterBackMesh);
                SwapNamedWaterSlot(slot);
            }
            else
            {
                ClearNamedWaterSlot(slot);
            }
        }

        private void AddGlobalWaterTriangle(
            Transform targetTransform,
            Vector3 aDirection,
            Vector3 bDirection,
            Vector3 cDirection,
            in PlanetRecipe recipe,
            in PlanetPlacement placement)
        {
            AddGlobalWaterVertex(targetTransform, aDirection, in recipe, in placement);
            AddGlobalWaterVertex(targetTransform, bDirection, in recipe, in placement);
            AddGlobalWaterVertex(targetTransform, cDirection, in recipe, in placement);
        }

        private void AddGlobalWaterVertex(
            Transform targetTransform,
            Vector3 gridDirection,
            in PlanetRecipe recipe,
            in PlanetPlacement placement)
        {
            Vector3 gridPosition = gridDirection * recipe.GridRadius;
            Vector3 worldPosition = PlanetCoordinateConverter.GridToWorld(gridPosition, in recipe, in placement);
            Vector3 localPosition = targetTransform != null ? targetTransform.InverseTransformPoint(worldPosition) : worldPosition;
            Vector3 worldNormal = (placement.PlanetRotation * gridDirection).normalized;
            Vector3 localNormal = targetTransform != null ? targetTransform.InverseTransformDirection(worldNormal).normalized : worldNormal;
            int index = meshUploadPositions.Count;
            meshUploadPositions.Add(localPosition);
            meshUploadNormals.Add(localNormal);
            meshUploadUvs.Add(Vector2.zero);
            meshUploadColors.Add(Color.white);
            meshUploadIndices.Add(index);
        }

        private static Vector3 EvaluateGlobalWaterDirection(int latitude, int longitude)
        {
            float latitude01 = Mathf.Clamp01(latitude / (float)GlobalWaterLatitudeSegments);
            float longitude01 = Mathf.Repeat(longitude / (float)GlobalWaterLongitudeSegments, 1f);
            float theta = latitude01 * Mathf.PI;
            float phi = longitude01 * Mathf.PI * 2f;
            float ringRadius = Mathf.Sin(theta);
            return new Vector3(
                Mathf.Cos(phi) * ringRadius,
                Mathf.Cos(theta),
                Mathf.Sin(phi) * ringRadius).normalized;
        }

        private static PlanetGpuShapeCell[] BuildCells(in PlanetRecipe recipe)
        {
            PlanetGpuShapeCell[] cells = new PlanetGpuShapeCell[recipe.VoronoiDivision];
            PlanetGpuShapeCellBuilder.Build(in recipe, cells);
            return cells;
        }

        private void ApplyMeshData(Mesh mesh)
        {
            mesh.Clear();
            mesh.SetVertices(meshUploadPositions);
            mesh.SetNormals(meshUploadNormals);
            mesh.SetUVs(0, meshUploadUvs);
            mesh.SetColors(meshUploadColors);
            mesh.SetTriangles(meshUploadIndices, 0, true);
            mesh.RecalculateBounds();
        }

        private void CopyMeshData(Mesh source, Mesh target)
        {
            int vertexCount = source.vertexCount;
            EnsureListCapacity(meshUploadPositions, vertexCount);
            EnsureListCapacity(meshUploadNormals, vertexCount);
            EnsureListCapacity(meshUploadUvs, vertexCount);
            EnsureListCapacity(meshUploadColors, vertexCount);
            meshUploadPositions.Clear();
            meshUploadNormals.Clear();
            meshUploadUvs.Clear();
            meshUploadColors.Clear();

            source.GetVertices(meshUploadPositions);
            source.GetNormals(meshUploadNormals);
            source.GetUVs(0, meshUploadUvs);
            source.GetColors(meshUploadColors);

            target.Clear();
            target.indexFormat = vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            target.SetVertices(meshUploadPositions);
            if (meshUploadNormals.Count == vertexCount)
            {
                target.SetNormals(meshUploadNormals);
            }

            if (meshUploadUvs.Count == vertexCount)
            {
                target.SetUVs(0, meshUploadUvs);
            }

            if (meshUploadColors.Count == vertexCount)
            {
                target.SetColors(meshUploadColors);
            }

            int subMeshCount = Mathf.Max(1, source.subMeshCount);
            target.subMeshCount = subMeshCount;
            for (int i = 0; i < subMeshCount; i++)
            {
                int indexCount = (int)source.GetIndexCount(i);
                EnsureListCapacity(meshUploadIndices, indexCount);
                meshUploadIndices.Clear();
                source.GetIndices(meshUploadIndices, i);
                target.SetIndices(meshUploadIndices, source.GetTopology(i), i, false);
            }

            target.bounds = source.bounds;
        }

        private static PlanetMarchingCubesPaintResult BuildEmptyPaintResult(
            PlanetMarchingCubesPaintSettings settings)
        {
            return new PlanetMarchingCubesPaintResult(
                0,
                0,
                0,
                0L,
                settings.colorMode);
        }

        private PlanetMarchingCubesPaintResult BuildNamedPaintResult(
            RuntimeChunkMesh slot,
            PlanetMarchingCubesPaintSettings settings)
        {
            return new PlanetMarchingCubesPaintResult(
                slot.surfaceTriangleCount,
                slot.surfaceTriangleCount,
                slot.surfaceVertexCount,
                slot.surfaceEstimatedBytes,
                slot.waterTriangleCount,
                slot.waterVertexCount,
                slot.waterEstimatedBytes,
                settings.colorMode,
                slot.surfaceTriangleCount > 0 || slot.waterTriangleCount > 0 ? 1 : 0);
        }

        private int CountRuntimeChunkMeshes(bool water)
        {
            int count = 0;
            for (int i = 0; i < runtimeChunks.Count; i++)
            {
                if (water ? runtimeChunks[i].waterMesh != null : runtimeChunks[i].surfaceMesh != null)
                {
                    count++;
                }
            }

            return count;
        }

        private int SumRuntimeChunkVertices(bool water)
        {
            int count = 0;
            for (int i = 0; i < runtimeChunks.Count; i++)
            {
                count += water ? runtimeChunks[i].waterVertexCount : runtimeChunks[i].surfaceVertexCount;
            }

            return count;
        }

        private int SumRuntimeChunkTriangles(bool water)
        {
            int count = 0;
            for (int i = 0; i < runtimeChunks.Count; i++)
            {
                count += water ? runtimeChunks[i].waterTriangleCount : runtimeChunks[i].surfaceTriangleCount;
            }

            return count;
        }

        private long SumRuntimeChunkBytes(bool water)
        {
            long bytes = 0L;
            for (int i = 0; i < runtimeChunks.Count; i++)
            {
                bytes += water ? runtimeChunks[i].waterEstimatedBytes : runtimeChunks[i].surfaceEstimatedBytes;
            }

            return bytes;
        }

        private int FindRuntimeMeshIndex(string meshId)
        {
            for (int i = 0; i < runtimeChunks.Count; i++)
            {
                if (string.Equals(runtimeChunks[i].meshId, meshId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private RuntimeChunkMesh GetOrCreateRuntimeMeshSlot(string meshId, int cacheChunkId)
        {
            int existingIndex = FindRuntimeMeshIndex(meshId);
            if (existingIndex >= 0)
            {
                RuntimeChunkMesh existing = runtimeChunks[existingIndex];
                existing.chunkIndex = cacheChunkId;
                return existing;
            }

            RuntimeChunkMesh created = new RuntimeChunkMesh
            {
                meshId = meshId,
                chunkIndex = cacheChunkId
            };
            runtimeChunks.Add(created);
            return created;
        }

        private void EnsureNamedSurfaceSlot(
            RuntimeChunkMesh slot,
            Transform parent,
            int layer,
            string objectName,
            MeshRenderer sourceRenderer,
            Material surfaceMaterial)
        {
            if (slot.surfaceObject == null)
            {
                slot.surfaceObject = new GameObject(objectName);
                slot.surfaceObject.transform.SetParent(parent, false);
                slot.surfaceObject.layer = layer;
                slot.surfaceMeshFilter = slot.surfaceObject.AddComponent<MeshFilter>();
                slot.surfaceMeshRenderer = slot.surfaceObject.AddComponent<MeshRenderer>();
                slot.surfaceMesh = new Mesh { name = objectName + "_Mesh" };
                slot.surfaceMesh.MarkDynamic();
                slot.surfaceMeshFilter.sharedMesh = slot.surfaceMesh;
            }

            CopyRendererSettings(sourceRenderer, slot.surfaceMeshRenderer);
            slot.surfaceMeshRenderer.sharedMaterial = surfaceMaterial;
        }

        private void EnsureNamedSurfaceBackSlot(
            RuntimeChunkMesh slot,
            Transform parent,
            int layer,
            string objectName,
            MeshRenderer sourceRenderer,
            Material surfaceMaterial)
        {
            if (slot.surfaceBackObject == null)
            {
                slot.surfaceBackObject = new GameObject(objectName + "_Back");
                slot.surfaceBackObject.transform.SetParent(parent, false);
                slot.surfaceBackObject.layer = layer;
                slot.surfaceBackMeshFilter = slot.surfaceBackObject.AddComponent<MeshFilter>();
                slot.surfaceBackMeshRenderer = slot.surfaceBackObject.AddComponent<MeshRenderer>();
                slot.surfaceBackMesh = new Mesh { name = objectName + "_Back_Mesh" };
                slot.surfaceBackMesh.MarkDynamic();
                slot.surfaceBackMeshFilter.sharedMesh = slot.surfaceBackMesh;
            }

            slot.surfaceBackObject.SetActive(false);
            CopyRendererSettings(sourceRenderer, slot.surfaceBackMeshRenderer);
            slot.surfaceBackMeshRenderer.sharedMaterial = surfaceMaterial;
        }

        private static void SwapNamedSurfaceSlot(RuntimeChunkMesh slot)
        {
            if (slot.surfaceBackObject == null || slot.surfaceBackMesh == null)
            {
                return;
            }

            if (slot.surfaceObject != null)
            {
                slot.surfaceObject.SetActive(false);
            }

            slot.surfaceBackObject.SetActive(true);

            GameObject oldObject = slot.surfaceObject;
            MeshFilter oldFilter = slot.surfaceMeshFilter;
            MeshRenderer oldRenderer = slot.surfaceMeshRenderer;
            Mesh oldMesh = slot.surfaceMesh;

            slot.surfaceObject = slot.surfaceBackObject;
            slot.surfaceMeshFilter = slot.surfaceBackMeshFilter;
            slot.surfaceMeshRenderer = slot.surfaceBackMeshRenderer;
            slot.surfaceMesh = slot.surfaceBackMesh;

            slot.surfaceBackObject = oldObject;
            slot.surfaceBackMeshFilter = oldFilter;
            slot.surfaceBackMeshRenderer = oldRenderer;
            slot.surfaceBackMesh = oldMesh;
        }

        private void EnsureNamedWaterSlot(
            RuntimeChunkMesh slot,
            Transform parent,
            int layer,
            string objectName,
            MeshRenderer sourceRenderer)
        {
            if (slot.waterObject == null)
            {
                slot.waterObject = new GameObject(objectName);
                slot.waterObject.transform.SetParent(parent, false);
                slot.waterObject.layer = layer;
                slot.waterMeshFilter = slot.waterObject.AddComponent<MeshFilter>();
                slot.waterMeshRenderer = slot.waterObject.AddComponent<MeshRenderer>();
                slot.waterMesh = new Mesh { name = objectName + "_Mesh" };
                slot.waterMesh.MarkDynamic();
                slot.waterMeshFilter.sharedMesh = slot.waterMesh;
            }

            CopyRendererSettings(sourceRenderer, slot.waterMeshRenderer);
            slot.waterMeshRenderer.sharedMaterial = ResolveWaterMaterial();
        }

        private void EnsureNamedWaterBackSlot(
            RuntimeChunkMesh slot,
            Transform parent,
            int layer,
            string objectName,
            MeshRenderer sourceRenderer)
        {
            if (slot.waterBackObject == null)
            {
                slot.waterBackObject = new GameObject(objectName + "_Back");
                slot.waterBackObject.transform.SetParent(parent, false);
                slot.waterBackObject.layer = layer;
                slot.waterBackMeshFilter = slot.waterBackObject.AddComponent<MeshFilter>();
                slot.waterBackMeshRenderer = slot.waterBackObject.AddComponent<MeshRenderer>();
                slot.waterBackMesh = new Mesh { name = objectName + "_Back_Mesh" };
                slot.waterBackMesh.MarkDynamic();
                slot.waterBackMeshFilter.sharedMesh = slot.waterBackMesh;
            }

            slot.waterBackObject.SetActive(false);
            CopyRendererSettings(sourceRenderer, slot.waterBackMeshRenderer);
            slot.waterBackMeshRenderer.sharedMaterial = ResolveWaterMaterial();
        }

        private static void SwapNamedWaterSlot(RuntimeChunkMesh slot)
        {
            if (slot.waterBackObject == null || slot.waterBackMesh == null)
            {
                return;
            }

            if (slot.waterObject != null)
            {
                slot.waterObject.SetActive(false);
            }

            slot.waterBackObject.SetActive(true);

            GameObject oldObject = slot.waterObject;
            MeshFilter oldFilter = slot.waterMeshFilter;
            MeshRenderer oldRenderer = slot.waterMeshRenderer;
            Mesh oldMesh = slot.waterMesh;

            slot.waterObject = slot.waterBackObject;
            slot.waterMeshFilter = slot.waterBackMeshFilter;
            slot.waterMeshRenderer = slot.waterBackMeshRenderer;
            slot.waterMesh = slot.waterBackMesh;

            slot.waterBackObject = oldObject;
            slot.waterBackMeshFilter = oldFilter;
            slot.waterBackMeshRenderer = oldRenderer;
            slot.waterBackMesh = oldMesh;
        }

        private static void ClearNamedSurfaceSlot(RuntimeChunkMesh slot)
        {
            if (slot.surfaceMesh != null)
            {
                DestroyRuntimeObject(slot.surfaceMesh);
                slot.surfaceMesh = null;
            }

            if (slot.surfaceObject != null)
            {
                DestroyRuntimeObject(slot.surfaceObject);
                slot.surfaceObject = null;
                slot.surfaceMeshFilter = null;
                slot.surfaceMeshRenderer = null;
            }

            if (slot.surfaceBackMesh != null)
            {
                DestroyRuntimeObject(slot.surfaceBackMesh);
                slot.surfaceBackMesh = null;
            }

            if (slot.surfaceBackObject != null)
            {
                DestroyRuntimeObject(slot.surfaceBackObject);
                slot.surfaceBackObject = null;
                slot.surfaceBackMeshFilter = null;
                slot.surfaceBackMeshRenderer = null;
            }

            slot.surfaceTriangleCount = 0;
            slot.surfaceVertexCount = 0;
            slot.surfaceEstimatedBytes = 0L;
        }

        private static void ClearNamedWaterSlot(RuntimeChunkMesh slot)
        {
            if (slot.waterMesh != null)
            {
                DestroyRuntimeObject(slot.waterMesh);
                slot.waterMesh = null;
            }

            if (slot.waterObject != null)
            {
                DestroyRuntimeObject(slot.waterObject);
                slot.waterObject = null;
                slot.waterMeshFilter = null;
                slot.waterMeshRenderer = null;
            }

            if (slot.waterBackMesh != null)
            {
                DestroyRuntimeObject(slot.waterBackMesh);
                slot.waterBackMesh = null;
            }

            if (slot.waterBackObject != null)
            {
                DestroyRuntimeObject(slot.waterBackObject);
                slot.waterBackObject = null;
                slot.waterBackMeshFilter = null;
                slot.waterBackMeshRenderer = null;
            }

            slot.waterTriangleCount = 0;
            slot.waterVertexCount = 0;
            slot.waterEstimatedBytes = 0L;
        }

        private static void UpdateRuntimeMeshSlotMetrics(RuntimeChunkMesh slot)
        {
            slot.surfaceVertexCount = slot.surfaceMesh != null ? slot.surfaceMesh.vertexCount : 0;
            slot.surfaceTriangleCount = slot.surfaceMesh != null ? (int)slot.surfaceMesh.GetIndexCount(0) / 3 : 0;
            slot.surfaceEstimatedBytes = PlanetMarchingCubesPaintResult.CalculateMeshEstimatedBytes(
                slot.surfaceVertexCount,
                slot.surfaceTriangleCount);
            slot.waterVertexCount = slot.waterMesh != null ? slot.waterMesh.vertexCount : 0;
            slot.waterTriangleCount = slot.waterMesh != null ? (int)slot.waterMesh.GetIndexCount(0) / 3 : 0;
            slot.waterEstimatedBytes = PlanetMarchingCubesPaintResult.CalculateMeshEstimatedBytes(
                slot.waterVertexCount,
                slot.waterTriangleCount);
        }

        private void ReleaseMeshOnly(MeshFilter meshFilter)
        {
            if (runtimeMesh == null)
            {
                return;
            }

            if (meshFilter != null && meshFilter.sharedMesh == runtimeMesh)
            {
                meshFilter.sharedMesh = null;
            }

            DestroyRuntimeObject(runtimeMesh);
            runtimeMesh = null;
        }

        private void ReleaseWater()
        {
            if (runtimeWaterMesh != null)
            {
                DestroyRuntimeObject(runtimeWaterMesh);
                runtimeWaterMesh = null;
            }

            if (runtimeWaterMaterial != null)
            {
                DestroyRuntimeObject(runtimeWaterMaterial);
                runtimeWaterMaterial = null;
            }

            if (runtimeWaterObject != null)
            {
                DestroyRuntimeObject(runtimeWaterObject);
                runtimeWaterObject = null;
            }
        }

        private void ReleaseChunks()
        {
            for (int i = 0; i < runtimeChunks.Count; i++)
            {
                ClearNamedSurfaceSlot(runtimeChunks[i]);
                ClearNamedWaterSlot(runtimeChunks[i]);
            }

            runtimeChunks.Clear();
        }

        private Material ResolveMaterial(Material materialOverride, PlanetMarchingCubesPaintColorMode colorMode, PlanetGpuShapeCell[] cells)
        {
            if (CanUseMaterialOverride(materialOverride, colorMode))
            {
                ApplyMaterialProperties(materialOverride, colorMode, cells);
                return materialOverride;
            }

            if (runtimeMaterial != null)
            {
                ApplyMaterialProperties(runtimeMaterial, colorMode, cells);
                return runtimeMaterial;
            }

            Material defaultSurfaceMaterial = Resources.Load<Material>(DefaultSurfaceMaterialResourceName);
            if (defaultSurfaceMaterial != null)
            {
                runtimeMaterial = new Material(defaultSurfaceMaterial)
                {
                    name = "PlanetMarchingCubesPaint_SurfaceMaterial_Runtime"
                };
                ApplyMaterialProperties(runtimeMaterial, colorMode, cells);
                return runtimeMaterial;
            }

            Shader shader = Shader.Find(SurfaceShaderName);
            if (shader == null)
            {
                shader = Shader.Find(UrpUnlitShaderName);
            }

            if (shader == null)
            {
                throw new InvalidOperationException("No planet surface material shader was found for PlanetMarchingCubesMeshPainter.");
            }

            runtimeMaterial = new Material(shader)
            {
                name = "PlanetMarchingCubesPaint_SurfaceMaterial_Runtime"
            };

            if (shader.name == UrpUnlitShaderName)
            {
                runtimeMaterial.color = Color.white;
            }

            ApplyMaterialProperties(runtimeMaterial, colorMode, cells);
            return runtimeMaterial;
        }

        private static bool CanUseMaterialOverride(Material materialOverride, PlanetMarchingCubesPaintColorMode colorMode)
        {
            if (materialOverride == null)
            {
                return false;
            }

            if (colorMode != PlanetMarchingCubesPaintColorMode.PlanetSurfaceAtlas)
            {
                return true;
            }

            return materialOverride.HasProperty(SurfaceAtlasTexturePropertyName) ||
                   materialOverride.HasProperty(BaseMapTexturePropertyName);
        }

        private Material ResolveWaterMaterial()
        {
            if (runtimeWaterMaterial != null)
            {
                return runtimeWaterMaterial;
            }

            Material defaultWaterMaterial = Resources.Load<Material>(DefaultOceanMaterialResourceName);
            if (defaultWaterMaterial != null)
            {
                runtimeWaterMaterial = new Material(defaultWaterMaterial)
                {
                    name = "PlanetMarchingCubesPaint_WaterMaterial_Runtime"
                };
                return runtimeWaterMaterial;
            }

            Shader shader = Shader.Find(UrpUnlitShaderName);
            if (shader == null)
            {
                throw new InvalidOperationException("No ocean material or fallback shader was found for PlanetMarchingCubesMeshPainter.");
            }

            runtimeWaterMaterial = new Material(shader)
            {
                name = "PlanetMarchingCubesPaint_WaterMaterial_Runtime",
                color = new Color(0.08f, 0.42f, 0.72f, 0.78f)
            };
            return runtimeWaterMaterial;
        }

        private void ApplyMaterialProperties(Material material, PlanetMarchingCubesPaintColorMode colorMode, PlanetGpuShapeCell[] cells)
        {
            if (material == null)
            {
                return;
            }

            bool useSurfaceAtlas = colorMode == PlanetMarchingCubesPaintColorMode.PlanetSurfaceAtlas;
            bool supportsRuntimeSurfaceAtlas = material.HasProperty(SurfaceAtlasTexturePropertyName);
            if (useSurfaceAtlas && supportsRuntimeSurfaceAtlas)
            {
                EnsureSurfaceAtlas(cells);
            }

            if (material.HasProperty(UseSurfaceAtlasPropertyName))
            {
                material.SetFloat(UseSurfaceAtlasPropertyName, useSurfaceAtlas ? 1f : 0f);
            }

            if (useSurfaceAtlas && supportsRuntimeSurfaceAtlas && runtimeSurfaceAtlas != null)
            {
                material.SetTexture(SurfaceAtlasTexturePropertyName, runtimeSurfaceAtlas);
            }
        }

        private void EnsureSurfaceAtlas(PlanetGpuShapeCell[] cells)
        {
            if (runtimeSurfaceAtlas != null)
            {
                return;
            }

            int cellCount = cells == null ? 0 : cells.Length;
            int atlasWidth = Mathf.Max(1, cellCount);
            runtimeSurfaceAtlas = new Texture2D(atlasWidth, SurfaceAtlasResolution, TextureFormat.RGBA32, false, true)
            {
                name = "PlanetMarchingCubesPaint_SurfaceAtlas_Runtime",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color32[] pixels = new Color32[atlasWidth * SurfaceAtlasResolution];
            for (int x = 0; x < atlasWidth; x++)
            {
                for (int y = 0; y < SurfaceAtlasResolution; y++)
                {
                    float t = y / (float)(SurfaceAtlasResolution - 1);
                    pixels[y * atlasWidth + x] = EvaluateSurfaceCellGradient(t, x);
                }
            }

            runtimeSurfaceAtlas.SetPixels32(pixels);
            runtimeSurfaceAtlas.Apply(false, false);
        }

        private static void CopyRendererSettings(MeshRenderer source, MeshRenderer target)
        {
            if (source == null || target == null)
            {
                return;
            }

            target.shadowCastingMode = source.shadowCastingMode;
            target.receiveShadows = source.receiveShadows;
            target.motionVectorGenerationMode = source.motionVectorGenerationMode;
            target.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
        }

        private static Vector3 ReadGridPosition(PlanetMarchingCubesVertex vertex)
        {
            Vector4 packedPosition = vertex.positionAndCase;
            return new Vector3(packedPosition.x, packedPosition.y, packedPosition.z);
        }

        private static Vector2 EvaluateSurfaceAtlasUv(float radius, in PlanetRecipe recipe)
        {
            float surfaceOffset = radius - recipe.GridRadius;
            float safeRadius = Mathf.Max(recipe.GridRadius, 0.0001f);
            float minHeightAtlasOffset =
                -safeRadius * Mathf.Max(recipe.OceanDepth, recipe.MinimumOceanDepth) -
                safeRadius * Mathf.Max(0f, recipe.SurfaceNoiseAmplitude);
            float maxHeightAtlasOffset =
                safeRadius * recipe.MaxLandElevation * recipe.MaxHeightModifier +
                safeRadius * Mathf.Max(0f, recipe.MountainBiomeHeight) +
                safeRadius * Mathf.Max(0f, recipe.SurfaceNoiseAmplitude);
            maxHeightAtlasOffset *= SurfaceAtlasLandRangeScale;

            if (surfaceOffset <= 0f)
            {
                float depthRange = Mathf.Max(0.0001f, -minHeightAtlasOffset);
                float underwaterHeight = Mathf.Clamp01((surfaceOffset - minHeightAtlasOffset) / depthRange);
                return new Vector2(0.5f, Mathf.Lerp(0f, SurfaceAtlasSeaLevelV, underwaterHeight));
            }

            float landRange = Mathf.Max(0.0001f, maxHeightAtlasOffset);
            float landHeight = Mathf.Clamp01(surfaceOffset / landRange);
            return new Vector2(0.5f, Mathf.Lerp(SurfaceAtlasSeaLevelV, 1f, landHeight));
        }

        private static float EvaluateHeight01(float radius, float minRadius, float maxRadius)
        {
            if (maxRadius - minRadius <= 0.0001f)
            {
                return 0.5f;
            }

            return Mathf.InverseLerp(minRadius, maxRadius, radius);
        }

        private static Color32 EvaluateColor(
            PlanetMarchingCubesPaintSettings settings,
            Vector3 position,
            Vector3 normal,
            int caseIndex,
            int triangleIndex,
            float minRadius,
            float maxRadius,
            float height01)
        {
            _ = position;
            _ = minRadius;
            _ = maxRadius;
            switch (settings.colorMode)
            {
                case PlanetMarchingCubesPaintColorMode.PlanetSurfaceAtlas:
                    return Color.white;
                case PlanetMarchingCubesPaintColorMode.FlatNormalColor:
                    return new Color(
                        normal.x * 0.5f + 0.5f,
                        normal.y * 0.5f + 0.5f,
                        normal.z * 0.5f + 0.5f,
                        1f);
                case PlanetMarchingCubesPaintColorMode.HeightColor:
                    return Color.Lerp(new Color(0.1f, 0.35f, 1f, 1f), new Color(0.95f, 1f, 0.35f, 1f), height01);
                case PlanetMarchingCubesPaintColorMode.CaseIndexPalette:
                    return Palette(caseIndex);
                case PlanetMarchingCubesPaintColorMode.TrianglePalette:
                    return Palette(triangleIndex);
                case PlanetMarchingCubesPaintColorMode.SolidColor:
                    return settings.solidColor;
                default:
                    return Color.white;
            }
        }

        private static Color32 EvaluateSurfaceCellGradient(float height01, int cellIndex)
        {
            Color baseColor = EvaluatePlanetSurfacePalette(height01);
            float valueNoise = Hash01((uint)cellIndex, 0x6ac690c5u);
            float warmthNoise = Hash01((uint)cellIndex, 0x9e3779b9u) - 0.5f;
            float value = Mathf.Lerp(0.94f, 1.06f, valueNoise);
            return new Color(
                Mathf.Clamp01(baseColor.r * value + warmthNoise * 0.025f),
                Mathf.Clamp01(baseColor.g * value),
                Mathf.Clamp01(baseColor.b * value - warmthNoise * 0.020f),
                1f);
        }

        private static Color EvaluatePlanetSurfacePalette(float height01)
        {
            height01 = Mathf.Clamp01(height01);
            Color deepPink = new Color(0.78f, 0.36f, 0.55f, 1f);
            Color salmon = new Color(0.72f, 0.42f, 0.45f, 1f);
            Color darkRedBrown = new Color(0.28f, 0.17f, 0.15f, 1f);
            Color roseRed = new Color(0.62f, 0.32f, 0.38f, 1f);
            Color sand = new Color(0.78f, 0.68f, 0.44f, 1f);
            Color paleYellow = new Color(0.88f, 0.80f, 0.56f, 1f);
            Color brightGreen = new Color(0.42f, 0.62f, 0.29f, 1f);
            Color darkGreen = new Color(0.17f, 0.40f, 0.19f, 1f);
            Color brown = new Color(0.43f, 0.31f, 0.20f, 1f);
            Color darkGrey = new Color(0.32f, 0.32f, 0.30f, 1f);
            Color grey = new Color(0.55f, 0.55f, 0.51f, 1f);
            Color lightGrey = new Color(0.78f, 0.78f, 0.74f, 1f);
            Color snow = new Color(0.93f, 0.93f, 0.89f, 1f);

            if (height01 < 0.12f) return Color.Lerp(deepPink, salmon, height01 / 0.12f);
            if (height01 < 0.24f) return Color.Lerp(salmon, darkRedBrown, (height01 - 0.12f) / 0.12f);
            if (height01 < 0.36f) return Color.Lerp(darkRedBrown, roseRed, (height01 - 0.24f) / 0.12f);
            if (height01 < 0.5f) return Color.Lerp(roseRed, sand, (height01 - 0.36f) / 0.14f);
            if (height01 < 0.58f) return Color.Lerp(sand, paleYellow, (height01 - 0.5f) / 0.08f);
            if (height01 < 0.68f) return Color.Lerp(paleYellow, brightGreen, (height01 - 0.58f) / 0.10f);
            if (height01 < 0.78f) return Color.Lerp(brightGreen, darkGreen, (height01 - 0.68f) / 0.10f);
            if (height01 < 0.86f) return Color.Lerp(darkGreen, brown, (height01 - 0.78f) / 0.08f);
            if (height01 < 0.93f) return Color.Lerp(brown, darkGrey, (height01 - 0.86f) / 0.07f);
            if (height01 < 0.97f) return Color.Lerp(darkGrey, grey, (height01 - 0.93f) / 0.04f);
            if (height01 < 0.99f) return Color.Lerp(grey, lightGrey, (height01 - 0.97f) / 0.02f);
            return Color.Lerp(lightGrey, snow, (height01 - 0.99f) / 0.01f);
        }

        private static float Hash01(uint value, uint salt)
        {
            uint hash = value ^ salt;
            hash ^= hash >> 16;
            hash *= 0x7feb352dU;
            hash ^= hash >> 15;
            hash *= 0x846ca68bU;
            hash ^= hash >> 16;
            return (hash & 0x00ffffffU) / 16777215f;
        }

        private static Color32 Palette(int value)
        {
            uint hash = (uint)value;
            hash ^= hash >> 16;
            hash *= 0x7feb352dU;
            hash ^= hash >> 15;
            hash *= 0x846ca68bU;
            hash ^= hash >> 16;
            return new Color32(
                (byte)(80 + (hash & 127U)),
                (byte)(80 + ((hash >> 8) & 127U)),
                (byte)(80 + ((hash >> 16) & 127U)),
                255);
        }

        private static string SanitizeObjectName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "chunk";
            }

            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        private static void DestroyIfNotOwned(Mesh mesh, Mesh ownedMesh)
        {
            if (mesh != null && mesh != ownedMesh)
            {
                DestroyRuntimeObject(mesh);
            }
        }

        private static void EnsureListCapacity<T>(List<T> list, int capacity)
        {
            if (list.Capacity < capacity)
            {
                list.Capacity = capacity;
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
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
