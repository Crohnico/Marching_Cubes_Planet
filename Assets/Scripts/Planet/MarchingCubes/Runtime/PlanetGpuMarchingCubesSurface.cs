using System;
using System.Collections;
using System.IO;
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
        private const string ShapeShaderAsset = "Assets/Shaders/Resources/Compute/PlanetShapeDensity.compute";
        private const string MarchingShaderResource = "Compute/PlanetMarchingCubes";
        private const string MarchingShaderAsset = "Assets/Shaders/Resources/Compute/PlanetMarchingCubes.compute";
        private const string SurfaceGpuShaderName = "MarchingCubesPlanet/Planet/SurfaceGpu";
        private const string DefaultSurfaceMaterialResourceName = "PlanetWorld_Surface";
        private const int SurfaceAtlasResolution = 256;
        private const int IndirectArgsCount = 4;
        private const int MaxThreadGroupsPerDispatchAxis = 65535;
        private const int ShellCacheMagic = 0x4d435053;
        private const int ShellCacheVersion = 1;

        private static readonly int ShapeParametersId = Shader.PropertyToID("_PlanetShapeParameters");
        private static readonly int ShapeCellsId = Shader.PropertyToID("_PlanetShapeCells");
        private static readonly int ChunkOriginsId = Shader.PropertyToID("_MarchingCubesChunkOrigins");
        private static readonly int VerticesId = Shader.PropertyToID("_MarchingCubesVertices");
        private static readonly int StateId = Shader.PropertyToID("_MarchingCubesState");
        private static readonly int DrawArgsId = Shader.PropertyToID("_MarchingCubesDrawArgs");
        private static readonly int EdgeTableId = Shader.PropertyToID("_MarchingCubesEdgeTable");
        private static readonly int TriTableId = Shader.PropertyToID("_MarchingCubesTriTable");
        private static readonly int TransvoxelFaceDescriptorsId = Shader.PropertyToID("_TransvoxelFaceDescriptors");
        private static readonly int TransvoxelTransitionCellClassId = Shader.PropertyToID("_TransvoxelTransitionCellClass");
        private static readonly int TransvoxelTransitionCellGeometryCountsId = Shader.PropertyToID("_TransvoxelTransitionCellGeometryCounts");
        private static readonly int TransvoxelTransitionCellVertexIndicesId = Shader.PropertyToID("_TransvoxelTransitionCellVertexIndices");
        private static readonly int TransvoxelTransitionVertexDataId = Shader.PropertyToID("_TransvoxelTransitionVertexData");
        private static readonly int TransvoxelCellCountId = Shader.PropertyToID("_TransvoxelCellCount");
        private static readonly int TransvoxelCellStartIndexId = Shader.PropertyToID("_TransvoxelCellStartIndex");
        private static readonly int TransvoxelCellEndIndexId = Shader.PropertyToID("_TransvoxelCellEndIndex");
        private static readonly int TransvoxelFaceCountId = Shader.PropertyToID("_TransvoxelFaceCount");
        private static readonly int CellCountId = Shader.PropertyToID("_MarchingCubesCellCount");
        private static readonly int CellStartIndexId = Shader.PropertyToID("_MarchingCubesCellStartIndex");
        private static readonly int CellEndIndexId = Shader.PropertyToID("_MarchingCubesCellEndIndex");
        private static readonly int ChunkSizeId = Shader.PropertyToID("_MarchingCubesChunkSize");
        private static readonly int ChunkIndexBaseId = Shader.PropertyToID("_MarchingCubesChunkIndexBase");
        private static readonly int OutputPrimitiveLimitId = Shader.PropertyToID("_MarchingCubesOutputPrimitiveLimit");
        private static readonly int OutputVertexLimitId = Shader.PropertyToID("_MarchingCubesOutputVertexLimit");
        private static readonly int WriteEnabledId = Shader.PropertyToID("_MarchingCubesWriteEnabled");
        private static readonly int DrawArgsEnabledId = Shader.PropertyToID("_MarchingCubesDrawArgsEnabled");
        private static readonly int OutputGridScaleId = Shader.PropertyToID("_MarchingCubesOutputGridScale");
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
        private int transitionKernel;
        private uint threadGroupSizeX;
        private uint transitionThreadGroupSizeX;
        private ComputeBuffer chunkOriginBuffer;
        private ComputeBuffer stateBuffer;
        private ComputeBuffer edgeTableBuffer;
        private ComputeBuffer triTableBuffer;
        private ComputeBuffer transvoxelFaceBuffer;
        private ComputeBuffer transvoxelCellClassBuffer;
        private ComputeBuffer transvoxelCellGeometryBuffer;
        private ComputeBuffer transvoxelCellVertexIndexBuffer;
        private ComputeBuffer transvoxelVertexDataBuffer;
        private bool transvoxelTablesUploaded;
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
        private readonly GpuSurfaceSlot[] chunkAggregateSlots =
        {
            new GpuSurfaceSlot("Chunks A"),
            new GpuSurfaceSlot("Chunks B")
        };
        private int visibleChunkAggregateSlotIndex;
        private int buildChunkAggregateSlotIndex;
        private bool chunkAggregateOpen;
        private bool chunkAggregatePublished;

        public int CandidateChunkCount => candidateCoordinates.Length;

        private GpuSurfaceSlot VisibleChunkAggregateSlot => chunkAggregateSlots[visibleChunkAggregateSlotIndex];
        private GpuSurfaceSlot BuildChunkAggregateSlot => chunkAggregateSlots[buildChunkAggregateSlotIndex];

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
            for (int i = 0; i < chunkAggregateSlots.Length; i++)
            {
                ApplySlotPlacement(chunkAggregateSlots[i], in placement, ref materialUpdated);
            }
        }

        public void BeginChunkSequence(bool keepShellVisible = false, bool keepCurrentChunksVisible = false)
        {
            if (!keepShellVisible)
            {
                shellSlot.Release();
            }

            if (keepCurrentChunksVisible && VisibleChunkAggregateSlot.hasDrawable)
            {
                buildChunkAggregateSlotIndex = 1 - visibleChunkAggregateSlotIndex;
                BuildChunkAggregateSlot.Release();
            }
            else
            {
                visibleChunkAggregateSlotIndex = 0;
                buildChunkAggregateSlotIndex = 0;
                ReleaseChunkAggregateSlots();
                chunkAggregatePublished = false;
            }

            chunkAggregateOpen = true;
        }

        public void CompleteChunkSequence()
        {
            if (!chunkAggregateOpen)
            {
                return;
            }

            if (buildChunkAggregateSlotIndex != visibleChunkAggregateSlotIndex)
            {
                int oldVisibleSlotIndex = visibleChunkAggregateSlotIndex;
                visibleChunkAggregateSlotIndex = buildChunkAggregateSlotIndex;
                StartCoroutine(ReleaseChunkAggregateSlotAfterFrame(oldVisibleSlotIndex));
            }

            chunkAggregatePublished = BuildChunkAggregateSlot.hasDrawable;
            chunkAggregateOpen = false;
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
            ReleaseChunkAggregateSlots();
            chunkAggregatePublished = false;
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
                true,
                in lodRecipe,
                1f);
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
                ReleaseChunkAggregateSlots();
                chunkAggregatePublished = false;
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
                true,
                in lodRecipe,
                1f);
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
                BuildChunkAggregateSlot,
                !BuildChunkAggregateSlot.hasDrawable,
                in lodRecipe,
                1f);
            chunkAggregatePublished = BuildChunkAggregateSlot.hasDrawable;
        }

        public void GenerateChunkIntoBase(
            PlanetRecipe recipe,
            PlanetPlacement placement,
            PlanetChunkLod lod,
            PlanetGridCoordinates chunkId,
            MeshRenderer materialSource,
            int outputVertexCapacity,
            PlanetTransvoxelFaceDescriptor[] transitionFaces = null,
            int transitionFaceCount = 0)
        {
            PlanetRecipe lodRecipe = PlanetChunkLodUtility.BuildRecipeForLod(in recipe, lod);
            singleChunkOrigins[0] = chunkId.ToChunkOrigin(lod);
            if (!chunkAggregateOpen)
            {
                BeginChunkSequence();
            }

            float outputGridScale = Mathf.Max(0.000001f, lodRecipe.WorldScale / Mathf.Max(0.000001f, recipe.WorldScale));
            Generate(
                in lodRecipe,
                in placement,
                materialSource,
                singleChunkOrigins,
                1,
                PlanetChunkLodUtility.GetChunkSizeForLod(lod),
                Mathf.Max(1, outputVertexCapacity),
                0,
                BuildChunkAggregateSlot,
                !BuildChunkAggregateSlot.hasDrawable,
                in recipe,
                outputGridScale,
                transitionFaces,
                transitionFaceCount);
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
            if (chunkAggregatePublished)
            {
                RenderSlot(VisibleChunkAggregateSlot);
            }
        }

        public void ReleaseShell()
        {
            shellSlot.Release();
        }

        public void ReleaseChunkAggregate()
        {
            ReleaseChunkAggregateSlots();
            chunkAggregateOpen = false;
            chunkAggregatePublished = false;
        }

        public bool TryLoadShellCache(
            string path,
            PlanetRecipe recipe,
            PlanetPlacement placement,
            PlanetChunkLod lod,
            MeshRenderer materialSource)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                PlanetRecipe lodRecipe = PlanetChunkLodUtility.BuildRecipeForLod(in recipe, lod);
                string expectedPlanetId = PlanetChunkMeshCache.BuildPlanetId(in recipe);
                using (BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    if (reader.ReadInt32() != ShellCacheMagic ||
                        reader.ReadInt32() != ShellCacheVersion ||
                        reader.ReadInt32() != PlanetMarchingCubesVertex.Stride ||
                        reader.ReadInt32() != (int)lod ||
                        !string.Equals(reader.ReadString(), expectedPlanetId, StringComparison.Ordinal))
                    {
                        return false;
                    }

                    int vertexCapacity = reader.ReadInt32();
                    int drawArgsCount = reader.ReadInt32();
                    if (vertexCapacity <= 0 || drawArgsCount != IndirectArgsCount)
                    {
                        return false;
                    }

                    uint[] drawArgs = new uint[IndirectArgsCount];
                    for (int i = 0; i < drawArgs.Length; i++)
                    {
                        drawArgs[i] = reader.ReadUInt32();
                    }

                    PlanetMarchingCubesVertex[] vertices = new PlanetMarchingCubesVertex[vertexCapacity];
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        vertices[i] = ReadVertex(reader);
                    }

                    EnsureShellCacheBuffers(vertexCapacity);
                    shellSlot.vertexBuffer.SetData(vertices, 0, 0, vertexCapacity);
                    shellSlot.drawArgsBuffer.SetData(drawArgs, 0, 0, IndirectArgsCount);

                    EnsureShapeCells(in lodRecipe);
                    ResolveMaterial(materialSource, shapeCells);
                    shellSlot.renderRecipe = lodRecipe;
                    shellSlot.hasRenderRecipe = true;
                    shellSlot.hasDrawable = true;
                    SetPlacement(placement);

                    ReleaseChunkAggregateSlots();
                    chunkAggregatePublished = false;
                    chunkAggregateOpen = false;
                    return true;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Shell GPU cache load failed: " + exception.Message, this);
                return false;
            }
        }

        public bool TrySaveShellCache(string path, PlanetRecipe recipe, PlanetChunkLod lod)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                shellSlot.vertexBuffer == null ||
                shellSlot.drawArgsBuffer == null ||
                !shellSlot.hasDrawable)
            {
                return false;
            }

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                int vertexCapacity = shellSlot.vertexBuffer.count;
                PlanetMarchingCubesVertex[] vertices = new PlanetMarchingCubesVertex[vertexCapacity];
                uint[] drawArgs = new uint[IndirectArgsCount];
                shellSlot.vertexBuffer.GetData(vertices, 0, 0, vertexCapacity);
                shellSlot.drawArgsBuffer.GetData(drawArgs, 0, 0, IndirectArgsCount);

                using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None)))
                {
                    writer.Write(ShellCacheMagic);
                    writer.Write(ShellCacheVersion);
                    writer.Write(PlanetMarchingCubesVertex.Stride);
                    writer.Write((int)lod);
                    writer.Write(PlanetChunkMeshCache.BuildPlanetId(in recipe));
                    writer.Write(vertexCapacity);
                    writer.Write(IndirectArgsCount);
                    for (int i = 0; i < drawArgs.Length; i++)
                    {
                        writer.Write(drawArgs[i]);
                    }

                    for (int i = 0; i < vertices.Length; i++)
                    {
                        WriteVertex(writer, vertices[i]);
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Shell GPU cache save failed: " + exception.Message, this);
                return false;
            }
        }

        private void ReleaseChunkAggregateSlots()
        {
            for (int i = 0; i < chunkAggregateSlots.Length; i++)
            {
                chunkAggregateSlots[i].Release();
            }
        }

        private IEnumerator ReleaseChunkAggregateSlotAfterFrame(int slotIndex)
        {
            yield return new WaitForEndOfFrame();
            if (slotIndex != visibleChunkAggregateSlotIndex)
            {
                chunkAggregateSlots[slotIndex].Release();
            }
        }

        public void Release()
        {
            shapeEvaluator.Release();
            ReleaseBuffer(ref triTableBuffer);
            ReleaseBuffer(ref edgeTableBuffer);
            ReleaseBuffer(ref transvoxelVertexDataBuffer);
            ReleaseBuffer(ref transvoxelCellVertexIndexBuffer);
            ReleaseBuffer(ref transvoxelCellGeometryBuffer);
            ReleaseBuffer(ref transvoxelCellClassBuffer);
            ReleaseBuffer(ref transvoxelFaceBuffer);
            transvoxelTablesUploaded = false;
            ReleaseBuffer(ref stateBuffer);
            ReleaseBuffer(ref chunkOriginBuffer);
            shellSlot.Release();
            ReleaseChunkAggregateSlots();
            chunkAggregateOpen = false;
            chunkAggregatePublished = false;
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
            bool resetDrawArgs,
            in PlanetRecipe renderRecipe,
            float outputGridScale,
            PlanetTransvoxelFaceDescriptor[] transitionFaces = null,
            int transitionFaceCount = 0)
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
            transitionKernel = marchingShader.FindKernel("CS_ExtractTransitionSurface");
            marchingShader.GetKernelThreadGroupSizes(extractKernel, out threadGroupSizeX, out _, out _);
            marchingShader.GetKernelThreadGroupSizes(transitionKernel, out transitionThreadGroupSizeX, out _, out _);

            long cellsPerChunk = (long)Mathf.Max(1, chunkSize) * chunkSize * chunkSize;
            long activeCellCount = originCount * cellsPerChunk;
            if (activeCellCount > uint.MaxValue)
            {
                throw new InvalidOperationException("GPU visual Marching Cubes cell count exceeds the current 32-bit dispatch contract.");
            }

            EnsureBuffers(originCount, Mathf.Max(1, outputVertexCapacity), slot);
            int safeTransitionFaceCount = PrepareTransvoxelFaces(transitionFaces, transitionFaceCount);
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
            marchingShader.SetInt(TransvoxelFaceCountId, safeTransitionFaceCount);
            marchingShader.SetFloat(OutputGridScaleId, Mathf.Max(0.000001f, outputGridScale));
            Dispatch(activeCellCount);
            DispatchTransitions(slot, safeTransitionFaceCount, chunkSize, Mathf.Max(1, outputVertexCapacity));

            ResolveMaterial(materialSource, shapeCells);
            slot.renderRecipe = renderRecipe;
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
            transvoxelFaceBuffer = EnsureBuffer(transvoxelFaceBuffer, "Planet GPU Transvoxel Face Descriptors", 1, PlanetTransvoxelFaceDescriptor.Stride, ComputeBufferType.Structured);
        }

        private void EnsureShellCacheBuffers(int vertexCapacity)
        {
            shellSlot.vertexBuffer = EnsureBuffer(shellSlot.vertexBuffer, "Planet GPU MC Shell Vertices", Mathf.Max(1, vertexCapacity), PlanetMarchingCubesVertex.Stride, ComputeBufferType.Structured);
            shellSlot.drawArgsBuffer = EnsureBuffer(shellSlot.drawArgsBuffer, "Planet GPU MC Shell Draw Args", IndirectArgsCount, sizeof(uint), ComputeBufferType.IndirectArguments);
        }

        private void EnsureTransvoxelBuffers(int faceCount)
        {
            bool needsTableUpload =
                transvoxelCellClassBuffer == null ||
                transvoxelCellGeometryBuffer == null ||
                transvoxelCellVertexIndexBuffer == null ||
                transvoxelVertexDataBuffer == null;
            transvoxelFaceBuffer = EnsureBuffer(transvoxelFaceBuffer, "Planet GPU Transvoxel Face Descriptors", Mathf.Max(1, faceCount), PlanetTransvoxelFaceDescriptor.Stride, ComputeBufferType.Structured);
            transvoxelCellClassBuffer = EnsureBuffer(transvoxelCellClassBuffer, "Planet GPU Transvoxel Cell Class", PlanetTransvoxelLookupTables.TransitionCellClass.Length, sizeof(uint), ComputeBufferType.Structured);
            transvoxelCellGeometryBuffer = EnsureBuffer(transvoxelCellGeometryBuffer, "Planet GPU Transvoxel Cell Geometry", PlanetTransvoxelLookupTables.TransitionCellGeometryCounts.Length, sizeof(uint), ComputeBufferType.Structured);
            transvoxelCellVertexIndexBuffer = EnsureBuffer(transvoxelCellVertexIndexBuffer, "Planet GPU Transvoxel Cell Vertex Indices", PlanetTransvoxelLookupTables.TransitionCellVertexIndices.Length, sizeof(uint), ComputeBufferType.Structured);
            transvoxelVertexDataBuffer = EnsureBuffer(transvoxelVertexDataBuffer, "Planet GPU Transvoxel Vertex Data", PlanetTransvoxelLookupTables.TransitionVertexData.Length, sizeof(uint), ComputeBufferType.Structured);
            if (needsTableUpload)
            {
                transvoxelTablesUploaded = false;
            }
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
            marchingShader.SetBuffer(extractKernel, TransvoxelFaceDescriptorsId, transvoxelFaceBuffer);
        }

        private void BindTransitionBuffers(GpuSurfaceSlot slot)
        {
            marchingShader.SetBuffer(transitionKernel, ShapeParametersId, shapeEvaluator.ParameterBuffer.ComputeBuffer);
            marchingShader.SetBuffer(transitionKernel, ShapeCellsId, shapeEvaluator.CellBuffer.ComputeBuffer);
            marchingShader.SetBuffer(transitionKernel, VerticesId, slot.vertexBuffer);
            marchingShader.SetBuffer(transitionKernel, StateId, stateBuffer);
            marchingShader.SetBuffer(transitionKernel, DrawArgsId, slot.drawArgsBuffer);
            marchingShader.SetBuffer(transitionKernel, TransvoxelFaceDescriptorsId, transvoxelFaceBuffer);
            marchingShader.SetBuffer(transitionKernel, TransvoxelTransitionCellClassId, transvoxelCellClassBuffer);
            marchingShader.SetBuffer(transitionKernel, TransvoxelTransitionCellGeometryCountsId, transvoxelCellGeometryBuffer);
            marchingShader.SetBuffer(transitionKernel, TransvoxelTransitionCellVertexIndicesId, transvoxelCellVertexIndexBuffer);
            marchingShader.SetBuffer(transitionKernel, TransvoxelTransitionVertexDataId, transvoxelVertexDataBuffer);
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

        private int PrepareTransvoxelFaces(
            PlanetTransvoxelFaceDescriptor[] transitionFaces,
            int transitionFaceCount)
        {
            if (transitionFaces == null || transitionFaceCount <= 0)
            {
                return 0;
            }

            int safeFaceCount = Mathf.Min(transitionFaceCount, transitionFaces.Length);
            if (safeFaceCount <= 0)
            {
                return 0;
            }

            EnsureTransvoxelBuffers(safeFaceCount);
            transvoxelFaceBuffer.SetData(transitionFaces, 0, 0, safeFaceCount);
            return safeFaceCount;
        }

        private void DispatchTransitions(
            GpuSurfaceSlot slot,
            int safeFaceCount,
            int chunkSize,
            int outputVertexCapacity)
        {
            if (safeFaceCount <= 0)
            {
                return;
            }

            long cellsPerFace = (long)Mathf.Max(1, chunkSize) * chunkSize;
            long activeCellCount = safeFaceCount * cellsPerFace;
            if (activeCellCount <= 0 || activeCellCount > uint.MaxValue)
            {
                return;
            }

            EnsureTransvoxelBuffers(safeFaceCount);
            if (!transvoxelTablesUploaded)
            {
                transvoxelCellClassBuffer.SetData(PlanetTransvoxelLookupTables.TransitionCellClass);
                transvoxelCellGeometryBuffer.SetData(PlanetTransvoxelLookupTables.TransitionCellGeometryCounts);
                transvoxelCellVertexIndexBuffer.SetData(PlanetTransvoxelLookupTables.TransitionCellVertexIndices);
                transvoxelVertexDataBuffer.SetData(PlanetTransvoxelLookupTables.TransitionVertexData);
                transvoxelTablesUploaded = true;
            }
            BindTransitionBuffers(slot);
            marchingShader.SetInt(OutputPrimitiveLimitId, Mathf.Max(1, outputVertexCapacity) / 3);
            marchingShader.SetInt(OutputVertexLimitId, Mathf.Max(1, outputVertexCapacity));
            marchingShader.SetInt(WriteEnabledId, 1);
            marchingShader.SetInt(DrawArgsEnabledId, 1);

            uint safeThreadGroupSizeX = transitionThreadGroupSizeX == 0u ? 1u : transitionThreadGroupSizeX;
            long maxCellsPerDispatch = MaxThreadGroupsPerDispatchAxis * (long)safeThreadGroupSizeX;
            long cellStartIndex = 0L;
            while (cellStartIndex < activeCellCount)
            {
                long dispatchCellCount = Math.Min(activeCellCount - cellStartIndex, maxCellsPerDispatch);
                int groupCount = Mathf.CeilToInt(dispatchCellCount / (float)safeThreadGroupSizeX);
                marchingShader.SetInt(TransvoxelCellCountId, unchecked((int)(uint)activeCellCount));
                marchingShader.SetInt(TransvoxelCellStartIndexId, unchecked((int)(uint)cellStartIndex));
                marchingShader.SetInt(TransvoxelCellEndIndexId, unchecked((int)(uint)(cellStartIndex + dispatchCellCount)));
                marchingShader.Dispatch(transitionKernel, groupCount, 1, 1);
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

        private static PlanetMarchingCubesVertex ReadVertex(BinaryReader reader)
        {
            return new PlanetMarchingCubesVertex
            {
                positionAndCase = ReadVector4(reader),
                normalAndDiagnostic = ReadVector4(reader)
            };
        }

        private static void WriteVertex(BinaryWriter writer, PlanetMarchingCubesVertex vertex)
        {
            WriteVector4(writer, vertex.positionAndCase);
            WriteVector4(writer, vertex.normalAndDiagnostic);
        }

        private static Vector4 ReadVector4(BinaryReader reader)
        {
            return new Vector4(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle());
        }

        private static void WriteVector4(BinaryWriter writer, Vector4 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
            writer.Write(value.w);
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
