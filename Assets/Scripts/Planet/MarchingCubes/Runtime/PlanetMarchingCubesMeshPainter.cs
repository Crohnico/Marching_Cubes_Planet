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
        private const int SurfaceAtlasResolution = 256;
        private const float SurfaceAtlasSeaLevelV = 0.5f;
        private const float SurfaceAtlasLandRangeScale = 0.6f;
        private const uint PlanetSurfaceMeshId = 1u;
        private const uint PlanetChunkSurfaceMeshIdBase = 0x10000000u;

        private Mesh runtimeMesh;
        private Mesh runtimeWaterMesh;
        private Material runtimeMaterial;
        private Material runtimeWaterMaterial;
        private Texture2D runtimeSurfaceAtlas;
        private GameObject runtimeWaterObject;
        private readonly List<RuntimeChunkMesh> runtimeChunks = new List<RuntimeChunkMesh>();
        private readonly PlanetTrianglePoolWriter trianglePoolWriter =
            new PlanetTrianglePoolWriter(PlanetTrianglePoolRegistry.Environment);

        public Mesh RuntimeMesh => runtimeMesh;
        public Mesh RuntimeWaterMesh => runtimeWaterMesh;
        public Material RuntimeMaterial => runtimeMaterial;
        public Material RuntimeWaterMaterial => runtimeWaterMaterial;
        public Texture2D RuntimeSurfaceAtlas => runtimeSurfaceAtlas;
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
            public int chunkIndex;
            public GameObject surfaceObject;
            public Mesh surfaceMesh;
            public GameObject waterObject;
            public Mesh waterMesh;
            public int surfaceTriangleCount;
            public int surfaceVertexCount;
            public int waterTriangleCount;
            public int waterVertexCount;
            public long surfaceEstimatedBytes;
            public long waterEstimatedBytes;
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
            int[] chunkTriangleCounts = new int[chunkSlotCount];

            for (int sourceTriangleIndex = 0; sourceTriangleIndex < sourceTriangleCount; sourceTriangleIndex++)
            {
                int chunkIndex = ReadSourceChunkIndex(sourceVertices, sourceTriangleIndex);
                chunkTriangleCounts[chunkIndex]++;
            }

            int[] chunkStarts = new int[chunkSlotCount];
            int runningStart = 0;
            for (int chunkIndex = 0; chunkIndex < chunkSlotCount; chunkIndex++)
            {
                chunkStarts[chunkIndex] = runningStart;
                runningStart += chunkTriangleCounts[chunkIndex];
            }

            int[] chunkWriteCursors = new int[chunkSlotCount];
            int[] sourceTriangleIndices = new int[sourceTriangleCount];
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
                if (chunk == null || chunk.surfaceMesh == null && chunk.waterMesh == null)
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
                RuntimeChunkMesh chunk = runtimeChunks[i];
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

            runtimeChunks.Clear();
        }

        private static void BuildMesh(
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
            Vector3[] positions = new Vector3[paintedVertexCount];
            Vector3[] normals = new Vector3[paintedVertexCount];
            Vector2[] uvs = new Vector2[paintedVertexCount];
            Color32[] colors = new Color32[paintedVertexCount];
            int[] indices = new int[paintedVertexCount];

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
                    positions[writeVertexIndex] = targetTransform != null ? targetTransform.InverseTransformPoint(worldPosition) : worldPosition;
                    normals[writeVertexIndex] = targetTransform != null ? targetTransform.InverseTransformDirection(worldNormal).normalized : worldNormal.normalized;
                    float height01 = triangleUv.y;
                    uvs[writeVertexIndex] = triangleUv;
                    colors[writeVertexIndex] = EvaluateColor(
                        settings,
                        gridPosition,
                        gridNormal,
                        Mathf.RoundToInt(packedPosition.w),
                        sourceTriangleIndex,
                        minRadius,
                        maxRadius,
                        height01);
                    indices[writeVertexIndex] = writeVertexIndex;
                    writeVertexIndex++;
                }
            }

            mesh.Clear();
            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.colors32 = colors;
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, true);
            mesh.RecalculateBounds();
        }

        private static void BuildMeshFromTriangleSegment(
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
            Vector3[] positions = new Vector3[paintedVertexCount];
            Vector3[] normals = new Vector3[paintedVertexCount];
            Vector2[] uvs = new Vector2[paintedVertexCount];
            Color32[] colors = new Color32[paintedVertexCount];
            int[] indices = new int[paintedVertexCount];

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
                    positions[writeVertexIndex] = targetTransform != null ? targetTransform.InverseTransformPoint(worldPosition) : worldPosition;
                    normals[writeVertexIndex] = targetTransform != null ? targetTransform.InverseTransformDirection(worldNormal).normalized : worldNormal.normalized;
                    float height01 = triangleUv.y;
                    uvs[writeVertexIndex] = triangleUv;
                    colors[writeVertexIndex] = EvaluateColor(
                        settings,
                        gridPosition,
                        gridNormal,
                        Mathf.RoundToInt(packedPosition.w),
                        sourceTriangleIndex,
                        minRadius,
                        maxRadius,
                        height01);
                    indices[writeVertexIndex] = writeVertexIndex;
                    writeVertexIndex++;
                }
            }

            mesh.Clear();
            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.colors32 = colors;
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, true);
            mesh.RecalculateBounds();
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
            Vector3[] positions = new Vector3[waterVertexCount];
            Vector3[] normals = new Vector3[waterVertexCount];
            Vector2[] uvs = new Vector2[waterVertexCount];
            Color32[] colors = new Color32[waterVertexCount];
            int[] indices = new int[waterVertexCount];
            FillWaterMeshData(
                positions,
                normals,
                uvs,
                colors,
                indices,
                sourceVertices,
                sourceVertexCount,
                recipe.GridRadius,
                poolWriter,
                surfaceMeshFilter.transform,
                in recipe,
                in placement);

            runtimeWaterMesh.Clear();
            runtimeWaterMesh.vertices = positions;
            runtimeWaterMesh.normals = normals;
            runtimeWaterMesh.uv = uvs;
            runtimeWaterMesh.colors32 = colors;
            runtimeWaterMesh.SetIndices(indices, MeshTopology.Triangles, 0, true);
            runtimeWaterMesh.RecalculateBounds();

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

            Vector3[] positions = new Vector3[runtimeChunk.waterVertexCount];
            Vector3[] normals = new Vector3[runtimeChunk.waterVertexCount];
            Vector2[] uvs = new Vector2[runtimeChunk.waterVertexCount];
            Color32[] colors = new Color32[runtimeChunk.waterVertexCount];
            int[] indices = new int[runtimeChunk.waterVertexCount];
            FillWaterMeshDataFromSegment(
                positions,
                normals,
                uvs,
                colors,
                indices,
                sourceVertices,
                sourceTriangleIndices,
                segmentStart,
                segmentTriangleCount,
                recipe.GridRadius,
                poolWriter,
                rootTransform,
                in recipe,
                in placement);

            runtimeChunk.waterMesh.Clear();
            runtimeChunk.waterMesh.vertices = positions;
            runtimeChunk.waterMesh.normals = normals;
            runtimeChunk.waterMesh.uv = uvs;
            runtimeChunk.waterMesh.colors32 = colors;
            runtimeChunk.waterMesh.SetIndices(indices, MeshTopology.Triangles, 0, true);
            runtimeChunk.waterMesh.RecalculateBounds();
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

        private static void FillWaterMeshData(
            Vector3[] positions,
            Vector3[] normals,
            Vector2[] uvs,
            Color32[] colors,
            int[] indices,
            PlanetMarchingCubesVertex[] sourceVertices,
            int sourceVertexCount,
            float seaRadius,
            PlanetTrianglePoolWriter poolWriter,
            Transform targetTransform,
            in PlanetRecipe recipe,
            in PlanetPlacement placement)
        {
            Vector3[] clipped = new Vector3[4];
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
                int clippedCount = ClipWaterTriangle(a, b, c, seaRadius, clipped);
                if (clippedCount == 3)
                {
                    WriteWaterTriangle(
                        positions,
                        normals,
                        uvs,
                        colors,
                        indices,
                        ref vertexCursor,
                        clipped[0],
                        clipped[1],
                        clipped[2],
                        targetTransform,
                        in recipe,
                        in placement);
                }
                else if (clippedCount == 4)
                {
                    WriteWaterTriangle(
                        positions,
                        normals,
                        uvs,
                        colors,
                        indices,
                        ref vertexCursor,
                        clipped[0],
                        clipped[1],
                        clipped[2],
                        targetTransform,
                        in recipe,
                        in placement);
                    WriteWaterTriangle(
                        positions,
                        normals,
                        uvs,
                        colors,
                        indices,
                        ref vertexCursor,
                        clipped[0],
                        clipped[2],
                        clipped[3],
                        targetTransform,
                        in recipe,
                        in placement);
                }
            }
        }

        private static void FillWaterMeshDataFromSegment(
            Vector3[] positions,
            Vector3[] normals,
            Vector2[] uvs,
            Color32[] colors,
            int[] indices,
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
            Vector3[] clipped = new Vector3[4];
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
                int clippedCount = ClipWaterTriangle(a, b, c, seaRadius, clipped);
                if (clippedCount == 3)
                {
                    WriteWaterTriangle(
                        positions,
                        normals,
                        uvs,
                        colors,
                        indices,
                        ref vertexCursor,
                        clipped[0],
                        clipped[1],
                        clipped[2],
                        targetTransform,
                        in recipe,
                        in placement);
                }
                else if (clippedCount == 4)
                {
                    WriteWaterTriangle(
                        positions,
                        normals,
                        uvs,
                        colors,
                        indices,
                        ref vertexCursor,
                        clipped[0],
                        clipped[1],
                        clipped[2],
                        targetTransform,
                        in recipe,
                        in placement);
                    WriteWaterTriangle(
                        positions,
                        normals,
                        uvs,
                        colors,
                        indices,
                        ref vertexCursor,
                        clipped[0],
                        clipped[2],
                        clipped[3],
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

        private static void WriteWaterTriangle(
            Vector3[] positions,
            Vector3[] normals,
            Vector2[] uvs,
            Color32[] colors,
            int[] indices,
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
            WriteWaterVertex(positions, normals, uvs, colors, indices, vertexCursor, a, normal, targetTransform, in recipe, in placement);
            vertexCursor++;
            WriteWaterVertex(positions, normals, uvs, colors, indices, vertexCursor, b, normal, targetTransform, in recipe, in placement);
            vertexCursor++;
            WriteWaterVertex(positions, normals, uvs, colors, indices, vertexCursor, c, normal, targetTransform, in recipe, in placement);
            vertexCursor++;
        }

        private static void WriteWaterVertex(
            Vector3[] positions,
            Vector3[] normals,
            Vector2[] uvs,
            Color32[] colors,
            int[] indices,
            int vertexIndex,
            Vector3 gridPosition,
            Vector3 gridNormal,
            Transform targetTransform,
            in PlanetRecipe recipe,
            in PlanetPlacement placement)
        {
            Vector3 worldPosition = PlanetCoordinateConverter.GridToWorld(gridPosition, in recipe, in placement);
            Vector3 worldNormal = placement.PlanetRotation * gridNormal;
            positions[vertexIndex] = targetTransform != null ? targetTransform.InverseTransformPoint(worldPosition) : worldPosition;
            normals[vertexIndex] = targetTransform != null ? targetTransform.InverseTransformDirection(worldNormal).normalized : worldNormal.normalized;
            uvs[vertexIndex] = Vector2.zero;
            colors[vertexIndex] = Color.white;
            indices[vertexIndex] = vertexIndex;
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
            if (materialOverride != null)
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
