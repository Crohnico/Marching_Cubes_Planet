using System;
using System.Collections.Generic;
using MarchingCubesPlanet.Coordinates;
using MarchingCubesPlanet.Shape;
using MarchingCubesPlanet.TrianglePools;
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
        private const uint PlanetSurfaceMeshId = 1u;
        private const uint PlanetChunkSurfaceMeshIdBase = 0x10000000u;
        private const int DefaultMeshUploadBufferCapacity = 65536;
        private const int GlobalWaterLongitudeSegments = 128;
        private const int GlobalWaterLatitudeSegments = 64;

        private Mesh runtimeMesh;
        private Mesh runtimeWaterMesh;
        private Material runtimeMaterial;
        private Material runtimeWaterMaterial;
        private Texture2D runtimeSurfaceAtlas;
        private GameObject runtimeWaterObject;
        private readonly List<RuntimeChunkMesh> runtimeChunks = new List<RuntimeChunkMesh>();
        private readonly PlanetTrianglePoolWriter trianglePoolWriter =
            new PlanetTrianglePoolWriter(PlanetTrianglePoolRegistry.Environment);
        private readonly List<Vector3> meshUploadPositions = new List<Vector3>(DefaultMeshUploadBufferCapacity);
        private readonly List<Vector3> meshUploadNormals = new List<Vector3>(DefaultMeshUploadBufferCapacity);
        private readonly List<Vector2> meshUploadUvs = new List<Vector2>(DefaultMeshUploadBufferCapacity);
        private readonly List<Color32> meshUploadColors = new List<Color32>(DefaultMeshUploadBufferCapacity);
        private readonly List<int> meshUploadIndices = new List<int>(DefaultMeshUploadBufferCapacity);
        private readonly List<PlanetTriangleGpuVertex> gpuUploadVertices = new List<PlanetTriangleGpuVertex>(DefaultMeshUploadBufferCapacity);
        private readonly List<Vector3> cacheGpuVertices = new List<Vector3>(DefaultMeshUploadBufferCapacity);
        private readonly List<Vector3> cacheGpuNormals = new List<Vector3>(DefaultMeshUploadBufferCapacity);
        private readonly List<Vector2> cacheGpuUvs = new List<Vector2>(DefaultMeshUploadBufferCapacity);
        private readonly List<Color32> cacheGpuColors = new List<Color32>(DefaultMeshUploadBufferCapacity);
        private readonly List<int> cacheGpuIndices = new List<int>(DefaultMeshUploadBufferCapacity);
        private readonly Vector3[] waterClipBuffer = new Vector3[4];
        private int[] chunkTriangleCountsBuffer = Array.Empty<int>();
        private int[] chunkStartsBuffer = Array.Empty<int>();
        private int[] chunkWriteCursorsBuffer = Array.Empty<int>();
        private int[] sourceTriangleIndicesBuffer = Array.Empty<int>();

        public Mesh RuntimeMesh => runtimeMesh;
        public Mesh RuntimeWaterMesh => runtimeWaterMesh;
        public Material RuntimeMaterial => runtimeMaterial;
        public Material RuntimeWaterMaterial => runtimeWaterMaterial;
        public Texture2D RuntimeSurfaceAtlas => runtimeSurfaceAtlas;
        public bool HasVisibleGpuWater => PlanetTrianglePoolRegistry.Environment.GpuBackend.HasVisibleWaterData;
        public long RuntimeGpuWaterEstimatedBytes => PlanetTrianglePoolRegistry.Environment.GpuBackend.EstimatedWaterGpuBytes;
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
        public int RuntimeChunkMeshCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < runtimeChunks.Count; i++)
                {
                    if (runtimeChunks[i].surfaceMesh != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int RuntimeChunkWaterMeshCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < runtimeChunks.Count; i++)
                {
                    if (runtimeChunks[i].waterMesh != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int RuntimeChunkVertexCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < runtimeChunks.Count; i++)
                {
                    count += runtimeChunks[i].surfaceVertexCount;
                }

                return count;
            }
        }

        public int RuntimeChunkTriangleCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < runtimeChunks.Count; i++)
                {
                    count += runtimeChunks[i].surfaceTriangleCount;
                }

                return count;
            }
        }

        public int RuntimeChunkWaterVertexCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < runtimeChunks.Count; i++)
                {
                    count += runtimeChunks[i].waterVertexCount;
                }

                return count;
            }
        }

        public int RuntimeChunkWaterTriangleCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < runtimeChunks.Count; i++)
                {
                    count += runtimeChunks[i].waterTriangleCount;
                }

                return count;
            }
        }

        public long RuntimeChunkMeshEstimatedBytes
        {
            get
            {
                long bytes = 0L;
                for (int i = 0; i < runtimeChunks.Count; i++)
                {
                    bytes += runtimeChunks[i].surfaceEstimatedBytes;
                }

                return bytes;
            }
        }

        public long RuntimeChunkWaterMeshEstimatedBytes
        {
            get
            {
                long bytes = 0L;
                for (int i = 0; i < runtimeChunks.Count; i++)
                {
                    bytes += runtimeChunks[i].waterEstimatedBytes;
                }

                return bytes;
            }
        }
        private sealed class RuntimeChunkMesh
        {
            public string meshId;
            public int chunkIndex;
            public GameObject surfaceObject;
            public MeshFilter surfaceMeshFilter;
            public MeshRenderer surfaceMeshRenderer;
            public Mesh surfaceMesh;
            public GameObject waterObject;
            public MeshFilter waterMeshFilter;
            public MeshRenderer waterMeshRenderer;
            public Mesh waterMesh;
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

            if (!settings.Validate(out string message))
            {
                throw new ArgumentException(message, nameof(settings));
            }

            if (!recipe.IsValid(out string recipeMessage))
            {
                throw new ArgumentException(recipeMessage, nameof(recipe));
            }

            int sourceVertexCount = source.VertexCount - source.VertexCount % 3;
            int sourceTriangleCount = sourceVertexCount / 3;
            Vector3 priorityOriginWorld = PlanetTrianglePoolRegistry.PriorityOriginWorld;
            PlanetRecipe recipeCopy = recipe;
            PlanetPlacement placementCopy = placement;
            PlanetMarchingCubesVertex[] sourceVertices = source.Vertices;
            PlanetTriangleDrawResult drawResult = trianglePoolWriter.Draw(
                sourceTriangleCount,
                triangleIndex => EvaluateTriangleScore(
                    sourceVertices,
                    triangleIndex,
                    recipeCopy,
                    placementCopy,
                    priorityOriginWorld),
                triangleIndex => EvaluateOutputTriangleCost(
                    sourceVertices,
                    triangleIndex,
                    recipeCopy.GridRadius),
                PlanetTriangleOwnerId.PlanetSurfaceValue,
                PlanetSurfaceMeshId);
            int paintedTriangleCount = drawResult.SelectedSourceTriangleCount;
            int paintedVertexCount = paintedTriangleCount * 3;
            PlanetGpuShapeCell[] cells = new PlanetGpuShapeCell[recipe.VoronoiDivision];
            PlanetGpuShapeCellBuilder.Build(in recipe, cells);

            Release(meshFilter, meshRenderer);

            runtimeMesh = new Mesh
            {
                name = "PlanetMarchingCubesPaint_Mesh_Runtime",
                indexFormat = paintedVertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
            };

            if (paintedVertexCount > 0)
            {
                BuildMesh(
                    runtimeMesh,
                    meshFilter.transform,
                    source.Vertices,
                    sourceVertexCount,
                    trianglePoolWriter,
                    paintedVertexCount,
                    cells,
                    in recipe,
                    in placement,
                    settings);
            }

            meshFilter.sharedMesh = runtimeMesh;
            meshRenderer.sharedMaterial = ResolveMaterial(materialOverride, settings.colorMode, cells);

            BuildWaterMesh(
                meshFilter,
                meshRenderer,
                source.Vertices,
                sourceVertexCount,
                trianglePoolWriter,
                in recipe,
                in placement,
                settings,
                out int waterVertexCount,
                out int waterTriangleCount);

            long estimatedBytes = PlanetMarchingCubesPaintResult.CalculateMeshEstimatedBytes(
                paintedVertexCount,
                paintedTriangleCount);
            long waterEstimatedBytes = PlanetMarchingCubesPaintResult.CalculateMeshEstimatedBytes(
                waterVertexCount,
                waterTriangleCount);
            return new PlanetMarchingCubesPaintResult(
                sourceTriangleCount,
                paintedTriangleCount,
                paintedVertexCount,
                estimatedBytes,
                waterTriangleCount,
                waterVertexCount,
                waterEstimatedBytes,
                settings.colorMode);
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

            if (!settings.Validate(out string message))
            {
                throw new ArgumentException(message, nameof(settings));
            }

            if (!recipe.IsValid(out string recipeMessage))
            {
                throw new ArgumentException(recipeMessage, nameof(recipe));
            }

            int triangleCount = GlobalWaterLatitudeSegments * GlobalWaterLongitudeSegments * 2;
            int vertexCount = triangleCount * 3;
            EnsureListCapacity(gpuUploadVertices, vertexCount);
            gpuUploadVertices.Clear();
            Bounds waterBounds = default;
            bool hasWaterBounds = false;
            Color32 waterColor = Color.white;

            for (int latitude = 0; latitude < GlobalWaterLatitudeSegments; latitude++)
            {
                for (int longitude = 0; longitude < GlobalWaterLongitudeSegments; longitude++)
                {
                    Vector3 upperLeft = EvaluateGlobalWaterDirection(latitude, longitude);
                    Vector3 upperRight = EvaluateGlobalWaterDirection(latitude, longitude + 1);
                    Vector3 lowerLeft = EvaluateGlobalWaterDirection(latitude + 1, longitude);
                    Vector3 lowerRight = EvaluateGlobalWaterDirection(latitude + 1, longitude + 1);

                    AddGlobalWaterGpuTriangle(
                        upperLeft,
                        upperRight,
                        lowerLeft,
                        waterColor,
                        in recipe,
                        in placement,
                        ref waterBounds,
                        ref hasWaterBounds);
                    AddGlobalWaterGpuTriangle(
                        upperRight,
                        lowerRight,
                        lowerLeft,
                        waterColor,
                        in recipe,
                        in placement,
                        ref waterBounds,
                        ref hasWaterBounds);
                }
            }

            bool published = PlanetTrianglePoolRegistry.Environment.GpuBackend.PublishWater(
                gpuUploadVertices,
                gpuUploadVertices.Count,
                hasWaterBounds ? waterBounds : new Bounds(Vector3.zero, Vector3.one),
                ResolveWaterMaterial());
            if (!published)
            {
                return new PlanetMarchingCubesPaintResult(0, 0, 0, 0L, 0, 0, 0L, settings.colorMode);
            }

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

        private void AddGlobalWaterGpuTriangle(
            Vector3 aDirection,
            Vector3 bDirection,
            Vector3 cDirection,
            Color32 color,
            in PlanetRecipe recipe,
            in PlanetPlacement placement,
            ref Bounds bounds,
            ref bool hasBounds)
        {
            AddGlobalWaterGpuVertex(aDirection, color, in recipe, in placement, ref bounds, ref hasBounds);
            AddGlobalWaterGpuVertex(bDirection, color, in recipe, in placement, ref bounds, ref hasBounds);
            AddGlobalWaterGpuVertex(cDirection, color, in recipe, in placement, ref bounds, ref hasBounds);
        }

        private void AddGlobalWaterGpuVertex(
            Vector3 gridDirection,
            Color32 color,
            in PlanetRecipe recipe,
            in PlanetPlacement placement,
            ref Bounds bounds,
            ref bool hasBounds)
        {
            Vector3 gridPosition = gridDirection * recipe.GridRadius;
            Vector3 worldPosition = PlanetCoordinateConverter.GridToWorld(gridPosition, in recipe, in placement);
            Vector3 worldNormal = (placement.PlanetRotation * gridDirection).normalized;
            gpuUploadVertices.Add(new PlanetTriangleGpuVertex(worldPosition, worldNormal, Vector2.zero, color, true));
            IncludePointInBounds(ref bounds, ref hasBounds, worldPosition);
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

        public PlanetMarchingCubesPaintResult PaintChunks(
            MeshFilter meshFilter,
            MeshRenderer meshRenderer,
            Material materialOverride,
            PlanetMarchingCubesExtractionResult source,
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

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (!settings.Validate(out string message))
            {
                throw new ArgumentException(message, nameof(settings));
            }

            if (!recipe.IsValid(out string recipeMessage))
            {
                throw new ArgumentException(recipeMessage, nameof(recipe));
            }

            int sourceVertexCount = source.VertexCount - source.VertexCount % 3;
            int sourceTriangleCount = sourceVertexCount / 3;
            PlanetMarchingCubesVertex[] sourceVertices = source.Vertices;
            PlanetGpuShapeCell[] cells = new PlanetGpuShapeCell[recipe.VoronoiDivision];
            PlanetGpuShapeCellBuilder.Build(in recipe, cells);

            Release(meshFilter, meshRenderer);
            meshFilter.sharedMesh = null;
            meshRenderer.sharedMaterial = null;

            if (sourceTriangleCount <= 0)
            {
                return new PlanetMarchingCubesPaintResult(
                    0,
                    0,
                    0,
                    0L,
                    0,
                    0,
                    0L,
                    settings.colorMode,
                    0);
            }

            Material surfaceMaterial = ResolveMaterial(materialOverride, settings.colorMode, cells);
            int maxChunkIndex = FindMaxSourceChunkIndex(sourceVertices, sourceTriangleCount);
            int chunkSlotCount = maxChunkIndex + 1;
            int[] chunkTriangleCounts = EnsureIntBuffer(ref chunkTriangleCountsBuffer, chunkSlotCount);
            Array.Clear(chunkTriangleCounts, 0, chunkSlotCount);

            for (int sourceTriangleIndex = 0; sourceTriangleIndex < sourceTriangleCount; sourceTriangleIndex++)
            {
                int chunkIndex = ReadSourceChunkIndex(sourceVertices, sourceTriangleIndex);
                chunkTriangleCounts[chunkIndex]++;
            }

            int[] chunkStarts = EnsureIntBuffer(ref chunkStartsBuffer, chunkSlotCount);
            Array.Clear(chunkStarts, 0, chunkSlotCount);
            int runningStart = 0;
            for (int chunkIndex = 0; chunkIndex < chunkSlotCount; chunkIndex++)
            {
                chunkStarts[chunkIndex] = runningStart;
                runningStart += chunkTriangleCounts[chunkIndex];
            }

            int[] chunkWriteCursors = EnsureIntBuffer(ref chunkWriteCursorsBuffer, chunkSlotCount);
            Array.Clear(chunkWriteCursors, 0, chunkSlotCount);
            int[] sourceTriangleIndices = EnsureIntBuffer(ref sourceTriangleIndicesBuffer, sourceTriangleCount);
            for (int sourceTriangleIndex = 0; sourceTriangleIndex < sourceTriangleCount; sourceTriangleIndex++)
            {
                int chunkIndex = ReadSourceChunkIndex(sourceVertices, sourceTriangleIndex);
                int writeIndex = chunkStarts[chunkIndex] + chunkWriteCursors[chunkIndex];
                sourceTriangleIndices[writeIndex] = sourceTriangleIndex;
                chunkWriteCursors[chunkIndex]++;
            }

            int paintedTriangleTotal = 0;
            int paintedVertexTotal = 0;
            int waterTriangleTotal = 0;
            int waterVertexTotal = 0;
            long meshEstimatedBytesTotal = 0L;
            long waterEstimatedBytesTotal = 0L;
            int visibleChunkCount = 0;
            Vector3 priorityOriginWorld = PlanetTrianglePoolRegistry.PriorityOriginWorld;
            PlanetRecipe recipeCopy = recipe;
            PlanetPlacement placementCopy = placement;

            for (int chunkIndex = 0; chunkIndex < chunkSlotCount; chunkIndex++)
            {
                int chunkTriangleCount = chunkTriangleCounts[chunkIndex];
                if (chunkTriangleCount <= 0)
                {
                    continue;
                }

                int chunkStart = chunkStarts[chunkIndex];
                PlanetTriangleDrawResult drawResult = trianglePoolWriter.Draw(
                    chunkTriangleCount,
                    localTriangleIndex => EvaluateTriangleScore(
                        sourceVertices,
                        sourceTriangleIndices[chunkStart + localTriangleIndex],
                        recipeCopy,
                        placementCopy,
                        priorityOriginWorld),
                    localTriangleIndex => EvaluateOutputTriangleCost(
                        sourceVertices,
                        sourceTriangleIndices[chunkStart + localTriangleIndex],
                        recipeCopy.GridRadius),
                    PlanetTriangleOwnerId.PlanetSurfaceValue,
                    MakeChunkSurfaceMeshId(chunkIndex));

                int paintedTriangleCount = drawResult.SelectedSourceTriangleCount;
                if (paintedTriangleCount <= 0)
                {
                    continue;
                }

                RuntimeChunkMesh runtimeChunk = new RuntimeChunkMesh
                {
                    meshId = BuildChunkMeshId(chunkIndex),
                    chunkIndex = chunkIndex,
                    surfaceTriangleCount = paintedTriangleCount,
                    surfaceVertexCount = paintedTriangleCount * 3
                };

                runtimeChunk.surfaceObject = CreateChunkObject(
                    meshFilter.transform,
                    meshFilter.gameObject.layer,
                    "PlanetChunk_" + chunkIndex + "_Surface_Runtime");
                MeshFilter chunkMeshFilter = runtimeChunk.surfaceObject.AddComponent<MeshFilter>();
                MeshRenderer chunkMeshRenderer = runtimeChunk.surfaceObject.AddComponent<MeshRenderer>();
                CopyRendererSettings(meshRenderer, chunkMeshRenderer);
                chunkMeshRenderer.sharedMaterial = surfaceMaterial;

                runtimeChunk.surfaceMesh = new Mesh
                {
                    name = "PlanetChunk_" + chunkIndex + "_SurfaceMesh_Runtime",
                    indexFormat = runtimeChunk.surfaceVertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
                };

                BuildMeshFromTriangleSegment(
                    runtimeChunk.surfaceMesh,
                    meshFilter.transform,
                    sourceVertices,
                    sourceTriangleIndices,
                    chunkStart,
                    chunkTriangleCount,
                    trianglePoolWriter,
                    runtimeChunk.surfaceVertexCount,
                    cells,
                    in recipe,
                    in placement,
                    settings);
                chunkMeshFilter.sharedMesh = runtimeChunk.surfaceMesh;

                runtimeChunk.surfaceEstimatedBytes = PlanetMarchingCubesPaintResult.CalculateMeshEstimatedBytes(
                    runtimeChunk.surfaceVertexCount,
                    runtimeChunk.surfaceTriangleCount);

                BuildChunkWaterMesh(
                    runtimeChunk,
                    meshFilter.transform,
                    meshFilter.gameObject.layer,
                    meshRenderer,
                    sourceVertices,
                    sourceTriangleIndices,
                    chunkStart,
                    chunkTriangleCount,
                    trianglePoolWriter,
                    in recipe,
                    in placement);

                runtimeChunks.Add(runtimeChunk);
                visibleChunkCount++;
                paintedTriangleTotal += runtimeChunk.surfaceTriangleCount;
                paintedVertexTotal += runtimeChunk.surfaceVertexCount;
                waterTriangleTotal += runtimeChunk.waterTriangleCount;
                waterVertexTotal += runtimeChunk.waterVertexCount;
                meshEstimatedBytesTotal += runtimeChunk.surfaceEstimatedBytes;
                waterEstimatedBytesTotal += runtimeChunk.waterEstimatedBytes;
            }

            return new PlanetMarchingCubesPaintResult(
                sourceTriangleCount,
                paintedTriangleTotal,
                paintedVertexTotal,
                meshEstimatedBytesTotal,
                waterTriangleTotal,
                waterVertexTotal,
                waterEstimatedBytesTotal,
                settings.colorMode,
                visibleChunkCount);
        }

        public PlanetMarchingCubesPaintResult PaintCachedChunks(
            MeshFilter meshFilter,
            MeshRenderer meshRenderer,
            Material materialOverride,
            IList<PlanetCachedChunkMesh> cachedChunks,
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

            if (cachedChunks == null)
            {
                throw new ArgumentNullException(nameof(cachedChunks));
            }

            if (!settings.Validate(out string message))
            {
                throw new ArgumentException(message, nameof(settings));
            }

            if (!recipe.IsValid(out string recipeMessage))
            {
                throw new ArgumentException(recipeMessage, nameof(recipe));
            }

            PlanetGpuShapeCell[] cells = new PlanetGpuShapeCell[recipe.VoronoiDivision];
            PlanetGpuShapeCellBuilder.Build(in recipe, cells);

            Release(meshFilter, meshRenderer);
            meshFilter.sharedMesh = null;
            meshRenderer.sharedMaterial = null;

            Material surfaceMaterial = ResolveMaterial(materialOverride, settings.colorMode, cells);
            int paintedTriangleTotal = 0;
            int paintedVertexTotal = 0;
            int waterTriangleTotal = 0;
            int waterVertexTotal = 0;
            long meshEstimatedBytesTotal = 0L;
            long waterEstimatedBytesTotal = 0L;
            int visibleChunkCount = 0;

            for (int i = 0; i < cachedChunks.Count; i++)
            {
                PlanetCachedChunkMesh cachedChunk = cachedChunks[i];
                if (cachedChunk == null || cachedChunk.SurfaceMesh == null && cachedChunk.WaterMesh == null)
                {
                    continue;
                }

                RuntimeChunkMesh runtimeChunk = new RuntimeChunkMesh
                {
                    meshId = BuildChunkMeshId(cachedChunk.ChunkId),
                    chunkIndex = cachedChunk.ChunkId,
                    surfaceMesh = cachedChunk.SurfaceMesh,
                    surfaceTriangleCount = cachedChunk.SurfaceTriangleCount,
                    surfaceVertexCount = cachedChunk.SurfaceVertexCount,
                    surfaceEstimatedBytes = cachedChunk.SurfaceEstimatedBytes,
                    waterMesh = cachedChunk.WaterMesh,
                    waterTriangleCount = cachedChunk.WaterTriangleCount,
                    waterVertexCount = cachedChunk.WaterVertexCount,
                    waterEstimatedBytes = cachedChunk.WaterEstimatedBytes
                };

                if (runtimeChunk.surfaceMesh != null)
                {
                    runtimeChunk.surfaceObject = CreateChunkObject(
                        meshFilter.transform,
                        meshFilter.gameObject.layer,
                        "PlanetChunk_" + runtimeChunk.chunkIndex + "_Surface_Cached");
                    MeshFilter chunkMeshFilter = runtimeChunk.surfaceObject.AddComponent<MeshFilter>();
                    MeshRenderer chunkMeshRenderer = runtimeChunk.surfaceObject.AddComponent<MeshRenderer>();
                    CopyRendererSettings(meshRenderer, chunkMeshRenderer);
                    chunkMeshRenderer.sharedMaterial = surfaceMaterial;
                    chunkMeshFilter.sharedMesh = runtimeChunk.surfaceMesh;
                }

                if (runtimeChunk.waterMesh != null)
                {
                    runtimeChunk.waterObject = CreateChunkObject(
                        meshFilter.transform,
                        meshFilter.gameObject.layer,
                        "PlanetChunk_" + runtimeChunk.chunkIndex + "_Water_Cached");
                    MeshFilter waterMeshFilter = runtimeChunk.waterObject.AddComponent<MeshFilter>();
                    MeshRenderer waterMeshRenderer = runtimeChunk.waterObject.AddComponent<MeshRenderer>();
                    CopyRendererSettings(meshRenderer, waterMeshRenderer);
                    waterMeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                    waterMeshRenderer.sharedMaterial = ResolveWaterMaterial();
                    waterMeshFilter.sharedMesh = runtimeChunk.waterMesh;
                }

                runtimeChunks.Add(runtimeChunk);
                visibleChunkCount++;
                paintedTriangleTotal += runtimeChunk.surfaceTriangleCount;
                paintedVertexTotal += runtimeChunk.surfaceVertexCount;
                waterTriangleTotal += runtimeChunk.waterTriangleCount;
                waterVertexTotal += runtimeChunk.waterVertexCount;
                meshEstimatedBytesTotal += runtimeChunk.surfaceEstimatedBytes;
                waterEstimatedBytesTotal += runtimeChunk.waterEstimatedBytes;
            }

            return new PlanetMarchingCubesPaintResult(
                paintedTriangleTotal,
                paintedTriangleTotal,
                paintedVertexTotal,
                meshEstimatedBytesTotal,
                waterTriangleTotal,
                waterVertexTotal,
                waterEstimatedBytesTotal,
                settings.colorMode,
                visibleChunkCount);
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

            if (!settings.Validate(out string message))
            {
                throw new ArgumentException(message, nameof(settings));
            }

            if (!recipe.IsValid(out string recipeMessage))
            {
                throw new ArgumentException(recipeMessage, nameof(recipe));
            }

            PlanetGpuShapeCell[] cells = new PlanetGpuShapeCell[recipe.VoronoiDivision];
            PlanetGpuShapeCellBuilder.Build(in recipe, cells);
            Material surfaceMaterial = ResolveMaterial(materialOverride, settings.colorMode, cells);
            if (meshRenderer != null)
            {
                meshRenderer.sharedMaterial = surfaceMaterial;
            }

            RuntimeChunkMesh runtimeMeshSlot = GetOrCreateRuntimeMeshSlot(meshId, cacheChunkId);
            string safeMeshId = SanitizeObjectName(meshId);

            if (surfaceMesh != null)
            {
                EnsureNamedSurfaceSlot(
                    runtimeMeshSlot,
                    meshFilter.transform,
                    meshFilter.gameObject.layer,
                    safeMeshId + "_Surface_Named",
                    meshRenderer,
                    surfaceMaterial);
                CopyMeshData(surfaceMesh, runtimeMeshSlot.surfaceMesh);
                if (surfaceMesh != runtimeMeshSlot.surfaceMesh)
                {
                    DestroyRuntimeObject(surfaceMesh);
                }
            }
            else
            {
                PlanetTrianglePoolRegistry.Environment.GpuBackend.ReleasePublication(
                    cacheChunkId >= 0 ? MakeChunkSurfaceMeshId(cacheChunkId) : PlanetSurfaceMeshId);
                ClearNamedSurfaceSlot(runtimeMeshSlot);
            }

            if (waterMesh != null)
            {
                EnsureNamedWaterSlot(
                    runtimeMeshSlot,
                    meshFilter.transform,
                    meshFilter.gameObject.layer,
                    safeMeshId + "_Water_Named",
                    meshRenderer);
                CopyMeshData(waterMesh, runtimeMeshSlot.waterMesh);
                if (waterMesh != runtimeMeshSlot.waterMesh)
                {
                    DestroyRuntimeObject(waterMesh);
                }
            }
            else
            {
                ClearNamedWaterSlot(runtimeMeshSlot);
            }

            UpdateRuntimeMeshSlotMetrics(runtimeMeshSlot);
            return new PlanetMarchingCubesPaintResult(
                runtimeMeshSlot.surfaceTriangleCount,
                runtimeMeshSlot.surfaceTriangleCount,
                runtimeMeshSlot.surfaceVertexCount,
                runtimeMeshSlot.surfaceEstimatedBytes,
                runtimeMeshSlot.waterTriangleCount,
                runtimeMeshSlot.waterVertexCount,
                runtimeMeshSlot.waterEstimatedBytes,
                settings.colorMode,
                1);
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

            if (cache == null)
            {
                throw new ArgumentNullException(nameof(cache));
            }

            if (string.IsNullOrWhiteSpace(meshId))
            {
                throw new ArgumentException("meshId cannot be empty.", nameof(meshId));
            }

            if (!settings.Validate(out string message))
            {
                throw new ArgumentException(message, nameof(settings));
            }

            if (!recipe.IsValid(out string recipeMessage))
            {
                throw new ArgumentException(recipeMessage, nameof(recipe));
            }

            if (!cache.TryGetChunkMeshAvailability(cacheChunkId, cacheLod, out bool hasSurfaceMesh, out bool hasWaterMesh))
            {
                return new PlanetMarchingCubesPaintResult(0, 0, 0, 0L, 0, 0, 0L, settings.colorMode, 0);
            }

            PlanetGpuShapeCell[] cells = new PlanetGpuShapeCell[recipe.VoronoiDivision];
            PlanetGpuShapeCellBuilder.Build(in recipe, cells);
            Material surfaceMaterial = ResolveMaterial(materialOverride, settings.colorMode, cells);
            RuntimeChunkMesh runtimeMeshSlot = GetOrCreateRuntimeMeshSlot(meshId, cacheChunkId);

            if (hasSurfaceMesh)
            {
                bool loadedSurfaceMesh = cache.TryLoadChunkSurfaceMeshData(
                        cacheChunkId,
                        cacheLod,
                        cacheGpuVertices,
                        cacheGpuNormals,
                        cacheGpuUvs,
                        cacheGpuColors,
                        cacheGpuIndices,
                        out Bounds cacheBounds);
                if (loadedSurfaceMesh &&
                    PublishGpuCachedSurface(
                        cacheChunkId >= 0 ? MakeChunkSurfaceMeshId(cacheChunkId) : PlanetSurfaceMeshId,
                        cacheGpuVertices,
                        cacheGpuNormals,
                        cacheGpuUvs,
                        cacheGpuColors,
                        cacheGpuIndices,
                        meshFilter != null ? meshFilter.transform : null,
                        cacheBounds,
                        surfaceMaterial,
                        out int gpuSurfaceVertexCount,
                        out Bounds _))
                {
                    ClearNamedSurfaceSlot(runtimeMeshSlot);
                    runtimeMeshSlot.surfaceVertexCount = gpuSurfaceVertexCount;
                    runtimeMeshSlot.surfaceTriangleCount = gpuSurfaceVertexCount / 3;
                    runtimeMeshSlot.surfaceEstimatedBytes = PlanetMarchingCubesPaintResult.CalculateMeshEstimatedBytes(
                        runtimeMeshSlot.surfaceVertexCount,
                        runtimeMeshSlot.surfaceTriangleCount);
                }
                else if (loadedSurfaceMesh)
                {
                    UpdateRuntimeMeshSlotMetricsPreservingGpuSurface(runtimeMeshSlot);
                    return BuildNamedPaintResult(runtimeMeshSlot, settings);
                }
                else
                {
                    PlanetTrianglePoolRegistry.Environment.GpuBackend.ReleasePublication(
                        cacheChunkId >= 0 ? MakeChunkSurfaceMeshId(cacheChunkId) : PlanetSurfaceMeshId);
                    ClearNamedSurfaceSlot(runtimeMeshSlot);
                }
            }
            else
            {
                PlanetTrianglePoolRegistry.Environment.GpuBackend.ReleasePublication(
                    cacheChunkId >= 0 ? MakeChunkSurfaceMeshId(cacheChunkId) : PlanetSurfaceMeshId);
                ClearNamedSurfaceSlot(runtimeMeshSlot);
            }

            ClearNamedWaterSlot(runtimeMeshSlot);

            UpdateRuntimeMeshSlotMetricsPreservingGpuSurface(runtimeMeshSlot);
            return BuildNamedPaintResult(runtimeMeshSlot, settings);
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
            int cacheLod)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (!settings.Validate(out string message))
            {
                throw new ArgumentException(message, nameof(settings));
            }

            if (!recipe.IsValid(out string recipeMessage))
            {
                throw new ArgumentException(recipeMessage, nameof(recipe));
            }

            int sourceVertexCount = source.VertexCount - source.VertexCount % 3;
            int sourceTriangleCount = sourceVertexCount / 3;
            RuntimeChunkMesh runtimeMeshSlot = GetOrCreateRuntimeMeshSlot(meshId, cacheChunkId);
            if (sourceTriangleCount <= 0)
            {
                PlanetTrianglePoolRegistry.Environment.GpuBackend.ReleasePublication(
                    cacheChunkId >= 0 ? MakeChunkSurfaceMeshId(cacheChunkId) : PlanetSurfaceMeshId);
                ClearNamedSurfaceSlot(runtimeMeshSlot);
                ClearNamedWaterSlot(runtimeMeshSlot);
                UpdateRuntimeMeshSlotMetrics(runtimeMeshSlot);
                return BuildNamedPaintResult(runtimeMeshSlot, settings);
            }

            Vector3 priorityOriginWorld = PlanetTrianglePoolRegistry.PriorityOriginWorld;
            PlanetRecipe recipeCopy = recipe;
            PlanetPlacement placementCopy = placement;
            PlanetMarchingCubesVertex[] sourceVertices = source.Vertices;
            PlanetTriangleDrawResult drawResult = trianglePoolWriter.Draw(
                sourceTriangleCount,
                triangleIndex => EvaluateTriangleScore(
                    sourceVertices,
                    triangleIndex,
                    recipeCopy,
                    placementCopy,
                    priorityOriginWorld),
                _ => 1,
                PlanetTriangleOwnerId.PlanetSurfaceValue,
                cacheChunkId >= 0 ? MakeChunkSurfaceMeshId(cacheChunkId) : PlanetSurfaceMeshId);

            int paintedTriangleCount = drawResult.SelectedSourceTriangleCount;
            int paintedVertexCount = paintedTriangleCount * 3;
            PlanetGpuShapeCell[] cells = new PlanetGpuShapeCell[recipe.VoronoiDivision];
            PlanetGpuShapeCellBuilder.Build(in recipe, cells);
            Material surfaceMaterial = ResolveMaterial(materialOverride, settings.colorMode, cells);
            if (meshRenderer != null)
            {
                meshRenderer.sharedMaterial = surfaceMaterial;
            }

            string safeMeshId = SanitizeObjectName(meshId);

            if (paintedVertexCount > 0)
            {
                if (PublishGpuExtractionSurface(
                    cacheChunkId >= 0 ? MakeChunkSurfaceMeshId(cacheChunkId) : PlanetSurfaceMeshId,
                    source.Vertices,
                    sourceVertexCount,
                    trianglePoolWriter,
                    in recipe,
                    in placement,
                    settings,
                    surfaceMaterial,
                    out Bounds _))
                {
                    ClearNamedSurfaceSlot(runtimeMeshSlot);
                    runtimeMeshSlot.surfaceVertexCount = paintedVertexCount;
                    runtimeMeshSlot.surfaceTriangleCount = paintedTriangleCount;
                    runtimeMeshSlot.surfaceEstimatedBytes = PlanetMarchingCubesPaintResult.CalculateMeshEstimatedBytes(
                        paintedVertexCount,
                        paintedTriangleCount);
                }
                else
                {
                    ClearNamedSurfaceSlot(runtimeMeshSlot);
                }
            }
            else
            {
                PlanetTrianglePoolRegistry.Environment.GpuBackend.ReleasePublication(
                    cacheChunkId >= 0 ? MakeChunkSurfaceMeshId(cacheChunkId) : PlanetSurfaceMeshId);
                ClearNamedSurfaceSlot(runtimeMeshSlot);
            }

            ClearNamedWaterSlot(runtimeMeshSlot);

            UpdateRuntimeMeshSlotMetricsPreservingGpuSurface(runtimeMeshSlot);
            if ((runtimeMeshSlot.surfaceTriangleCount > 0 || runtimeMeshSlot.waterTriangleCount > 0) &&
                cache != null &&
                cacheChunkId >= 0 &&
                cacheLod >= 0)
            {
                Mesh cacheSurfaceMesh = null;
                try
                {
                    if (paintedVertexCount > 0)
                    {
                        cacheSurfaceMesh = new Mesh
                        {
                            name = safeMeshId + "_Surface_CacheWrite",
                            indexFormat = paintedVertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
                        };
                        BuildMesh(
                            cacheSurfaceMesh,
                            meshFilter != null ? meshFilter.transform : null,
                            source.Vertices,
                            sourceVertexCount,
                            trianglePoolWriter,
                            paintedVertexCount,
                            cells,
                            in recipe,
                            in placement,
                            settings);
                    }

                    cache.SaveChunk(cacheChunkId, cacheLod, cacheSurfaceMesh, null);
                }
                finally
                {
                    if (cacheSurfaceMesh != null)
                    {
                        DestroyRuntimeObject(cacheSurfaceMesh);
                    }
                }
            }

            return BuildNamedPaintResult(runtimeMeshSlot, settings);
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
            PlanetTrianglePoolRegistry.Environment.GpuBackend.ReleaseAllPublications();

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
            PlanetTrianglePoolRegistry.Environment.GpuBackend.ReleaseWaterPublication();

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
                ReleaseRuntimeMesh(runtimeChunks[i]);
            }

            runtimeChunks.Clear();
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
            Material material)
        {
            if (slot.surfaceObject == null)
            {
                slot.surfaceObject = CreateChunkObject(parent, layer, objectName);
            }

            slot.surfaceObject.SetActive(true);
            slot.surfaceObject.name = objectName;
            slot.surfaceObject.layer = layer;
            EnsureChunkTransform(slot.surfaceObject.transform, parent);
            slot.surfaceMeshFilter = EnsureComponent(slot.surfaceObject, slot.surfaceMeshFilter);
            slot.surfaceMeshRenderer = EnsureComponent(slot.surfaceObject, slot.surfaceMeshRenderer);
            CopyRendererSettings(sourceRenderer, slot.surfaceMeshRenderer);
            slot.surfaceMeshRenderer.sharedMaterial = material;

            if (slot.surfaceMesh == null)
            {
                slot.surfaceMesh = new Mesh
                {
                    name = objectName + "_Mesh"
                };
                slot.surfaceMesh.MarkDynamic();
            }

            slot.surfaceMeshFilter.sharedMesh = slot.surfaceMesh;
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
                slot.waterObject = CreateChunkObject(parent, layer, objectName);
            }

            slot.waterObject.SetActive(true);
            slot.waterObject.name = objectName;
            slot.waterObject.layer = layer;
            EnsureChunkTransform(slot.waterObject.transform, parent);
            slot.waterMeshFilter = EnsureComponent(slot.waterObject, slot.waterMeshFilter);
            slot.waterMeshRenderer = EnsureComponent(slot.waterObject, slot.waterMeshRenderer);
            CopyRendererSettings(sourceRenderer, slot.waterMeshRenderer);
            slot.waterMeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            slot.waterMeshRenderer.sharedMaterial = ResolveWaterMaterial();

            if (slot.waterMesh == null)
            {
                slot.waterMesh = new Mesh
                {
                    name = objectName + "_Mesh"
                };
                slot.waterMesh.MarkDynamic();
            }

            slot.waterMeshFilter.sharedMesh = slot.waterMesh;
        }

        private static T EnsureComponent<T>(GameObject owner, T cached) where T : Component
        {
            if (cached != null)
            {
                return cached;
            }

            T component = owner.GetComponent<T>();
            return component != null ? component : owner.AddComponent<T>();
        }

        private static void EnsureChunkTransform(Transform chunkTransform, Transform parent)
        {
            if (chunkTransform == null)
            {
                return;
            }

            if (parent != null && chunkTransform.parent != parent)
            {
                chunkTransform.SetParent(parent, false);
            }

            chunkTransform.localPosition = Vector3.zero;
            chunkTransform.localRotation = Quaternion.identity;
            chunkTransform.localScale = Vector3.one;
        }

        private static void ClearNamedSurfaceSlot(RuntimeChunkMesh slot)
        {
            if (slot.surfaceMesh != null)
            {
                slot.surfaceMesh.Clear();
            }

            if (slot.surfaceObject != null)
            {
                slot.surfaceObject.SetActive(false);
            }

            slot.surfaceVertexCount = 0;
            slot.surfaceTriangleCount = 0;
            slot.surfaceEstimatedBytes = 0L;
        }

        private static void ClearNamedWaterSlot(RuntimeChunkMesh slot)
        {
            if (slot.waterMesh != null)
            {
                slot.waterMesh.Clear();
            }

            if (slot.waterObject != null)
            {
                slot.waterObject.SetActive(false);
            }

            slot.waterVertexCount = 0;
            slot.waterTriangleCount = 0;
            slot.waterEstimatedBytes = 0L;
        }

        private void CopyMeshData(Mesh source, Mesh target)
        {
            if (source == null || target == null)
            {
                return;
            }

            meshUploadPositions.Clear();
            meshUploadNormals.Clear();
            meshUploadUvs.Clear();
            meshUploadColors.Clear();
            meshUploadIndices.Clear();
            source.GetVertices(meshUploadPositions);
            source.GetNormals(meshUploadNormals);
            source.GetUVs(0, meshUploadUvs);
            source.GetColors(meshUploadColors);
            source.GetTriangles(meshUploadIndices, 0);

            target.indexFormat = source.indexFormat;
            target.Clear();
            target.SetVertices(meshUploadPositions);
            if (meshUploadNormals.Count == meshUploadPositions.Count)
            {
                target.SetNormals(meshUploadNormals);
            }

            if (meshUploadUvs.Count == meshUploadPositions.Count)
            {
                target.SetUVs(0, meshUploadUvs);
            }

            if (meshUploadColors.Count == meshUploadPositions.Count)
            {
                target.SetColors(meshUploadColors);
            }

            target.SetTriangles(meshUploadIndices, 0, true);
            target.bounds = source.bounds;
        }

        private static void UpdateRuntimeMeshSlotMetrics(RuntimeChunkMesh slot)
        {
            slot.surfaceVertexCount = slot.surfaceMesh != null ? slot.surfaceMesh.vertexCount : 0;
            slot.surfaceTriangleCount = slot.surfaceMesh != null ? CountMeshTriangles(slot.surfaceMesh) : 0;
            slot.waterVertexCount = slot.waterMesh != null ? slot.waterMesh.vertexCount : 0;
            slot.waterTriangleCount = slot.waterMesh != null ? CountMeshTriangles(slot.waterMesh) : 0;
            slot.surfaceEstimatedBytes = PlanetMarchingCubesPaintResult.CalculateMeshEstimatedBytes(
                slot.surfaceVertexCount,
                slot.surfaceTriangleCount);
            slot.waterEstimatedBytes = PlanetMarchingCubesPaintResult.CalculateMeshEstimatedBytes(
                slot.waterVertexCount,
                slot.waterTriangleCount);
        }

        private static void UpdateRuntimeMeshSlotMetricsPreservingGpuSurface(RuntimeChunkMesh slot)
        {
            slot.waterVertexCount = slot.waterMesh != null ? slot.waterMesh.vertexCount : 0;
            slot.waterTriangleCount = slot.waterMesh != null ? CountMeshTriangles(slot.waterMesh) : 0;
            slot.waterEstimatedBytes = PlanetMarchingCubesPaintResult.CalculateMeshEstimatedBytes(
                slot.waterVertexCount,
                slot.waterTriangleCount);
        }

        private bool PublishGpuExtractionSurface(
            uint meshId,
            PlanetMarchingCubesVertex[] sourceVertices,
            int sourceVertexCount,
            PlanetTrianglePoolWriter poolWriter,
            in PlanetRecipe recipe,
            in PlanetPlacement placement,
            PlanetMarchingCubesPaintSettings settings,
            Material material,
            out Bounds bounds)
        {
            bounds = default;
            gpuUploadVertices.Clear();
            int sourceTriangleCount = sourceVertexCount / 3;
            float minRadius = float.MaxValue;
            float maxRadius = float.MinValue;
            for (int sourceTriangleIndex = 0; sourceTriangleIndex < sourceTriangleCount; sourceTriangleIndex++)
            {
                if (!poolWriter.IsSourceTriangleSelected(sourceTriangleIndex))
                {
                    continue;
                }

                int sourceVertexIndex = sourceTriangleIndex * 3;
                for (int corner = 0; corner < 3; corner++)
                {
                    Vector3 gridPosition = ReadGridPosition(sourceVertices[sourceVertexIndex + corner]);
                    float radius = gridPosition.magnitude;
                    minRadius = Mathf.Min(minRadius, radius);
                    maxRadius = Mathf.Max(maxRadius, radius);
                }
            }

            if (minRadius == float.MaxValue)
            {
                minRadius = 0f;
                maxRadius = 1f;
            }

            bool hasBounds = false;
            for (int sourceTriangleIndex = 0; sourceTriangleIndex < sourceTriangleCount; sourceTriangleIndex++)
            {
                if (!poolWriter.IsSourceTriangleSelected(sourceTriangleIndex))
                {
                    continue;
                }

                int sourceVertexIndex = sourceTriangleIndex * 3;
                Vector3 a = ReadGridPosition(sourceVertices[sourceVertexIndex]);
                Vector3 b = ReadGridPosition(sourceVertices[sourceVertexIndex + 1]);
                Vector3 c = ReadGridPosition(sourceVertices[sourceVertexIndex + 2]);
                Vector3 triangleCenter = (a + b + c) * 0.33333334f;
                Vector2 triangleUv = EvaluateSurfaceAtlasUv(triangleCenter.magnitude, in recipe);

                for (int corner = 0; corner < 3; corner++)
                {
                    PlanetMarchingCubesVertex sourceVertex = sourceVertices[sourceVertexIndex + corner];
                    Vector4 packedPosition = sourceVertex.positionAndCase;
                    Vector4 packedNormal = sourceVertex.normalAndDiagnostic;
                    Vector3 gridPosition = new Vector3(packedPosition.x, packedPosition.y, packedPosition.z);
                    Vector3 gridNormal = new Vector3(packedNormal.x, packedNormal.y, packedNormal.z);
                    if (gridNormal.sqrMagnitude > 0.0001f)
                    {
                        gridNormal.Normalize();
                    }
                    else
                    {
                        gridNormal = Vector3.up;
                    }

                    Vector3 worldPosition = PlanetCoordinateConverter.GridToWorld(gridPosition, in recipe, in placement);
                    Vector3 worldNormal = (placement.PlanetRotation * gridNormal).normalized;
                    float height01 = triangleUv.y;
                    Color32 color = EvaluateColor(
                        settings,
                        gridPosition,
                        gridNormal,
                        Mathf.RoundToInt(packedPosition.w),
                        sourceTriangleIndex,
                        minRadius,
                        maxRadius,
                        height01);
                    gpuUploadVertices.Add(new PlanetTriangleGpuVertex(worldPosition, worldNormal, triangleUv, color, true));
                    IncludePointInBounds(ref bounds, ref hasBounds, worldPosition);
                }
            }

            if (!hasBounds)
            {
                bounds = new Bounds(Vector3.zero, Vector3.one);
            }

            return PlanetTrianglePoolRegistry.Environment.GpuBackend.Publish(
                meshId,
                gpuUploadVertices,
                gpuUploadVertices.Count,
                bounds,
                material);
        }

        private bool PublishGpuCachedSurface(
            uint meshId,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<Color32> colors,
            List<int> indices,
            Transform localToWorld,
            Bounds localBounds,
            Material material,
            out int publishedVertexCount,
            out Bounds bounds)
        {
            publishedVertexCount = 0;
            bounds = default;
            gpuUploadVertices.Clear();
            bool hasBounds = false;
            int indexCount = indices.Count - indices.Count % 3;
            int sourceTriangleCount = indexCount / 3;
            Vector3 priorityOriginWorld = PlanetTrianglePoolRegistry.PriorityOriginWorld;
            trianglePoolWriter.Draw(
                sourceTriangleCount,
                triangleIndex => EvaluateCachedTriangleScore(
                    vertices,
                    indices,
                    triangleIndex,
                    localToWorld,
                    priorityOriginWorld),
                _ => 1,
                PlanetTriangleOwnerId.PlanetSurfaceValue,
                meshId);

            for (int i = 0; i < indexCount; i++)
            {
                int sourceTriangleIndex = i / 3;
                if (!trianglePoolWriter.IsSourceTriangleSelected(sourceTriangleIndex))
                {
                    i += 2 - i % 3;
                    continue;
                }

                int vertexIndex = indices[i];
                if (vertexIndex < 0 || vertexIndex >= vertices.Count)
                {
                    continue;
                }

                Vector3 position = vertices[vertexIndex];
                Vector3 normal = vertexIndex < normals.Count ? normals[vertexIndex] : Vector3.up;
                Vector2 uv = vertexIndex < uvs.Count ? uvs[vertexIndex] : Vector2.zero;
                Color32 color = vertexIndex < colors.Count ? colors[vertexIndex] : Color.white;
                Vector3 worldPosition = localToWorld != null ? localToWorld.TransformPoint(position) : position;
                Vector3 worldNormal = localToWorld != null ? localToWorld.TransformDirection(normal).normalized : normal.normalized;
                if (worldNormal.sqrMagnitude <= 0.0001f)
                {
                    worldNormal = Vector3.up;
                }

                gpuUploadVertices.Add(new PlanetTriangleGpuVertex(worldPosition, worldNormal, uv, color, true));
                IncludePointInBounds(ref bounds, ref hasBounds, worldPosition);
            }

            publishedVertexCount = gpuUploadVertices.Count - gpuUploadVertices.Count % 3;
            if (publishedVertexCount <= 0)
            {
                PlanetTrianglePoolRegistry.Environment.GpuBackend.ReleasePublication(meshId);
                bounds = localToWorld != null ? TransformBounds(localToWorld, localBounds) : localBounds;
                return true;
            }

            if (!hasBounds)
            {
                bounds = localToWorld != null ? TransformBounds(localToWorld, localBounds) : localBounds;
            }

            PlanetTriangleGpuBackend gpuBackend = PlanetTrianglePoolRegistry.Environment.GpuBackend;
            bool published = gpuBackend.Publish(
                meshId,
                gpuUploadVertices,
                publishedVertexCount,
                bounds,
                material);
            publishedVertexCount = gpuBackend.LastPublishedVertexCount;
            return published;
        }

        private static float EvaluateCachedTriangleScore(
            List<Vector3> vertices,
            List<int> indices,
            int sourceTriangleIndex,
            Transform localToWorld,
            Vector3 priorityOriginWorld)
        {
            int indexStart = sourceTriangleIndex * 3;
            Vector3 center = Vector3.zero;
            int validVertexCount = 0;
            for (int corner = 0; corner < 3; corner++)
            {
                int index = indexStart + corner;
                if (index < 0 || index >= indices.Count)
                {
                    continue;
                }

                int vertexIndex = indices[index];
                if (vertexIndex < 0 || vertexIndex >= vertices.Count)
                {
                    continue;
                }

                center += vertices[vertexIndex];
                validVertexCount++;
            }

            if (validVertexCount <= 0)
            {
                return float.MaxValue;
            }

            center /= validVertexCount;
            Vector3 worldCenter = localToWorld != null ? localToWorld.TransformPoint(center) : center;
            return (worldCenter - priorityOriginWorld).sqrMagnitude;
        }

        private static void IncludePointInBounds(ref Bounds bounds, ref bool hasBounds, Vector3 point)
        {
            if (!hasBounds)
            {
                bounds = new Bounds(point, Vector3.zero);
                hasBounds = true;
                return;
            }

            bounds.Encapsulate(point);
        }

        private static Bounds TransformBounds(Transform transform, Bounds localBounds)
        {
            if (transform == null)
            {
                return localBounds;
            }

            Vector3 center = transform.TransformPoint(localBounds.center);
            Vector3 extents = localBounds.extents;
            Vector3 axisX = transform.TransformVector(extents.x, 0f, 0f);
            Vector3 axisY = transform.TransformVector(0f, extents.y, 0f);
            Vector3 axisZ = transform.TransformVector(0f, 0f, extents.z);
            extents.x = Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x);
            extents.y = Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y);
            extents.z = Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z);
            return new Bounds(center, extents * 2f);
        }

        private static PlanetMarchingCubesPaintResult BuildNamedPaintResult(
            RuntimeChunkMesh slot,
            PlanetMarchingCubesPaintSettings settings)
        {
            int visibleChunkCount = slot.surfaceTriangleCount > 0 || slot.waterTriangleCount > 0 ? 1 : 0;
            return new PlanetMarchingCubesPaintResult(
                slot.surfaceTriangleCount,
                slot.surfaceTriangleCount,
                slot.surfaceVertexCount,
                slot.surfaceEstimatedBytes,
                slot.waterTriangleCount,
                slot.waterVertexCount,
                slot.waterEstimatedBytes,
                settings.colorMode,
                visibleChunkCount);
        }

        private void ReleaseRuntimeMeshAt(int index)
        {
            if (index < 0 || index >= runtimeChunks.Count)
            {
                return;
            }

            ReleaseRuntimeMesh(runtimeChunks[index]);
            runtimeChunks.RemoveAt(index);
        }

        private void ReleaseRuntimeMesh(RuntimeChunkMesh chunk)
        {
            if (chunk == null)
            {
                return;
            }

            if (chunk.surfaceMesh != null)
            {
                DestroyRuntimeObject(chunk.surfaceMesh);
                chunk.surfaceMesh = null;
            }

            if (chunk.waterMesh != null)
            {
                DestroyRuntimeObject(chunk.waterMesh);
                chunk.waterMesh = null;
            }

            if (chunk.waterObject != null)
            {
                DestroyRuntimeObject(chunk.waterObject);
                chunk.waterObject = null;
            }

            if (chunk.surfaceObject != null)
            {
                DestroyRuntimeObject(chunk.surfaceObject);
                chunk.surfaceObject = null;
            }
        }

        private static int CountMeshTriangles(Mesh mesh)
        {
            if (mesh == null || mesh.subMeshCount <= 0)
            {
                return 0;
            }

            return (int)mesh.GetIndexCount(0) / 3;
        }

        private static string SanitizeObjectName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "NamedMesh";
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

        private void BuildMesh(
            Mesh mesh,
            Transform targetTransform,
            PlanetMarchingCubesVertex[] sourceVertices,
            int sourceVertexCount,
            PlanetTrianglePoolWriter poolWriter,
            int paintedVertexCount,
            PlanetGpuShapeCell[] cells,
            in PlanetRecipe recipe,
            in PlanetPlacement placement,
            PlanetMarchingCubesPaintSettings settings)
        {
            PrepareMeshUploadBuffers(paintedVertexCount);

            float minRadius = float.MaxValue;
            float maxRadius = float.MinValue;
            int sourceTriangleCount = sourceVertexCount / 3;
            for (int sourceTriangleIndex = 0; sourceTriangleIndex < sourceTriangleCount; sourceTriangleIndex++)
            {
                if (!poolWriter.IsSourceTriangleSelected(sourceTriangleIndex))
                {
                    continue;
                }

                int sourceVertexIndex = sourceTriangleIndex * 3;
                for (int corner = 0; corner < 3; corner++)
                {
                    Vector4 packedPosition = sourceVertices[sourceVertexIndex + corner].positionAndCase;
                    Vector3 gridPosition = new Vector3(packedPosition.x, packedPosition.y, packedPosition.z);
                    float radius = gridPosition.magnitude;
                    if (radius < minRadius)
                    {
                        minRadius = radius;
                    }

                    if (radius > maxRadius)
                    {
                        maxRadius = radius;
                    }
                }
            }

            if (minRadius == float.MaxValue)
            {
                minRadius = 0f;
                maxRadius = 1f;
            }

            int writeVertexIndex = 0;
            for (int sourceTriangleIndex = 0; sourceTriangleIndex < sourceTriangleCount; sourceTriangleIndex++)
            {
                if (!poolWriter.IsSourceTriangleSelected(sourceTriangleIndex))
                {
                    continue;
                }

                int sourceVertexIndex = sourceTriangleIndex * 3;
                Vector3 a = ReadGridPosition(sourceVertices[sourceVertexIndex]);
                Vector3 b = ReadGridPosition(sourceVertices[sourceVertexIndex + 1]);
                Vector3 c = ReadGridPosition(sourceVertices[sourceVertexIndex + 2]);
                Vector3 triangleCenter = (a + b + c) * 0.33333334f;
                Vector2 triangleUv = EvaluateSurfaceAtlasUv(triangleCenter.magnitude, in recipe);

                for (int corner = 0; corner < 3; corner++)
                {
                    PlanetMarchingCubesVertex sourceVertex = sourceVertices[sourceVertexIndex + corner];
                    Vector4 packedPosition = sourceVertex.positionAndCase;
                    Vector4 packedNormal = sourceVertex.normalAndDiagnostic;
                    Vector3 gridPosition = new Vector3(packedPosition.x, packedPosition.y, packedPosition.z);
                    Vector3 gridNormal = new Vector3(packedNormal.x, packedNormal.y, packedNormal.z);
                    if (gridNormal.sqrMagnitude > 0.0001f)
                    {
                        gridNormal.Normalize();
                    }
                    else
                    {
                        gridNormal = Vector3.up;
                    }

                    Vector3 worldPosition = PlanetCoordinateConverter.GridToWorld(gridPosition, in recipe, in placement);
                    Vector3 worldNormal = placement.PlanetRotation * gridNormal;
                    meshUploadPositions.Add(targetTransform != null ? targetTransform.InverseTransformPoint(worldPosition) : worldPosition);
                    meshUploadNormals.Add(targetTransform != null ? targetTransform.InverseTransformDirection(worldNormal).normalized : worldNormal.normalized);
                    float height01 = triangleUv.y;
                    meshUploadUvs.Add(triangleUv);
                    meshUploadColors.Add(EvaluateColor(
                        settings,
                        gridPosition,
                        gridNormal,
                        Mathf.RoundToInt(packedPosition.w),
                        sourceTriangleIndex,
                        minRadius,
                        maxRadius,
                        height01));
                    meshUploadIndices.Add(writeVertexIndex);
                    writeVertexIndex++;
                }
            }

            UploadMeshBuffers(mesh);
        }

        private void BuildMeshFromTriangleSegment(
            Mesh mesh,
            Transform targetTransform,
            PlanetMarchingCubesVertex[] sourceVertices,
            int[] sourceTriangleIndices,
            int segmentStart,
            int segmentTriangleCount,
            PlanetTrianglePoolWriter poolWriter,
            int paintedVertexCount,
            PlanetGpuShapeCell[] cells,
            in PlanetRecipe recipe,
            in PlanetPlacement placement,
            PlanetMarchingCubesPaintSettings settings)
        {
            PrepareMeshUploadBuffers(paintedVertexCount);

            float minRadius = float.MaxValue;
            float maxRadius = float.MinValue;
            for (int localTriangleIndex = 0; localTriangleIndex < segmentTriangleCount; localTriangleIndex++)
            {
                if (!poolWriter.IsSourceTriangleSelected(localTriangleIndex))
                {
                    continue;
                }

                int sourceTriangleIndex = sourceTriangleIndices[segmentStart + localTriangleIndex];
                int sourceVertexIndex = sourceTriangleIndex * 3;
                for (int corner = 0; corner < 3; corner++)
                {
                    Vector4 packedPosition = sourceVertices[sourceVertexIndex + corner].positionAndCase;
                    Vector3 gridPosition = new Vector3(packedPosition.x, packedPosition.y, packedPosition.z);
                    float radius = gridPosition.magnitude;
                    if (radius < minRadius)
                    {
                        minRadius = radius;
                    }

                    if (radius > maxRadius)
                    {
                        maxRadius = radius;
                    }
                }
            }

            if (minRadius == float.MaxValue)
            {
                minRadius = 0f;
                maxRadius = 1f;
            }

            int writeVertexIndex = 0;
            for (int localTriangleIndex = 0; localTriangleIndex < segmentTriangleCount; localTriangleIndex++)
            {
                if (!poolWriter.IsSourceTriangleSelected(localTriangleIndex))
                {
                    continue;
                }

                int sourceTriangleIndex = sourceTriangleIndices[segmentStart + localTriangleIndex];
                int sourceVertexIndex = sourceTriangleIndex * 3;
                Vector3 a = ReadGridPosition(sourceVertices[sourceVertexIndex]);
                Vector3 b = ReadGridPosition(sourceVertices[sourceVertexIndex + 1]);
                Vector3 c = ReadGridPosition(sourceVertices[sourceVertexIndex + 2]);
                Vector3 triangleCenter = (a + b + c) * 0.33333334f;
                Vector2 triangleUv = EvaluateSurfaceAtlasUv(triangleCenter.magnitude, in recipe);

                for (int corner = 0; corner < 3; corner++)
                {
                    PlanetMarchingCubesVertex sourceVertex = sourceVertices[sourceVertexIndex + corner];
                    Vector4 packedPosition = sourceVertex.positionAndCase;
                    Vector4 packedNormal = sourceVertex.normalAndDiagnostic;
                    Vector3 gridPosition = new Vector3(packedPosition.x, packedPosition.y, packedPosition.z);
                    Vector3 gridNormal = new Vector3(packedNormal.x, packedNormal.y, packedNormal.z);
                    if (gridNormal.sqrMagnitude > 0.0001f)
                    {
                        gridNormal.Normalize();
                    }
                    else
                    {
                        gridNormal = Vector3.up;
                    }

                    Vector3 worldPosition = PlanetCoordinateConverter.GridToWorld(gridPosition, in recipe, in placement);
                    Vector3 worldNormal = placement.PlanetRotation * gridNormal;
                    meshUploadPositions.Add(targetTransform != null ? targetTransform.InverseTransformPoint(worldPosition) : worldPosition);
                    meshUploadNormals.Add(targetTransform != null ? targetTransform.InverseTransformDirection(worldNormal).normalized : worldNormal.normalized);
                    float height01 = triangleUv.y;
                    meshUploadUvs.Add(triangleUv);
                    meshUploadColors.Add(EvaluateColor(
                        settings,
                        gridPosition,
                        gridNormal,
                        Mathf.RoundToInt(packedPosition.w),
                        sourceTriangleIndex,
                        minRadius,
                        maxRadius,
                        height01));
                    meshUploadIndices.Add(writeVertexIndex);
                    writeVertexIndex++;
                }
            }

            UploadMeshBuffers(mesh);
        }

        private void BuildWaterMesh(
            MeshFilter surfaceMeshFilter,
            MeshRenderer surfaceMeshRenderer,
            PlanetMarchingCubesVertex[] sourceVertices,
            int sourceVertexCount,
            PlanetTrianglePoolWriter poolWriter,
            in PlanetRecipe recipe,
            in PlanetPlacement placement,
            PlanetMarchingCubesPaintSettings settings,
            out int waterVertexCount,
            out int waterTriangleCount)
        {
            waterVertexCount = 0;
            waterTriangleCount = CountWaterTriangles(sourceVertices, sourceVertexCount, recipe.GridRadius, poolWriter);
            if (waterTriangleCount <= 0)
            {
                return;
            }

            MeshFilter waterMeshFilter = EnsureWaterRenderer(surfaceMeshFilter, surfaceMeshRenderer, out MeshRenderer waterMeshRenderer);
            runtimeWaterMesh = new Mesh
            {
                name = "PlanetMarchingCubesPaint_WaterMesh_Runtime",
                indexFormat = waterTriangleCount * 3 > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
            };

            waterVertexCount = waterTriangleCount * 3;
            PrepareMeshUploadBuffers(waterVertexCount);
            FillWaterMeshData(
                sourceVertices,
                sourceVertexCount,
                recipe.GridRadius,
                poolWriter,
                surfaceMeshFilter.transform,
                in recipe,
                in placement);

            UploadMeshBuffers(runtimeWaterMesh);

            waterMeshFilter.sharedMesh = runtimeWaterMesh;
            waterMeshRenderer.sharedMaterial = ResolveWaterMaterial();
        }

        private void BuildChunkWaterMesh(
            RuntimeChunkMesh runtimeChunk,
            Transform rootTransform,
            int layer,
            MeshRenderer sourceRenderer,
            PlanetMarchingCubesVertex[] sourceVertices,
            int[] sourceTriangleIndices,
            int segmentStart,
            int segmentTriangleCount,
            PlanetTrianglePoolWriter poolWriter,
            in PlanetRecipe recipe,
            in PlanetPlacement placement)
        {
            int waterTriangleCount = CountWaterTrianglesFromSegment(
                sourceVertices,
                sourceTriangleIndices,
                segmentStart,
                segmentTriangleCount,
                recipe.GridRadius,
                poolWriter);
            if (waterTriangleCount <= 0)
            {
                return;
            }

            runtimeChunk.waterTriangleCount = waterTriangleCount;
            runtimeChunk.waterVertexCount = waterTriangleCount * 3;
            runtimeChunk.waterEstimatedBytes = PlanetMarchingCubesPaintResult.CalculateMeshEstimatedBytes(
                runtimeChunk.waterVertexCount,
                runtimeChunk.waterTriangleCount);
            runtimeChunk.waterObject = CreateChunkObject(
                rootTransform,
                layer,
                "PlanetChunk_" + runtimeChunk.chunkIndex + "_Water_Runtime");

            MeshFilter waterMeshFilter = runtimeChunk.waterObject.AddComponent<MeshFilter>();
            MeshRenderer waterMeshRenderer = runtimeChunk.waterObject.AddComponent<MeshRenderer>();
            CopyRendererSettings(sourceRenderer, waterMeshRenderer);
            waterMeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            waterMeshRenderer.sharedMaterial = ResolveWaterMaterial();

            runtimeChunk.waterMesh = new Mesh
            {
                name = "PlanetChunk_" + runtimeChunk.chunkIndex + "_WaterMesh_Runtime",
                indexFormat = runtimeChunk.waterVertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
            };

            PrepareMeshUploadBuffers(runtimeChunk.waterVertexCount);
            FillWaterMeshDataFromSegment(
                sourceVertices,
                sourceTriangleIndices,
                segmentStart,
                segmentTriangleCount,
                recipe.GridRadius,
                poolWriter,
                rootTransform,
                in recipe,
                in placement);

            UploadMeshBuffers(runtimeChunk.waterMesh);
            waterMeshFilter.sharedMesh = runtimeChunk.waterMesh;
        }

        private static int CountWaterTriangles(
            PlanetMarchingCubesVertex[] sourceVertices,
            int sourceVertexCount,
            float seaRadius,
            PlanetTrianglePoolWriter poolWriter)
        {
            int triangleCount = 0;
            int sourceTriangleCount = sourceVertexCount / 3;
            for (int sourceTriangleIndex = 0; sourceTriangleIndex < sourceTriangleCount; sourceTriangleIndex++)
            {
                if (!poolWriter.IsSourceTriangleSelected(sourceTriangleIndex))
                {
                    continue;
                }

                int i = sourceTriangleIndex * 3;
                int underwaterCount = 0;
                if (IsUnderSea(ReadGridPosition(sourceVertices[i]), seaRadius))
                {
                    underwaterCount++;
                }

                if (IsUnderSea(ReadGridPosition(sourceVertices[i + 1]), seaRadius))
                {
                    underwaterCount++;
                }

                if (IsUnderSea(ReadGridPosition(sourceVertices[i + 2]), seaRadius))
                {
                    underwaterCount++;
                }

                if (underwaterCount == 3 || underwaterCount == 1)
                {
                    triangleCount++;
                }
                else if (underwaterCount == 2)
                {
                    triangleCount += 2;
                }
            }

            return triangleCount;
        }

        private static int CountWaterTrianglesFromSegment(
            PlanetMarchingCubesVertex[] sourceVertices,
            int[] sourceTriangleIndices,
            int segmentStart,
            int segmentTriangleCount,
            float seaRadius,
            PlanetTrianglePoolWriter poolWriter)
        {
            int triangleCount = 0;
            for (int localTriangleIndex = 0; localTriangleIndex < segmentTriangleCount; localTriangleIndex++)
            {
                if (!poolWriter.IsSourceTriangleSelected(localTriangleIndex))
                {
                    continue;
                }

                int sourceTriangleIndex = sourceTriangleIndices[segmentStart + localTriangleIndex];
                int i = sourceTriangleIndex * 3;
                int underwaterCount = 0;
                if (IsUnderSea(ReadGridPosition(sourceVertices[i]), seaRadius))
                {
                    underwaterCount++;
                }

                if (IsUnderSea(ReadGridPosition(sourceVertices[i + 1]), seaRadius))
                {
                    underwaterCount++;
                }

                if (IsUnderSea(ReadGridPosition(sourceVertices[i + 2]), seaRadius))
                {
                    underwaterCount++;
                }

                if (underwaterCount == 3 || underwaterCount == 1)
                {
                    triangleCount++;
                }
                else if (underwaterCount == 2)
                {
                    triangleCount += 2;
                }
            }

            return triangleCount;
        }

        private void FillWaterMeshData(
            PlanetMarchingCubesVertex[] sourceVertices,
            int sourceVertexCount,
            float seaRadius,
            PlanetTrianglePoolWriter poolWriter,
            Transform targetTransform,
            in PlanetRecipe recipe,
            in PlanetPlacement placement)
        {
            int vertexCursor = 0;
            int sourceTriangleCount = sourceVertexCount / 3;
            for (int sourceTriangleIndex = 0; sourceTriangleIndex < sourceTriangleCount; sourceTriangleIndex++)
            {
                if (!poolWriter.IsSourceTriangleSelected(sourceTriangleIndex))
                {
                    continue;
                }

                int i = sourceTriangleIndex * 3;
                Vector3 a = ReadGridPosition(sourceVertices[i]);
                Vector3 b = ReadGridPosition(sourceVertices[i + 1]);
                Vector3 c = ReadGridPosition(sourceVertices[i + 2]);
                int clippedCount = ClipWaterTriangle(a, b, c, seaRadius, waterClipBuffer);
                if (clippedCount == 3)
                {
                    WriteWaterTriangle(
                        ref vertexCursor,
                        waterClipBuffer[0],
                        waterClipBuffer[1],
                        waterClipBuffer[2],
                        targetTransform,
                        in recipe,
                        in placement);
                }
                else if (clippedCount == 4)
                {
                    WriteWaterTriangle(
                        ref vertexCursor,
                        waterClipBuffer[0],
                        waterClipBuffer[1],
                        waterClipBuffer[2],
                        targetTransform,
                        in recipe,
                        in placement);
                    WriteWaterTriangle(
                        ref vertexCursor,
                        waterClipBuffer[0],
                        waterClipBuffer[2],
                        waterClipBuffer[3],
                        targetTransform,
                        in recipe,
                        in placement);
                }
            }
        }

        private void FillWaterMeshDataFromSegment(
            PlanetMarchingCubesVertex[] sourceVertices,
            int[] sourceTriangleIndices,
            int segmentStart,
            int segmentTriangleCount,
            float seaRadius,
            PlanetTrianglePoolWriter poolWriter,
            Transform targetTransform,
            in PlanetRecipe recipe,
            in PlanetPlacement placement)
        {
            int vertexCursor = 0;
            for (int localTriangleIndex = 0; localTriangleIndex < segmentTriangleCount; localTriangleIndex++)
            {
                if (!poolWriter.IsSourceTriangleSelected(localTriangleIndex))
                {
                    continue;
                }

                int sourceTriangleIndex = sourceTriangleIndices[segmentStart + localTriangleIndex];
                int i = sourceTriangleIndex * 3;
                Vector3 a = ReadGridPosition(sourceVertices[i]);
                Vector3 b = ReadGridPosition(sourceVertices[i + 1]);
                Vector3 c = ReadGridPosition(sourceVertices[i + 2]);
                int clippedCount = ClipWaterTriangle(a, b, c, seaRadius, waterClipBuffer);
                if (clippedCount == 3)
                {
                    WriteWaterTriangle(
                        ref vertexCursor,
                        waterClipBuffer[0],
                        waterClipBuffer[1],
                        waterClipBuffer[2],
                        targetTransform,
                        in recipe,
                        in placement);
                }
                else if (clippedCount == 4)
                {
                    WriteWaterTriangle(
                        ref vertexCursor,
                        waterClipBuffer[0],
                        waterClipBuffer[1],
                        waterClipBuffer[2],
                        targetTransform,
                        in recipe,
                        in placement);
                    WriteWaterTriangle(
                        ref vertexCursor,
                        waterClipBuffer[0],
                        waterClipBuffer[2],
                        waterClipBuffer[3],
                        targetTransform,
                        in recipe,
                        in placement);
                }
            }
        }

        private static int ClipWaterTriangle(Vector3 a, Vector3 b, Vector3 c, float seaRadius, Vector3[] clipped)
        {
            int count = 0;
            ClipWaterEdge(a, b, seaRadius, clipped, ref count);
            ClipWaterEdge(b, c, seaRadius, clipped, ref count);
            ClipWaterEdge(c, a, seaRadius, clipped, ref count);
            return count;
        }

        private static void ClipWaterEdge(Vector3 current, Vector3 next, float seaRadius, Vector3[] clipped, ref int count)
        {
            float currentDepth = seaRadius - current.magnitude;
            float nextDepth = seaRadius - next.magnitude;
            bool currentUnderwater = currentDepth > 0f;
            bool nextUnderwater = nextDepth > 0f;

            if (currentUnderwater)
            {
                clipped[count++] = ProjectToRadius(current, seaRadius);
            }

            if (currentUnderwater != nextUnderwater)
            {
                float denominator = currentDepth - nextDepth;
                float t = Mathf.Abs(denominator) <= 0.000001f ? 0.5f : Mathf.Clamp01(currentDepth / denominator);
                clipped[count++] = ProjectToRadius(Vector3.Lerp(current, next, t), seaRadius);
            }
        }

        private void WriteWaterTriangle(
            ref int vertexCursor,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Transform targetTransform,
            in PlanetRecipe recipe,
            in PlanetPlacement placement)
        {
            Vector3 center = (a + b + c) * 0.33333334f;
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude <= 0.000001f)
            {
                normal = center.sqrMagnitude > 0.000001f ? center.normalized : Vector3.up;
            }
            else if (Vector3.Dot(normal, center) < 0f)
            {
                Vector3 swap = b;
                b = c;
                c = swap;
                normal = -normal;
            }

            normal.Normalize();
            WriteWaterVertex(vertexCursor, a, normal, targetTransform, in recipe, in placement);
            vertexCursor++;
            WriteWaterVertex(vertexCursor, b, normal, targetTransform, in recipe, in placement);
            vertexCursor++;
            WriteWaterVertex(vertexCursor, c, normal, targetTransform, in recipe, in placement);
            vertexCursor++;
        }

        private void WriteWaterVertex(
            int vertexIndex,
            Vector3 gridPosition,
            Vector3 gridNormal,
            Transform targetTransform,
            in PlanetRecipe recipe,
            in PlanetPlacement placement)
        {
            Vector3 worldPosition = PlanetCoordinateConverter.GridToWorld(gridPosition, in recipe, in placement);
            Vector3 worldNormal = placement.PlanetRotation * gridNormal;
            meshUploadPositions.Add(targetTransform != null ? targetTransform.InverseTransformPoint(worldPosition) : worldPosition);
            meshUploadNormals.Add(targetTransform != null ? targetTransform.InverseTransformDirection(worldNormal).normalized : worldNormal.normalized);
            meshUploadUvs.Add(Vector2.zero);
            meshUploadColors.Add(Color.white);
            meshUploadIndices.Add(vertexIndex);
        }

        private void PrepareMeshUploadBuffers(int vertexCapacity)
        {
            int safeCapacity = Mathf.Max(0, vertexCapacity);
            EnsureListCapacity(meshUploadPositions, safeCapacity);
            EnsureListCapacity(meshUploadNormals, safeCapacity);
            EnsureListCapacity(meshUploadUvs, safeCapacity);
            EnsureListCapacity(meshUploadColors, safeCapacity);
            EnsureListCapacity(meshUploadIndices, safeCapacity);
            meshUploadPositions.Clear();
            meshUploadNormals.Clear();
            meshUploadUvs.Clear();
            meshUploadColors.Clear();
            meshUploadIndices.Clear();
        }

        private void UploadMeshBuffers(Mesh mesh)
        {
            mesh.Clear();
            mesh.SetVertices(meshUploadPositions);
            mesh.SetNormals(meshUploadNormals);
            mesh.SetUVs(0, meshUploadUvs);
            mesh.SetColors(meshUploadColors);
            mesh.SetTriangles(meshUploadIndices, 0, true);
            mesh.RecalculateBounds();
        }

        private static void EnsureListCapacity<T>(List<T> list, int capacity)
        {
            if (list.Capacity < capacity)
            {
                list.Capacity = capacity;
            }
        }

        private static int[] EnsureIntBuffer(ref int[] buffer, int capacity)
        {
            if (buffer == null || buffer.Length < capacity)
            {
                buffer = new int[Mathf.Max(0, capacity)];
            }

            return buffer;
        }

        private static bool IsUnderSea(Vector3 gridPosition, float seaRadius)
        {
            return gridPosition.magnitude < seaRadius;
        }

        private static Vector3 ProjectToRadius(Vector3 gridPosition, float radius)
        {
            if (gridPosition.sqrMagnitude <= 0.000001f)
            {
                return Vector3.up * radius;
            }

            return gridPosition.normalized * radius;
        }

        private MeshFilter EnsureWaterRenderer(MeshFilter surfaceMeshFilter, MeshRenderer surfaceMeshRenderer, out MeshRenderer waterMeshRenderer)
        {
            if (runtimeWaterObject == null)
            {
                runtimeWaterObject = new GameObject("PlanetMarchingCubesPaint_Water_Runtime");
                if (surfaceMeshFilter != null)
                {
                    runtimeWaterObject.layer = surfaceMeshFilter.gameObject.layer;
                    runtimeWaterObject.transform.SetParent(surfaceMeshFilter.transform, false);
                }
            }

            Transform waterTransform = runtimeWaterObject.transform;
            waterTransform.localPosition = Vector3.zero;
            waterTransform.localRotation = Quaternion.identity;
            waterTransform.localScale = Vector3.one;

            MeshFilter waterMeshFilter = runtimeWaterObject.GetComponent<MeshFilter>();
            if (waterMeshFilter == null)
            {
                waterMeshFilter = runtimeWaterObject.AddComponent<MeshFilter>();
            }

            waterMeshRenderer = runtimeWaterObject.GetComponent<MeshRenderer>();
            if (waterMeshRenderer == null)
            {
                waterMeshRenderer = runtimeWaterObject.AddComponent<MeshRenderer>();
            }

            if (surfaceMeshRenderer != null)
            {
                waterMeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                waterMeshRenderer.receiveShadows = surfaceMeshRenderer.receiveShadows;
            }

            return waterMeshFilter;
        }

        private static GameObject CreateChunkObject(Transform parent, int layer, string objectName)
        {
            GameObject chunkObject = new GameObject(objectName)
            {
                layer = layer
            };

            if (parent != null)
            {
                chunkObject.transform.SetParent(parent, false);
            }

            Transform chunkTransform = chunkObject.transform;
            chunkTransform.localPosition = Vector3.zero;
            chunkTransform.localRotation = Quaternion.identity;
            chunkTransform.localScale = Vector3.one;
            return chunkObject;
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

        private Material ResolveMaterial(Material materialOverride, PlanetMarchingCubesPaintColorMode colorMode, PlanetGpuShapeCell[] cells)
        {
            if (CanUseMaterialOverride(materialOverride, colorMode))
            {
                ApplyMaterialProperties(materialOverride, colorMode, cells);
                return materialOverride;
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

        private static Vector3 ReadGridPosition(PlanetMarchingCubesVertex vertex)
        {
            Vector4 packedPosition = vertex.positionAndCase;
            return new Vector3(packedPosition.x, packedPosition.y, packedPosition.z);
        }

        private static int FindMaxSourceChunkIndex(PlanetMarchingCubesVertex[] sourceVertices, int sourceTriangleCount)
        {
            int maxChunkIndex = 0;
            for (int sourceTriangleIndex = 0; sourceTriangleIndex < sourceTriangleCount; sourceTriangleIndex++)
            {
                int chunkIndex = ReadSourceChunkIndex(sourceVertices, sourceTriangleIndex);
                if (chunkIndex > maxChunkIndex)
                {
                    maxChunkIndex = chunkIndex;
                }
            }

            return maxChunkIndex;
        }

        private static int ReadSourceChunkIndex(PlanetMarchingCubesVertex[] sourceVertices, int sourceTriangleIndex)
        {
            int sourceVertexIndex = sourceTriangleIndex * 3;
            if (sourceVertices == null || sourceVertexIndex < 0 || sourceVertexIndex >= sourceVertices.Length)
            {
                return 0;
            }

            float packedChunkIndex = sourceVertices[sourceVertexIndex].normalAndDiagnostic.w;
            if (packedChunkIndex <= 0f)
            {
                return 0;
            }

            return Mathf.Max(0, Mathf.RoundToInt(packedChunkIndex));
        }

        private static uint MakeChunkSurfaceMeshId(int chunkIndex)
        {
            return PlanetChunkSurfaceMeshIdBase + (uint)Mathf.Max(0, chunkIndex);
        }

        private static float EvaluateTriangleScore(
            PlanetMarchingCubesVertex[] sourceVertices,
            int sourceTriangleIndex,
            PlanetRecipe recipe,
            PlanetPlacement placement,
            Vector3 priorityOriginWorld)
        {
            int vertexIndex = sourceTriangleIndex * 3;
            Vector3 a = ReadGridPosition(sourceVertices[vertexIndex]);
            Vector3 b = ReadGridPosition(sourceVertices[vertexIndex + 1]);
            Vector3 c = ReadGridPosition(sourceVertices[vertexIndex + 2]);
            Vector3 centerGrid = (a + b + c) * 0.33333334f;
            Vector3 centerWorld = PlanetCoordinateConverter.GridToWorld(centerGrid, in recipe, in placement);
            return (centerWorld - priorityOriginWorld).sqrMagnitude;
        }

        private static int EvaluateOutputTriangleCost(
            PlanetMarchingCubesVertex[] sourceVertices,
            int sourceTriangleIndex,
            float seaRadius)
        {
            int vertexIndex = sourceTriangleIndex * 3;
            Vector3 a = ReadGridPosition(sourceVertices[vertexIndex]);
            Vector3 b = ReadGridPosition(sourceVertices[vertexIndex + 1]);
            Vector3 c = ReadGridPosition(sourceVertices[vertexIndex + 2]);
            return 1 + CountWaterTrianglesForSourceTriangle(a, b, c, seaRadius);
        }

        private static int CountWaterTrianglesForSourceTriangle(Vector3 a, Vector3 b, Vector3 c, float seaRadius)
        {
            int underwaterCount = 0;
            if (a.magnitude < seaRadius)
            {
                underwaterCount++;
            }

            if (b.magnitude < seaRadius)
            {
                underwaterCount++;
            }

            if (c.magnitude < seaRadius)
            {
                underwaterCount++;
            }

            if (underwaterCount == 3 || underwaterCount == 1)
            {
                return 1;
            }

            if (underwaterCount == 2)
            {
                return 2;
            }

            return 0;
        }

        private static int FindNearestCellIndex(Vector3 gridPosition, PlanetGpuShapeCell[] cells)
        {
            if (cells == null || cells.Length == 0)
            {
                return 0;
            }

            Vector3 direction = gridPosition.sqrMagnitude > 0.0001f ? gridPosition.normalized : Vector3.up;
            int nearestIndex = 0;
            float nearestDot = -2f;
            for (int i = 0; i < cells.Length; i++)
            {
                float cellDot = Vector3.Dot(direction, cells[i].Direction);
                if (cellDot > nearestDot)
                {
                    nearestDot = cellDot;
                    nearestIndex = i;
                }
            }

            return nearestIndex;
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

        private static Color32 EvaluateSurfaceGradient(float height01)
        {
            height01 = Mathf.Clamp01(height01);

            Color underwater = new Color(0.82f, 0.36f, 0.54f, 1f);
            Color shallow = new Color(0.91f, 0.74f, 0.57f, 1f);
            Color sand = new Color(0.76f, 0.66f, 0.43f, 1f);
            Color darkGreen = new Color(0.13f, 0.31f, 0.18f, 1f);
            Color green = new Color(0.25f, 0.48f, 0.24f, 1f);
            Color brown = new Color(0.38f, 0.29f, 0.20f, 1f);
            Color grey = new Color(0.50f, 0.50f, 0.47f, 1f);
            Color snow = new Color(0.88f, 0.89f, 0.84f, 1f);

            if (height01 < 0.12f)
            {
                return Color.Lerp(underwater, shallow, height01 / 0.12f);
            }

            if (height01 < 0.20f)
            {
                return Color.Lerp(shallow, sand, (height01 - 0.12f) / 0.08f);
            }

            if (height01 < 0.38f)
            {
                return Color.Lerp(sand, darkGreen, (height01 - 0.20f) / 0.18f);
            }

            if (height01 < 0.58f)
            {
                return Color.Lerp(darkGreen, green, (height01 - 0.38f) / 0.20f);
            }

            if (height01 < 0.76f)
            {
                return Color.Lerp(green, brown, (height01 - 0.58f) / 0.18f);
            }

            if (height01 < 0.90f)
            {
                return Color.Lerp(brown, grey, (height01 - 0.76f) / 0.14f);
            }

            return Color.Lerp(grey, snow, (height01 - 0.90f) / 0.10f);
        }

        private static Color32 EvaluateSurfaceCellGradient(float height01, int cellIndex)
        {
            Color baseColor = EvaluatePlanetSurfacePalette(height01);

            float valueNoise = Hash01((uint)cellIndex, 0x6ac690c5u);
            float warmthNoise = Hash01((uint)cellIndex, 0x9e3779b9u) - 0.5f;
            float value = Mathf.Lerp(0.94f, 1.06f, valueNoise);
            Color cellColor = new Color(
                Mathf.Clamp01(baseColor.r * value + warmthNoise * 0.025f),
                Mathf.Clamp01(baseColor.g * value),
                Mathf.Clamp01(baseColor.b * value - warmthNoise * 0.020f),
                1f);

            return cellColor;
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

            if (height01 < 0.12f)
            {
                return Color.Lerp(deepPink, salmon, height01 / 0.12f);
            }

            if (height01 < 0.24f)
            {
                return Color.Lerp(salmon, darkRedBrown, (height01 - 0.12f) / 0.12f);
            }

            if (height01 < 0.36f)
            {
                return Color.Lerp(darkRedBrown, roseRed, (height01 - 0.24f) / 0.12f);
            }

            if (height01 < 0.5f)
            {
                return Color.Lerp(roseRed, sand, (height01 - 0.36f) / 0.14f);
            }

            if (height01 < 0.58f)
            {
                return Color.Lerp(sand, paleYellow, (height01 - 0.5f) / 0.08f);
            }

            if (height01 < 0.68f)
            {
                return Color.Lerp(paleYellow, brightGreen, (height01 - 0.58f) / 0.10f);
            }

            if (height01 < 0.78f)
            {
                return Color.Lerp(brightGreen, darkGreen, (height01 - 0.68f) / 0.10f);
            }

            if (height01 < 0.86f)
            {
                return Color.Lerp(darkGreen, brown, (height01 - 0.78f) / 0.08f);
            }

            if (height01 < 0.93f)
            {
                return Color.Lerp(brown, darkGrey, (height01 - 0.86f) / 0.07f);
            }

            if (height01 < 0.97f)
            {
                return Color.Lerp(darkGrey, grey, (height01 - 0.93f) / 0.04f);
            }

            if (height01 < 0.99f)
            {
                return Color.Lerp(grey, lightGrey, (height01 - 0.97f) / 0.02f);
            }

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
            byte r = (byte)(80 + (hash & 127U));
            byte g = (byte)(80 + ((hash >> 8) & 127U));
            byte b = (byte)(80 + ((hash >> 16) & 127U));
            return new Color32(r, g, b, 255);
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
