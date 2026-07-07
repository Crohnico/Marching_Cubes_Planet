using System;
using MarchingCubesPlanet.Compute;
using MarchingCubesPlanet.Coordinates;
using MarchingCubesPlanet.Shape;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarchingCubesPlanet.MarchingCubes
{
    [DisallowMultipleComponent]
    public sealed class PlanetGpuMarchingCubesSurface : MonoBehaviour
    {
        private const string ShapeShaderResource = "Compute/PlanetShapeDensity";
        private const string ShapeShaderAsset = "Assets/Shaders/Compute/PlanetShapeDensity.compute";
        private const string MarchingShaderResource = "Compute/PlanetMarchingCubes";
        private const string MarchingShaderAsset = "Assets/Shaders/Compute/PlanetMarchingCubes.compute";
        private const string SurfaceGpuShaderName = "MarchingCubesPlanet/Planet/SurfaceGpu";
        private const string DefaultSurfaceMaterialResourceName = "PlanetWorld_Surface";
        private const int SurfaceAtlasResolution = 256;
        private const int IndirectArgsCount = 4;
        private const int MaxThreadGroupsPerDispatchAxis = 65535;

        private static readonly int ShapeParametersId = Shader.PropertyToID("_PlanetShapeParameters");
        private static readonly int ShapeCellsId = Shader.PropertyToID("_PlanetShapeCells");
        private static readonly int ChunkOriginsId = Shader.PropertyToID("_MarchingCubesChunkOrigins");
        private static readonly int VerticesId = Shader.PropertyToID("_MarchingCubesVertices");
        private static readonly int StateId = Shader.PropertyToID("_MarchingCubesState");
        private static readonly int DrawArgsId = Shader.PropertyToID("_MarchingCubesDrawArgs");
        private static readonly int EdgeTableId = Shader.PropertyToID("_MarchingCubesEdgeTable");
        private static readonly int TriTableId = Shader.PropertyToID("_MarchingCubesTriTable");
        private static readonly int CellCountId = Shader.PropertyToID("_MarchingCubesCellCount");
        private static readonly int CellStartIndexId = Shader.PropertyToID("_MarchingCubesCellStartIndex");
        private static readonly int CellEndIndexId = Shader.PropertyToID("_MarchingCubesCellEndIndex");
        private static readonly int ChunkSizeId = Shader.PropertyToID("_MarchingCubesChunkSize");
        private static readonly int ChunkIndexBaseId = Shader.PropertyToID("_MarchingCubesChunkIndexBase");
        private static readonly int OutputPrimitiveLimitId = Shader.PropertyToID("_MarchingCubesOutputPrimitiveLimit");
        private static readonly int OutputVertexLimitId = Shader.PropertyToID("_MarchingCubesOutputVertexLimit");
        private static readonly int WriteEnabledId = Shader.PropertyToID("_MarchingCubesWriteEnabled");
        private static readonly int DrawArgsEnabledId = Shader.PropertyToID("_MarchingCubesDrawArgsEnabled");
        private static readonly int PlanetMarchingVerticesId = Shader.PropertyToID("_PlanetMarchingCubesVertices");
        private static readonly int PlanetTriangleVerticesId = Shader.PropertyToID("_PlanetTriangleVertices");
        private static readonly int PlanetGpuVertexLayoutId = Shader.PropertyToID("_PlanetGpuVertexLayout");
        private static readonly int PlanetGridToWorldMatrixId = Shader.PropertyToID("_PlanetGridToWorldMatrix");
        private static readonly int PlanetGridRadiusId = Shader.PropertyToID("_PlanetGridRadius");
        private static readonly int PlanetOceanDepthId = Shader.PropertyToID("_PlanetOceanDepth");
        private static readonly int PlanetMinimumOceanDepthId = Shader.PropertyToID("_PlanetMinimumOceanDepth");
        private static readonly int PlanetSurfaceNoiseAmplitudeId = Shader.PropertyToID("_PlanetSurfaceNoiseAmplitude");
        private static readonly int PlanetMaxLandElevationId = Shader.PropertyToID("_PlanetMaxLandElevation");
        private static readonly int PlanetMaxHeightModifierId = Shader.PropertyToID("_PlanetMaxHeightModifier");
        private static readonly int PlanetMountainBiomeHeightId = Shader.PropertyToID("_PlanetMountainBiomeHeight");
        private static readonly int SurfaceAtlasTextureId = Shader.PropertyToID("_PlanetSurfaceAtlas");
        private static readonly int UseSurfaceAtlasId = Shader.PropertyToID("_UsePlanetSurfaceAtlas");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");

        private readonly PlanetGpuShapeEvaluator shapeEvaluator = new PlanetGpuShapeEvaluator();
        private readonly PlanetMarchingCubesState[] stateUpload = new PlanetMarchingCubesState[1];
        private readonly uint[] drawArgsUpload = { 0u, 1u, 0u, 0u };

        private ComputeShader marchingShader;
        private int extractKernel;
        private uint threadGroupSizeX;
        private ComputeBuffer chunkOriginBuffer;
        private ComputeBuffer stateBuffer;
        private ComputeBuffer edgeTableBuffer;
        private ComputeBuffer triTableBuffer;
        private Material runtimeMaterial;
        private Material sourceMaterial;
        private MaterialPropertyBlock properties;
        private Texture2D runtimeSurfaceAtlas;
        private Color32[] surfaceAtlasPixels = Array.Empty<Color32>();
        private PlanetGpuShapeCell[] shapeCells = Array.Empty<PlanetGpuShapeCell>();
        private PlanetMarchingCubesChunkOrigin[] candidateOrigins = Array.Empty<PlanetMarchingCubesChunkOrigin>();
        private PlanetGridCoordinates[] candidateCoordinates = Array.Empty<PlanetGridCoordinates>();
        private readonly PlanetMarchingCubesChunkOrigin[] singleChunkOrigins = new PlanetMarchingCubesChunkOrigin[1];
        private readonly GpuSurfaceSlot shellSlot = new GpuSurfaceSlot("Shell");
        private readonly GpuSurfaceSlot chunkAggregateSlot = new GpuSurfaceSlot("Chunks");
        private bool chunkAggregateOpen;

        public int CandidateChunkCount => candidateCoordinates.Length;

        private void Update()
        {
            Render();
        }

        public int PrepareCandidateChunks(PlanetRecipe recipe, PlanetChunkLod lod)
        {
            PlanetRecipe lodRecipe = PlanetChunkLodUtility.BuildRecipeForLod(in recipe, lod);
            PlanetMarchingCubesSettings settings = PlanetMarchingCubesSettings.Default();
            settings.chunkRange.chunkSize = PlanetChunkLodUtility.GetChunkSizeForLod(lod);
            candidateOrigins = settings.chunkRange.BuildCandidateChunks(in lodRecipe, out _);
            EnsureCandidateCoordinateCapacity(candidateOrigins.Length);

            for (int i = 0; i < candidateOrigins.Length; i++)
            {
                candidateCoordinates[i] = PlanetGridCoordinates.FromChunkOrigin(
                    candidateOrigins[i],
                    settings.chunkRange.chunkSize);
            }

            return candidateOrigins.Length;
        }

        public bool TryGetCandidateChunk(int index, out PlanetGridCoordinates coordinates)
        {
            if (index >= 0 && index < candidateCoordinates.Length)
            {
                coordinates = candidateCoordinates[index];
                return true;
            }

            coordinates = default;
            return false;
        }

        public void SetPlacement(PlanetPlacement placement)
        {
            bool materialUpdated = false;
            ApplySlotPlacement(shellSlot, in placement, ref materialUpdated);
            ApplySlotPlacement(chunkAggregateSlot, in placement, ref materialUpdated);
        }

        public void BeginChunkSequence(bool keepShellVisible = false)
        {
            if (!keepShellVisible)
            {
                shellSlot.Release();
            }

            chunkAggregateSlot.Release();
            chunkAggregateOpen = true;
        }

        public void GenerateShell(
            PlanetRecipe recipe,
            PlanetPlacement placement,
            PlanetChunkLod lod,
            MeshRenderer materialSource)
        {
            PlanetRecipe lodRecipe = PlanetChunkLodUtility.BuildRecipeForLod(in recipe, lod);
            PlanetMarchingCubesSettings settings = PlanetMarchingCubesSettings.Default();
            settings.chunkRange.chunkSize = PlanetChunkLodUtility.GetChunkSizeForLod(lod);
            PrepareCandidateChunks(recipe, lod);
            chunkAggregateSlot.Release();
            chunkAggregateOpen = false;
            Generate(
                in lodRecipe,
                in placement,
                materialSource,
                candidateOrigins,
                candidateOrigins.Length,
                settings.chunkRange.chunkSize,
                settings.outputVertexCapacity,
                0,
                shellSlot,
                true);
        }

        public void GenerateShell(
            PlanetRecipe recipe,
            PlanetPlacement placement,
            PlanetChunkLod lod,
            MeshRenderer materialSource,
            bool keepChunkAggregateVisible)
        {
            PlanetRecipe lodRecipe = PlanetChunkLodUtility.BuildRecipeForLod(in recipe, lod);
            PlanetMarchingCubesSettings settings = PlanetMarchingCubesSettings.Default();
            settings.chunkRange.chunkSize = PlanetChunkLodUtility.GetChunkSizeForLod(lod);
            PrepareCandidateChunks(recipe, lod);
            if (!keepChunkAggregateVisible)
            {
                chunkAggregateSlot.Release();
            }

            chunkAggregateOpen = false;
            Generate(
                in lodRecipe,
                in placement,
                materialSource,
                candidateOrigins,
                candidateOrigins.Length,
                settings.chunkRange.chunkSize,
                settings.outputVertexCapacity,
                0,
                shellSlot,
                true);
        }

        public void GenerateChunk(
            PlanetRecipe recipe,
            PlanetPlacement placement,
            PlanetChunkLod lod,
            PlanetGridCoordinates chunkId,
            MeshRenderer materialSource)
        {
            PlanetRecipe lodRecipe = PlanetChunkLodUtility.BuildRecipeForLod(in recipe, lod);
            singleChunkOrigins[0] = chunkId.ToChunkOrigin(lod);
            if (!chunkAggregateOpen)
            {
                BeginChunkSequence();
            }

            Generate(
                in lodRecipe,
                in placement,
                materialSource,
                singleChunkOrigins,
                1,
                PlanetChunkLodUtility.GetChunkSizeForLod(lod),
                PlanetMarchingCubesSettings.DefaultOutputVertexCapacity,
                0,
                chunkAggregateSlot,
                !chunkAggregateSlot.hasDrawable);
        }

        public void Render()
        {
            if (runtimeMaterial == null)
            {
                return;
            }

            if (properties == null)
            {
                properties = new MaterialPropertyBlock();
            }

            RenderSlot(shellSlot);
            RenderSlot(chunkAggregateSlot);
        }

        public void ReleaseShell()
        {
            shellSlot.Release();
        }

        public void ReleaseChunkAggregate()
        {
            chunkAggregateSlot.Release();
            chunkAggregateOpen = false;
        }

        public void Release()
        {
            shapeEvaluator.Release();
            ReleaseBuffer(ref triTableBuffer);
            ReleaseBuffer(ref edgeTableBuffer);
            ReleaseBuffer(ref stateBuffer);
            ReleaseBuffer(ref chunkOriginBuffer);
            shellSlot.Release();
            chunkAggregateSlot.Release();
            chunkAggregateOpen = false;
            DestroyUnityObject(runtimeMaterial);
            DestroyUnityObject(runtimeSurfaceAtlas);
            runtimeMaterial = null;
            runtimeSurfaceAtlas = null;
            sourceMaterial = null;
            candidateOrigins = Array.Empty<PlanetMarchingCubesChunkOrigin>();
            candidateCoordinates = Array.Empty<PlanetGridCoordinates>();
            shapeCells = Array.Empty<PlanetGpuShapeCell>();
            surfaceAtlasPixels = Array.Empty<Color32>();
        }

        private void OnDestroy()
        {
            Release();
        }

        private void Generate(
            in PlanetRecipe lodRecipe,
            in PlanetPlacement placement,
            MeshRenderer materialSource,
            PlanetMarchingCubesChunkOrigin[] origins,
            int originCount,
            int chunkSize,
            int outputVertexCapacity,
            int chunkIndexBase,
            GpuSurfaceSlot slot,
            bool resetDrawArgs)
        {
            if (origins == null || originCount <= 0)
            {
                slot.hasDrawable = false;
                return;
            }

            EnsureShapeCells(in lodRecipe);
            ComputeShader shapeShader = LoadShader(ShapeShaderResource, ShapeShaderAsset);
            shapeEvaluator.InitializeReusable(shapeShader, in lodRecipe, shapeCells, PlanetGpuBufferMode.ComputeBuffer);

            marchingShader = LoadShader(MarchingShaderResource, MarchingShaderAsset);
            if (marchingShader == null)
            {
                throw new InvalidOperationException("Planet Marching Cubes compute shader was not found.");
            }

            extractKernel = marchingShader.FindKernel("CS_ExtractChunkedCartesianSurface");
            marchingShader.GetKernelThreadGroupSizes(extractKernel, out threadGroupSizeX, out _, out _);

            long cellsPerChunk = (long)Mathf.Max(1, chunkSize) * chunkSize * chunkSize;
            long activeCellCount = originCount * cellsPerChunk;
            if (activeCellCount > uint.MaxValue)
            {
                throw new InvalidOperationException("GPU visual Marching Cubes cell count exceeds the current 32-bit dispatch contract.");
            }

            EnsureBuffers(originCount, Mathf.Max(1, outputVertexCapacity), slot);
            chunkOriginBuffer.SetData(origins, 0, 0, originCount);
            edgeTableBuffer.SetData(PlanetMarchingCubesLookupTables.EdgeTable);
            triTableBuffer.SetData(PlanetMarchingCubesLookupTables.TriTable);
            ResetState(originCount);
            if (resetDrawArgs)
            {
                ResetDrawArgs(slot);
            }

            BindBuffers(slot);
            marchingShader.SetInt(ChunkSizeId, Mathf.Max(1, chunkSize));
            marchingShader.SetInt(ChunkIndexBaseId, Mathf.Max(0, chunkIndexBase));
            marchingShader.SetInt(OutputPrimitiveLimitId, Mathf.Max(1, outputVertexCapacity) / 3);
            marchingShader.SetInt(OutputVertexLimitId, Mathf.Max(1, outputVertexCapacity));
            marchingShader.SetInt(WriteEnabledId, 1);
            marchingShader.SetInt(DrawArgsEnabledId, 1);
            Dispatch(activeCellCount);

            ResolveMaterial(materialSource, shapeCells);
            slot.renderRecipe = lodRecipe;
            slot.hasRenderRecipe = true;
            slot.hasDrawable = true;
            SetPlacement(placement);
        }

        private void EnsureBuffers(int chunkOriginCount, int vertexCapacity, GpuSurfaceSlot slot)
        {
            chunkOriginBuffer = EnsureBuffer(chunkOriginBuffer, "Planet GPU MC Chunk Origins", Mathf.Max(1, chunkOriginCount), PlanetMarchingCubesChunkOrigin.Stride, ComputeBufferType.Structured);
            slot.vertexBuffer = EnsureBuffer(slot.vertexBuffer, "Planet GPU MC " + slot.name + " Vertices", Mathf.Max(1, vertexCapacity), PlanetMarchingCubesVertex.Stride, ComputeBufferType.Structured);
            stateBuffer = EnsureBuffer(stateBuffer, "Planet GPU MC State", 1, PlanetMarchingCubesState.Stride, ComputeBufferType.Structured);
            slot.drawArgsBuffer = EnsureBuffer(slot.drawArgsBuffer, "Planet GPU MC " + slot.name + " Draw Args", IndirectArgsCount, sizeof(uint), ComputeBufferType.IndirectArguments);
            edgeTableBuffer = EnsureBuffer(edgeTableBuffer, "Planet GPU MC Edge Table", PlanetMarchingCubesLookupTables.EdgeTable.Length, sizeof(uint), ComputeBufferType.Structured);
            triTableBuffer = EnsureBuffer(triTableBuffer, "Planet GPU MC Tri Table", PlanetMarchingCubesLookupTables.TriTable.Length, sizeof(int), ComputeBufferType.Structured);
        }

        private static ComputeBuffer EnsureBuffer(ComputeBuffer buffer, string name, int count, int stride, ComputeBufferType type)
        {
            if (buffer != null && buffer.count >= count && buffer.stride == stride)
            {
                return buffer;
            }

            ReleaseBuffer(ref buffer);
            return new ComputeBuffer(count, stride, type)
            {
                name = name
            };
        }

        private void BindBuffers(GpuSurfaceSlot slot)
        {
            marchingShader.SetBuffer(extractKernel, ShapeParametersId, shapeEvaluator.ParameterBuffer.ComputeBuffer);
            marchingShader.SetBuffer(extractKernel, ShapeCellsId, shapeEvaluator.CellBuffer.ComputeBuffer);
            marchingShader.SetBuffer(extractKernel, ChunkOriginsId, chunkOriginBuffer);
            marchingShader.SetBuffer(extractKernel, VerticesId, slot.vertexBuffer);
            marchingShader.SetBuffer(extractKernel, StateId, stateBuffer);
            marchingShader.SetBuffer(extractKernel, DrawArgsId, slot.drawArgsBuffer);
            marchingShader.SetBuffer(extractKernel, EdgeTableId, edgeTableBuffer);
            marchingShader.SetBuffer(extractKernel, TriTableId, triTableBuffer);
        }

        private void Dispatch(long activeCellCount)
        {
            if (activeCellCount <= 0)
            {
                return;
            }

            uint safeThreadGroupSizeX = threadGroupSizeX == 0u ? 1u : threadGroupSizeX;
            long maxCellsPerDispatch = MaxThreadGroupsPerDispatchAxis * (long)safeThreadGroupSizeX;
            long cellStartIndex = 0L;
            while (cellStartIndex < activeCellCount)
            {
                long dispatchCellCount = Math.Min(activeCellCount - cellStartIndex, maxCellsPerDispatch);
                int groupCount = Mathf.CeilToInt(dispatchCellCount / (float)safeThreadGroupSizeX);
                marchingShader.SetInt(CellCountId, unchecked((int)(uint)activeCellCount));
                marchingShader.SetInt(CellStartIndexId, unchecked((int)(uint)cellStartIndex));
                marchingShader.SetInt(CellEndIndexId, unchecked((int)(uint)(cellStartIndex + dispatchCellCount)));
                marchingShader.Dispatch(extractKernel, groupCount, 1, 1);
                cellStartIndex += dispatchCellCount;
            }
        }

        private void ResetState(int activeChunkCount)
        {
            stateUpload[0] = new PlanetMarchingCubesState
            {
                chunkCountCandidate = (uint)Mathf.Max(0, activeChunkCount),
                chunkCountProcessed = (uint)Mathf.Max(0, activeChunkCount)
            };
            stateBuffer.SetData(stateUpload, 0, 0, 1);
        }

        private void ResetDrawArgs(GpuSurfaceSlot slot)
        {
            drawArgsUpload[0] = 0u;
            drawArgsUpload[1] = 1u;
            drawArgsUpload[2] = 0u;
            drawArgsUpload[3] = 0u;
            slot.drawArgsBuffer.SetData(drawArgsUpload, 0, 0, IndirectArgsCount);
        }

        private void ResolveMaterial(MeshRenderer materialSource, PlanetGpuShapeCell[] cells)
        {
            Material nextSource = materialSource != null && materialSource.sharedMaterial != null
                ? materialSource.sharedMaterial
                : Resources.Load<Material>(DefaultSurfaceMaterialResourceName);

            if (runtimeMaterial == null || sourceMaterial != nextSource)
            {
                DestroyUnityObject(runtimeMaterial);
                Shader gpuShader = Shader.Find(SurfaceGpuShaderName);
                if (gpuShader == null)
                {
                    throw new InvalidOperationException("Planet GPU surface shader was not found.");
                }

                runtimeMaterial = nextSource != null
                    ? new Material(nextSource)
                    : new Material(gpuShader);
                runtimeMaterial.name = "PlanetGpuMarchingCubesSurface_Material_Runtime";
                runtimeMaterial.shader = gpuShader;
                sourceMaterial = nextSource;
            }

            CopyTextureProperty(nextSource, runtimeMaterial, BaseMapId);
            CopyColorProperty(nextSource, runtimeMaterial, BaseColorId);
            CopyFloatProperty(nextSource, runtimeMaterial, SmoothnessId);
            CopyFloatProperty(nextSource, runtimeMaterial, MetallicId);
            bool hasSourceBaseMap = nextSource != null &&
                                    nextSource.HasProperty(BaseMapId) &&
                                    nextSource.GetTexture(BaseMapId) != null;
            if (hasSourceBaseMap)
            {
                runtimeMaterial.SetFloat(UseSurfaceAtlasId, 0f);
            }
            else
            {
                EnsureSurfaceAtlas(cells);
                runtimeMaterial.SetFloat(UseSurfaceAtlasId, 1f);
                runtimeMaterial.SetTexture(SurfaceAtlasTextureId, runtimeSurfaceAtlas);
            }
        }

        private void ApplyMaterialProperties(in PlanetRecipe recipe, in PlanetPlacement placement)
        {
            Matrix4x4 gridToWorld = Matrix4x4.TRS(
                placement.PlanetWorldCenter,
                placement.PlanetRotation,
                Vector3.one * recipe.WorldScale);
            runtimeMaterial.SetFloat(PlanetGpuVertexLayoutId, 1f);
            runtimeMaterial.SetMatrix(PlanetGridToWorldMatrixId, gridToWorld);
            runtimeMaterial.SetFloat(PlanetGridRadiusId, recipe.GridRadius);
            runtimeMaterial.SetFloat(PlanetOceanDepthId, recipe.OceanDepth);
            runtimeMaterial.SetFloat(PlanetMinimumOceanDepthId, recipe.MinimumOceanDepth);
            runtimeMaterial.SetFloat(PlanetSurfaceNoiseAmplitudeId, recipe.SurfaceNoiseAmplitude);
            runtimeMaterial.SetFloat(PlanetMaxLandElevationId, recipe.MaxLandElevation);
            runtimeMaterial.SetFloat(PlanetMaxHeightModifierId, recipe.MaxHeightModifier);
            runtimeMaterial.SetFloat(PlanetMountainBiomeHeightId, recipe.MountainBiomeHeight);
        }

        private void EnsureShapeCells(in PlanetRecipe recipe)
        {
            if (shapeCells.Length != recipe.VoronoiDivision)
            {
                shapeCells = new PlanetGpuShapeCell[recipe.VoronoiDivision];
            }

            PlanetGpuShapeCellBuilder.Build(in recipe, shapeCells);
        }

        private void EnsureSurfaceAtlas(PlanetGpuShapeCell[] cells)
        {
            int atlasWidth = Mathf.Max(1, cells == null ? 0 : cells.Length);
            if (runtimeSurfaceAtlas != null && runtimeSurfaceAtlas.width == atlasWidth)
            {
                return;
            }

            DestroyUnityObject(runtimeSurfaceAtlas);
            runtimeSurfaceAtlas = new Texture2D(atlasWidth, SurfaceAtlasResolution, TextureFormat.RGBA32, false, true)
            {
                name = "PlanetGpuMarchingCubesSurface_SurfaceAtlas_Runtime",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            int pixelCount = atlasWidth * SurfaceAtlasResolution;
            if (surfaceAtlasPixels.Length != pixelCount)
            {
                surfaceAtlasPixels = new Color32[pixelCount];
            }

            for (int x = 0; x < atlasWidth; x++)
            {
                for (int y = 0; y < SurfaceAtlasResolution; y++)
                {
                    float t = y / (float)(SurfaceAtlasResolution - 1);
                    surfaceAtlasPixels[y * atlasWidth + x] = EvaluateSurfaceCellGradient(t, x);
                }
            }

            runtimeSurfaceAtlas.SetPixels32(surfaceAtlasPixels);
            runtimeSurfaceAtlas.Apply(false, false);
        }

        private void EnsureCandidateCoordinateCapacity(int count)
        {
            if (candidateCoordinates.Length != count)
            {
                candidateCoordinates = new PlanetGridCoordinates[count];
            }
        }

        private void RenderSlot(GpuSurfaceSlot slot)
        {
            if (slot == null ||
                !slot.hasDrawable ||
                slot.vertexBuffer == null ||
                slot.drawArgsBuffer == null)
            {
                return;
            }

            properties.SetBuffer(PlanetMarchingVerticesId, slot.vertexBuffer);
            properties.SetBuffer(PlanetTriangleVerticesId, slot.vertexBuffer);
            Graphics.DrawProceduralIndirect(
                runtimeMaterial,
                slot.drawBounds,
                MeshTopology.Triangles,
                slot.drawArgsBuffer,
                0,
                null,
                properties,
                ShadowCastingMode.On,
                true,
                gameObject.layer);
        }

        private void ApplySlotPlacement(GpuSurfaceSlot slot, in PlanetPlacement placement, ref bool materialUpdated)
        {
            if (slot == null || !slot.hasDrawable || !slot.hasRenderRecipe)
            {
                return;
            }

            if (!materialUpdated && runtimeMaterial != null)
            {
                ApplyMaterialProperties(in slot.renderRecipe, in placement);
                materialUpdated = true;
            }

            slot.drawBounds = CalculateDrawBounds(in slot.renderRecipe, in placement);
        }

        private static Bounds CalculateDrawBounds(in PlanetRecipe recipe, in PlanetPlacement placement)
        {
            PlanetMarchingCubesChunkRange.CalculateSurfaceShell(
                in recipe,
                PlanetMarchingCubesChunkRange.DefaultSafetyMargin,
                out _,
                out float outerRadius);
            float worldRadius = Mathf.Max(1f, outerRadius * recipe.WorldScale);
            return new Bounds(placement.PlanetWorldCenter, Vector3.one * worldRadius * 2.25f);
        }

        private static void CopyTextureProperty(Material source, Material target, int propertyId)
        {
            if (source == null || target == null || !source.HasProperty(propertyId) || !target.HasProperty(propertyId))
            {
                return;
            }

            target.SetTexture(propertyId, source.GetTexture(propertyId));
        }

        private static void CopyColorProperty(Material source, Material target, int propertyId)
        {
            if (source == null || target == null || !source.HasProperty(propertyId) || !target.HasProperty(propertyId))
            {
                return;
            }

            target.SetColor(propertyId, source.GetColor(propertyId));
        }

        private static void CopyFloatProperty(Material source, Material target, int propertyId)
        {
            if (source == null || target == null || !source.HasProperty(propertyId) || !target.HasProperty(propertyId))
            {
                return;
            }

            target.SetFloat(propertyId, source.GetFloat(propertyId));
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
            if (height01 < 0.34f) return Color.Lerp(darkRedBrown, roseRed, (height01 - 0.24f) / 0.10f);
            if (height01 < 0.46f) return Color.Lerp(roseRed, sand, (height01 - 0.34f) / 0.12f);
            if (height01 < 0.54f) return Color.Lerp(sand, paleYellow, (height01 - 0.46f) / 0.08f);
            if (height01 < 0.66f) return Color.Lerp(paleYellow, brightGreen, (height01 - 0.54f) / 0.12f);
            if (height01 < 0.76f) return Color.Lerp(brightGreen, darkGreen, (height01 - 0.66f) / 0.10f);
            if (height01 < 0.84f) return Color.Lerp(darkGreen, brown, (height01 - 0.76f) / 0.08f);
            if (height01 < 0.91f) return Color.Lerp(brown, darkGrey, (height01 - 0.84f) / 0.07f);
            if (height01 < 0.96f) return Color.Lerp(darkGrey, grey, (height01 - 0.91f) / 0.05f);
            if (height01 < 0.985f) return Color.Lerp(grey, lightGrey, (height01 - 0.96f) / 0.025f);
            return Color.Lerp(lightGrey, snow, (height01 - 0.985f) / 0.015f);
        }

        private static float Hash01(uint value, uint salt)
        {
            uint x = value ^ salt;
            x ^= x >> 16;
            x *= 0x7feb352du;
            x ^= x >> 15;
            x *= 0x846ca68bu;
            x ^= x >> 16;
            return (x & 0x00ffffffu) / 16777215f;
        }

        private static void ReleaseBuffer(ref ComputeBuffer buffer)
        {
            if (buffer == null)
            {
                return;
            }

            buffer.Release();
            buffer = null;
        }

        private static void DestroyUnityObject(UnityEngine.Object instance)
        {
            if (instance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(instance);
            }
            else
            {
                DestroyImmediate(instance);
            }
        }

        private static ComputeShader LoadShader(string resourcePath, string assetPath)
        {
            ComputeShader shader = Resources.Load<ComputeShader>(resourcePath);
#if UNITY_EDITOR
            if (shader == null)
            {
                shader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(assetPath);
            }
#endif
            return shader;
        }

        private sealed class GpuSurfaceSlot
        {
            public readonly string name;
            public ComputeBuffer vertexBuffer;
            public ComputeBuffer drawArgsBuffer;
            public Bounds drawBounds;
            public PlanetRecipe renderRecipe;
            public bool hasRenderRecipe;
            public bool hasDrawable;

            public GpuSurfaceSlot(string name)
            {
                this.name = name;
            }

            public void Release()
            {
                hasDrawable = false;
                hasRenderRecipe = false;
                ReleaseBuffer(ref vertexBuffer);
                ReleaseBuffer(ref drawArgsBuffer);
            }
        }
    }
}
