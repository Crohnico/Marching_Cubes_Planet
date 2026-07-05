using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MarchingCubesPlanet.Coordinates;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarchingCubesPlanet.MarchingCubes
{
    public sealed class PlanetChunkMeshCache
    {
        private const string DebugPrefix = "[10 Chunk Cache]";
        private const string CreateColor = "#FFD400";
        private const string DeleteColor = "#FF4040";
        private const string LoadColor = "#37D67A";
        public const string DefaultFolderName = "PlanetChunkCache";
        public const string PlanetIdFileName = "planet_id";
        public const string ChunksFolderName = "chunks";
        public const string MeshFileName = "mesh.pmesh";
        public const string ChunkDataFileName = "chunk_data.pchunk";
        public const string WaterMeshFileName = "water.pmesh";
        public const string WaterDataFileName = "water_data.pchunk";

        private string planetId;
        private string lastDiagnostic;
        private readonly List<Vector3> meshCacheVertices = new List<Vector3>(65536);
        private readonly List<Vector3> meshCacheNormals = new List<Vector3>(65536);
        private readonly List<Vector2> meshCacheUvs = new List<Vector2>(65536);
        private readonly List<Color32> meshCacheColors = new List<Color32>(65536);
        private readonly List<int> meshCacheIndices = new List<int>(65536);

        public PlanetChunkMeshCache(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                throw new ArgumentException("Cache root path cannot be empty.", nameof(rootPath));
            }

            RootPath = rootPath;
        }

        public string RootPath { get; }
        public string PlanetId => planetId;
        public string LastDiagnostic => lastDiagnostic;
        public string ChunksPath => Path.Combine(RootPath, ChunksFolderName);

        public static PlanetChunkMeshCache CreateDefault()
        {
            return new PlanetChunkMeshCache(Path.Combine(Application.persistentDataPath, DefaultFolderName));
        }

        public static string BuildPlanetId(in PlanetRecipe recipe)
        {
            PlanetChunkLodActivationConfig activationConfig = PlanetChunkLodActivationConfig.Default();
            return BuildPlanetId(in recipe, in activationConfig);
        }

        public static string BuildPlanetId(in PlanetRecipe recipe, in PlanetChunkLodActivationConfig activationConfig)
        {
            PlanetChunkLodActivationConfig safeActivationConfig = activationConfig;
            safeActivationConfig.EnsureValid();
            string signature = BuildRecipeSignature(in recipe, in safeActivationConfig);
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < signature.Length; i++)
            {
                hash ^= signature[i];
                hash *= 1099511628211UL;
            }

            return "planet_" + hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        public bool Prepare(in PlanetRecipe recipe)
        {
            PlanetChunkLodActivationConfig activationConfig = PlanetChunkLodActivationConfig.Default();
            return Prepare(in recipe, in activationConfig);
        }

        public bool Prepare(in PlanetRecipe recipe, in PlanetChunkLodActivationConfig activationConfig)
        {
            if (!recipe.IsValid(out string recipeMessage))
            {
                lastDiagnostic = "Chunk cache prepare blocked: invalid recipe. " + recipeMessage;
                return false;
            }

            PlanetChunkLodActivationConfig safeActivationConfig = activationConfig;
            safeActivationConfig.EnsureValid();
            planetId = BuildPlanetId(in recipe, in safeActivationConfig);

            try
            {
                bool rootExists = Directory.Exists(RootPath);
                Directory.CreateDirectory(RootPath);
                if (!rootExists)
                {
                    LogCreate("Created cache root: " + RootPath);
                }

                string planetIdPath = Path.Combine(RootPath, PlanetIdFileName);
                string existingPlanetId = File.Exists(planetIdPath)
                    ? File.ReadAllText(planetIdPath, Encoding.UTF8).Trim()
                    : string.Empty;

                if (!string.IsNullOrEmpty(existingPlanetId) &&
                    !string.Equals(existingPlanetId, planetId, StringComparison.Ordinal))
                {
                    if (Directory.Exists(ChunksPath))
                    {
                        LogDelete(
                            "Deleted stale chunk cache. oldPlanetId=" + existingPlanetId +
                            " newPlanetId=" + planetId +
                            " path=" + ChunksPath);
                        Directory.Delete(ChunksPath, true);
                    }
                }

                bool chunksPathExists = Directory.Exists(ChunksPath);
                Directory.CreateDirectory(ChunksPath);
                if (!chunksPathExists)
                {
                    LogCreate("Created chunks folder: " + ChunksPath);
                }

                bool planetIdFileExists = File.Exists(planetIdPath);
                File.WriteAllText(planetIdPath, planetId, Encoding.UTF8);
                if (!planetIdFileExists || !string.Equals(existingPlanetId, planetId, StringComparison.Ordinal))
                {
                    LogCreate("Wrote planet_id: " + planetIdPath + " planetId=" + planetId);
                }

                lastDiagnostic = "Chunk cache ready. planetId=" + planetId + " root=" + RootPath;
                return true;
            }
            catch (Exception exception)
            {
                lastDiagnostic = "Chunk cache prepare failed: " + exception.Message;
                return false;
            }
        }

        public string GetChunkLodDirectory(int chunkId, int lod)
        {
            return Path.Combine(ChunksPath, Mathf.Max(0, chunkId).ToString(CultureInfo.InvariantCulture), "LOD" + Mathf.Max(0, lod));
        }

        public bool HasAnyChunkMesh(int lod)
        {
            if (!Directory.Exists(ChunksPath))
            {
                return false;
            }

            string[] chunkDirectories = Directory.GetDirectories(ChunksPath);
            for (int i = 0; i < chunkDirectories.Length; i++)
            {
                string meshPath = Path.Combine(chunkDirectories[i], "LOD" + Mathf.Max(0, lod), MeshFileName);
                if (File.Exists(meshPath))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryLoadAllChunkMeshes(int lod, List<PlanetCachedChunkMesh> results)
        {
            return TryLoadAllChunkMeshes(
                lod,
                PlanetChunkCachePayloadMode.MeshOnly,
                results,
                out PlanetChunkCacheLoadSummary _);
        }

        public bool TryLoadAllChunkMeshes(
            int lod,
            PlanetChunkCachePayloadMode payloadMode,
            List<PlanetCachedChunkMesh> results,
            out PlanetChunkCacheLoadSummary summary)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            int safeLod = Mathf.Max(0, lod);
            summary = new PlanetChunkCacheLoadSummary(safeLod, payloadMode);
            results.Clear();
            if (!Directory.Exists(ChunksPath))
            {
                lastDiagnostic = "Chunk cache miss: chunks folder does not exist.";
                return false;
            }

            List<string> lodDirectories = CollectLodDirectoriesWithMeshes(safeLod);
            for (int i = 0; i < lodDirectories.Count; i++)
            {
                string chunkName = Path.GetFileName(Path.GetDirectoryName(lodDirectories[i]));
                if (!int.TryParse(chunkName, NumberStyles.Integer, CultureInfo.InvariantCulture, out int chunkId))
                {
                    continue;
                }

                summary.RecordRequestedChunk();
                string lodDirectory = lodDirectories[i];
                string surfaceMeshPath = Path.Combine(lodDirectory, MeshFileName);
                Mesh surfaceMesh = null;
                PlanetCachedChunkData surfaceChunkData = null;
                if (File.Exists(surfaceMeshPath) &&
                    !TryReadMesh(surfaceMeshPath, "PlanetChunk_" + chunkId + "_SurfaceMesh_Cached", out surfaceMesh))
                {
                    ReleaseLoadedMeshes(results);
                    return false;
                }

                if (surfaceMesh != null &&
                    payloadMode == PlanetChunkCachePayloadMode.MeshAndChunkData &&
                    !TryReadRequiredChunkData(
                        Path.Combine(lodDirectory, ChunkDataFileName),
                        chunkId,
                        safeLod,
                        false,
                        ref summary,
                        out surfaceChunkData))
                {
                    DestroyRuntimeObject(surfaceMesh);
                    ReleaseLoadedMeshes(results);
                    return false;
                }

                Mesh waterMesh = null;
                PlanetCachedChunkData waterChunkData = null;
                string waterMeshPath = Path.Combine(lodDirectory, WaterMeshFileName);
                if (File.Exists(waterMeshPath) &&
                    !TryReadMesh(waterMeshPath, "PlanetChunk_" + chunkId + "_WaterMesh_Cached", out waterMesh))
                {
                    DestroyRuntimeObject(surfaceMesh);
                    ReleaseLoadedMeshes(results);
                    return false;
                }

                if (waterMesh != null &&
                    payloadMode == PlanetChunkCachePayloadMode.MeshAndChunkData &&
                    !TryReadRequiredChunkData(
                        Path.Combine(lodDirectory, WaterDataFileName),
                        chunkId,
                        safeLod,
                        true,
                        ref summary,
                        out waterChunkData))
                {
                    DestroyRuntimeObject(surfaceMesh);
                    DestroyRuntimeObject(waterMesh);
                    ReleaseLoadedMeshes(results);
                    return false;
                }

                results.Add(new PlanetCachedChunkMesh(
                    chunkId,
                    safeLod,
                    surfaceMesh,
                    waterMesh,
                    surfaceChunkData,
                    waterChunkData));
                summary.RecordLoadedChunk(surfaceMesh != null, waterMesh != null, surfaceChunkData != null, waterChunkData != null);
            }

            if (results.Count <= 0)
            {
                lastDiagnostic = "Chunk cache miss: no LOD" + safeLod + " .pmesh files were found.";
                return false;
            }

            lastDiagnostic = "Chunk cache loaded " + results.Count + " chunks for LOD" + safeLod +
                             ". mode=" + payloadMode +
                             " meshOnlyLoads=" + summary.MeshOnlyLoadCount +
                             " chunkDataLoads=" + summary.ChunkDataLoadCount + ".";
            return true;
        }

        public bool TryLoadChunkMesh(
            int chunkId,
            int lod,
            PlanetChunkCachePayloadMode payloadMode,
            out PlanetCachedChunkMesh result,
            out PlanetChunkCacheLoadSummary summary)
        {
            int safeChunkId = Mathf.Max(0, chunkId);
            int safeLod = Mathf.Max(0, lod);
            result = null;
            summary = new PlanetChunkCacheLoadSummary(safeLod, payloadMode);
            summary.RecordRequestedChunk();

            string lodDirectory = GetChunkLodDirectory(safeChunkId, safeLod);
            if (!Directory.Exists(lodDirectory))
            {
                lastDiagnostic = "Chunk cache miss: chunk " + safeChunkId + " LOD" + safeLod + " folder does not exist.";
                return false;
            }

            string surfaceMeshPath = Path.Combine(lodDirectory, MeshFileName);
            string waterMeshPath = Path.Combine(lodDirectory, WaterMeshFileName);
            if (!File.Exists(surfaceMeshPath) && !File.Exists(waterMeshPath))
            {
                lastDiagnostic = "Chunk cache miss: chunk " + safeChunkId + " LOD" + safeLod + " has no .pmesh files.";
                return false;
            }

            Mesh surfaceMesh = null;
            Mesh waterMesh = null;
            PlanetCachedChunkData surfaceChunkData = null;
            PlanetCachedChunkData waterChunkData = null;

            if (File.Exists(surfaceMeshPath) &&
                !TryReadMesh(surfaceMeshPath, "PlanetChunk_" + safeChunkId + "_SurfaceMesh_Cached", out surfaceMesh))
            {
                return false;
            }

            if (surfaceMesh != null &&
                payloadMode == PlanetChunkCachePayloadMode.MeshAndChunkData &&
                !TryReadRequiredChunkData(
                    Path.Combine(lodDirectory, ChunkDataFileName),
                    safeChunkId,
                    safeLod,
                    false,
                    ref summary,
                    out surfaceChunkData))
            {
                DestroyRuntimeObject(surfaceMesh);
                return false;
            }

            if (File.Exists(waterMeshPath) &&
                !TryReadMesh(waterMeshPath, "PlanetChunk_" + safeChunkId + "_WaterMesh_Cached", out waterMesh))
            {
                DestroyRuntimeObject(surfaceMesh);
                return false;
            }

            if (waterMesh != null &&
                payloadMode == PlanetChunkCachePayloadMode.MeshAndChunkData &&
                !TryReadRequiredChunkData(
                    Path.Combine(lodDirectory, WaterDataFileName),
                    safeChunkId,
                    safeLod,
                    true,
                    ref summary,
                    out waterChunkData))
            {
                DestroyRuntimeObject(surfaceMesh);
                DestroyRuntimeObject(waterMesh);
                return false;
            }

            result = new PlanetCachedChunkMesh(
                safeChunkId,
                safeLod,
                surfaceMesh,
                waterMesh,
                surfaceChunkData,
                waterChunkData);
            summary.RecordLoadedChunk(surfaceMesh != null, waterMesh != null, surfaceChunkData != null, waterChunkData != null);
            lastDiagnostic = "Chunk cache loaded chunk " + safeChunkId + " LOD" + safeLod + ". mode=" + payloadMode + ".";
            return true;
        }

        public bool TryGetChunkMeshAvailability(
            int chunkId,
            int lod,
            out bool hasSurfaceMesh,
            out bool hasWaterMesh)
        {
            int safeChunkId = Mathf.Max(0, chunkId);
            int safeLod = Mathf.Max(0, lod);
            string lodDirectory = GetChunkLodDirectory(safeChunkId, safeLod);
            hasSurfaceMesh = File.Exists(Path.Combine(lodDirectory, MeshFileName));
            hasWaterMesh = File.Exists(Path.Combine(lodDirectory, WaterMeshFileName));
            if (!hasSurfaceMesh && !hasWaterMesh)
            {
                lastDiagnostic = "Chunk cache miss: chunk " + safeChunkId + " LOD" + safeLod + " has no .pmesh files.";
                return false;
            }

            return true;
        }

        public bool TryLoadChunkMeshInto(
            int chunkId,
            int lod,
            PlanetChunkCachePayloadMode payloadMode,
            Mesh surfaceTarget,
            Mesh waterTarget,
            out bool loadedSurfaceMesh,
            out bool loadedWaterMesh,
            out PlanetChunkCacheLoadSummary summary)
        {
            int safeChunkId = Mathf.Max(0, chunkId);
            int safeLod = Mathf.Max(0, lod);
            loadedSurfaceMesh = false;
            loadedWaterMesh = false;
            summary = new PlanetChunkCacheLoadSummary(safeLod, payloadMode);
            summary.RecordRequestedChunk();

            string lodDirectory = GetChunkLodDirectory(safeChunkId, safeLod);
            if (!Directory.Exists(lodDirectory))
            {
                lastDiagnostic = "Chunk cache miss: chunk " + safeChunkId + " LOD" + safeLod + " folder does not exist.";
                return false;
            }

            string surfaceMeshPath = Path.Combine(lodDirectory, MeshFileName);
            string waterMeshPath = Path.Combine(lodDirectory, WaterMeshFileName);
            bool hasSurfaceMesh = File.Exists(surfaceMeshPath);
            bool hasWaterMesh = File.Exists(waterMeshPath);
            if (!hasSurfaceMesh && !hasWaterMesh)
            {
                lastDiagnostic = "Chunk cache miss: chunk " + safeChunkId + " LOD" + safeLod + " has no .pmesh files.";
                return false;
            }

            if (hasSurfaceMesh)
            {
                if (surfaceTarget == null)
                {
                    lastDiagnostic = "Chunk cache load failed: surface target mesh is missing for chunk " + safeChunkId + " LOD" + safeLod + ".";
                    return false;
                }

                if (!TryReadMeshInto(surfaceMeshPath, "PlanetChunk_" + safeChunkId + "_SurfaceMesh_Cached", surfaceTarget))
                {
                    return false;
                }

                loadedSurfaceMesh = true;
            }

            if (loadedSurfaceMesh &&
                payloadMode == PlanetChunkCachePayloadMode.MeshAndChunkData &&
                !TryReadRequiredChunkData(
                    Path.Combine(lodDirectory, ChunkDataFileName),
                    safeChunkId,
                    safeLod,
                    false,
                    ref summary,
                    out _))
            {
                return false;
            }

            if (hasWaterMesh)
            {
                if (waterTarget == null)
                {
                    lastDiagnostic = "Chunk cache load failed: water target mesh is missing for chunk " + safeChunkId + " LOD" + safeLod + ".";
                    return false;
                }

                if (!TryReadMeshInto(waterMeshPath, "PlanetChunk_" + safeChunkId + "_WaterMesh_Cached", waterTarget))
                {
                    return false;
                }

                loadedWaterMesh = true;
            }

            if (loadedWaterMesh &&
                payloadMode == PlanetChunkCachePayloadMode.MeshAndChunkData &&
                !TryReadRequiredChunkData(
                    Path.Combine(lodDirectory, WaterDataFileName),
                    safeChunkId,
                    safeLod,
                    true,
                    ref summary,
                    out _))
            {
                return false;
            }

            summary.RecordLoadedChunk(loadedSurfaceMesh, loadedWaterMesh, false, false);
            lastDiagnostic = "Chunk cache loaded chunk " + safeChunkId + " LOD" + safeLod + " into existing meshes. mode=" + payloadMode + ".";
            return true;
        }

        public bool TryLoadChunkWaterMeshInto(
            int chunkId,
            int lod,
            Mesh waterTarget,
            out bool loadedWaterMesh)
        {
            int safeChunkId = Mathf.Max(0, chunkId);
            int safeLod = Mathf.Max(0, lod);
            loadedWaterMesh = false;
            if (waterTarget == null)
            {
                lastDiagnostic = "Chunk cache water load failed: water target mesh is missing for chunk " + safeChunkId + " LOD" + safeLod + ".";
                return false;
            }

            string waterMeshPath = Path.Combine(GetChunkLodDirectory(safeChunkId, safeLod), WaterMeshFileName);
            if (!File.Exists(waterMeshPath))
            {
                lastDiagnostic = "Chunk cache water miss: chunk " + safeChunkId + " LOD" + safeLod + " has no water .pmesh.";
                return false;
            }

            if (!TryReadMeshInto(waterMeshPath, "PlanetChunk_" + safeChunkId + "_WaterMesh_Cached", waterTarget))
            {
                return false;
            }

            loadedWaterMesh = true;
            lastDiagnostic = "Chunk cache loaded water mesh for chunk " + safeChunkId + " LOD" + safeLod + ".";
            return true;
        }

        public bool TryLoadChunkSurfaceMeshData(
            int chunkId,
            int lod,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<Color32> colors,
            List<int> indices,
            out Bounds bounds)
        {
            bounds = default;
            if (vertices == null)
            {
                throw new ArgumentNullException(nameof(vertices));
            }

            if (normals == null)
            {
                throw new ArgumentNullException(nameof(normals));
            }

            if (uvs == null)
            {
                throw new ArgumentNullException(nameof(uvs));
            }

            if (colors == null)
            {
                throw new ArgumentNullException(nameof(colors));
            }

            if (indices == null)
            {
                throw new ArgumentNullException(nameof(indices));
            }

            int safeChunkId = Mathf.Max(0, chunkId);
            int safeLod = Mathf.Max(0, lod);
            string surfaceMeshPath = Path.Combine(GetChunkLodDirectory(safeChunkId, safeLod), MeshFileName);
            if (!File.Exists(surfaceMeshPath))
            {
                lastDiagnostic = "Chunk cache miss: chunk " + safeChunkId + " LOD" + safeLod + " has no surface .pmesh.";
                return false;
            }

            return TryReadMeshData(surfaceMeshPath, vertices, normals, uvs, colors, indices, out bounds);
        }

        public bool SaveChunk(int chunkId, int lod, Mesh surfaceMesh, Mesh waterMesh)
        {
            if (surfaceMesh == null && waterMesh == null)
            {
                lastDiagnostic = "Chunk cache save skipped: chunk has no meshes.";
                return false;
            }

            try
            {
                string lodDirectory = GetChunkLodDirectory(chunkId, lod);
                bool lodDirectoryExists = Directory.Exists(lodDirectory);
                Directory.CreateDirectory(lodDirectory);
                if (!lodDirectoryExists)
                {
                    LogCreate("Created chunk LOD folder: " + lodDirectory);
                }

                if (surfaceMesh != null)
                {
                    string surfaceMeshPath = Path.Combine(lodDirectory, MeshFileName);
                    string surfaceDataPath = Path.Combine(lodDirectory, ChunkDataFileName);
                    WriteMesh(surfaceMeshPath, surfaceMesh);
                    WriteChunkData(surfaceDataPath, chunkId, lod, surfaceMesh, false);
                    LogCreate("Wrote surface cache files: " + surfaceMeshPath + " / " + surfaceDataPath);
                }

                if (waterMesh != null)
                {
                    string waterMeshPath = Path.Combine(lodDirectory, WaterMeshFileName);
                    string waterDataPath = Path.Combine(lodDirectory, WaterDataFileName);
                    WriteMesh(waterMeshPath, waterMesh);
                    WriteChunkData(waterDataPath, chunkId, lod, waterMesh, true);
                    LogCreate("Wrote water cache files: " + waterMeshPath + " / " + waterDataPath);
                }

                lastDiagnostic = "Chunk cache saved chunk " + chunkId + " LOD" + lod + ".";
                return true;
            }
            catch (Exception exception)
            {
                lastDiagnostic = "Chunk cache save failed for chunk " + chunkId + " LOD" + lod + ": " + exception.Message;
                return false;
            }
        }

        private static string BuildRecipeSignature(in PlanetRecipe recipe, in PlanetChunkLodActivationConfig activationConfig)
        {
            StringBuilder builder = new StringBuilder(1024);
            Append(builder, "BaseRecipeLod", (int)PlanetChunkLodUtility.BaseRecipeLod);
            Append(builder, "InitialFallbackLod", (int)PlanetChunkLodUtility.InitialFallbackLod);
            Append(builder, "Lod0GridMultiplier", 2f);
            Append(builder, "Lod2GridMultiplier", 0.5f);
            Append(builder, "CanonicalChunkSize", activationConfig.CanonicalChunkSize);
            Append(builder, "Lod0ChunkSize", PlanetChunkLodUtility.GetChunkSizeForLod(PlanetChunkLod.LOD0, in activationConfig));
            Append(builder, "Lod1ChunkSize", PlanetChunkLodUtility.GetChunkSizeForLod(PlanetChunkLod.LOD1, in activationConfig));
            Append(builder, "Lod2ChunkSize", PlanetChunkLodUtility.GetChunkSizeForLod(PlanetChunkLod.LOD2, in activationConfig));
            Append(builder, "GridRadius", recipe.GridRadius);
            Append(builder, "WorldScale", recipe.WorldScale);
            Append(builder, "Seed", recipe.Seed);
            Append(builder, "IsoLevel", recipe.IsoLevel);
            Append(builder, "VoronoiDivision", recipe.VoronoiDivision);
            Append(builder, "ContinentCells", recipe.ContinentCells);
            Append(builder, "ContinentEdgeBlend", recipe.ContinentEdgeBlend);
            Append(builder, "ContinentEdgeWidthMin", recipe.ContinentEdgeWidthMin);
            Append(builder, "ContinentEdgeWidthMax", recipe.ContinentEdgeWidthMax);
            Append(builder, "ContinentEdgeShiftStrength", recipe.ContinentEdgeShiftStrength);
            Append(builder, "MinLandElevation", recipe.MinLandElevation);
            Append(builder, "MaxLandElevation", recipe.MaxLandElevation);
            Append(builder, "MinHeightModifier", recipe.MinHeightModifier);
            Append(builder, "MaxHeightModifier", recipe.MaxHeightModifier);
            Append(builder, "OceanDepth", recipe.OceanDepth);
            Append(builder, "MinimumOceanDepth", recipe.MinimumOceanDepth);
            Append(builder, "SurfaceNoiseAmplitude", recipe.SurfaceNoiseAmplitude);
            Append(builder, "SurfaceNoiseFrequency", recipe.SurfaceNoiseFrequency);
            Append(builder, "SurfaceNoiseOctaves", recipe.SurfaceNoiseOctaves);
            Append(builder, "SurfaceNoiseLacunarity", recipe.SurfaceNoiseLacunarity);
            Append(builder, "SurfaceNoisePersistence", recipe.SurfaceNoisePersistence);
            Append(builder, "SurfaceNoiseResponsePower", recipe.SurfaceNoiseResponsePower);
            Append(builder, "MountainBiomeCells", recipe.MountainBiomeCells);
            Append(builder, "MountainBiomeMinPeaks", recipe.MountainBiomeMinPeaks);
            Append(builder, "MountainBiomeMaxPeaks", recipe.MountainBiomeMaxPeaks);
            Append(builder, "MountainBiomeHeight", recipe.MountainBiomeHeight);
            Append(builder, "MountainBiomePeakRadius", recipe.MountainBiomePeakRadius);
            Append(builder, "MountainBiomePeakSpread", recipe.MountainBiomePeakSpread);
            Append(builder, "MountainBiomeEdgeBlend", recipe.MountainBiomeEdgeBlend);
            Append(builder, "MountainBiomePeakFalloff", recipe.MountainBiomePeakFalloff);
            Append(builder, "MinRoughness", recipe.MinRoughness);
            Append(builder, "MaxRoughness", recipe.MaxRoughness);
            return builder.ToString();
        }

        private static void Append(StringBuilder builder, string name, int value)
        {
            builder.Append(name).Append('=').Append(value.ToString(CultureInfo.InvariantCulture)).Append(';');
        }

        private static void Append(StringBuilder builder, string name, float value)
        {
            builder.Append(name).Append('=').Append(value.ToString("R", CultureInfo.InvariantCulture)).Append(';');
        }

        private void WriteMesh(string path, Mesh mesh)
        {
            meshCacheVertices.Clear();
            meshCacheNormals.Clear();
            meshCacheUvs.Clear();
            meshCacheColors.Clear();
            meshCacheIndices.Clear();
            mesh.GetVertices(meshCacheVertices);
            mesh.GetNormals(meshCacheNormals);
            mesh.GetUVs(0, meshCacheUvs);
            mesh.GetColors(meshCacheColors);
            mesh.GetTriangles(meshCacheIndices, 0);
            bool hasNormals = meshCacheNormals.Count == meshCacheVertices.Count;
            bool hasUvs = meshCacheUvs.Count == meshCacheVertices.Count;
            bool hasColors = meshCacheColors.Count == meshCacheVertices.Count;

            using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None)))
            {
                writer.Write(meshCacheVertices.Count);
                writer.Write(meshCacheIndices.Count);
                WriteBounds(writer, mesh.bounds);
                writer.Write(hasNormals);
                writer.Write(hasUvs);
                writer.Write(hasColors);

                for (int i = 0; i < meshCacheVertices.Count; i++)
                {
                    WriteVector3(writer, meshCacheVertices[i]);
                }

                if (hasNormals)
                {
                    for (int i = 0; i < meshCacheNormals.Count; i++)
                    {
                        WriteVector3(writer, meshCacheNormals[i]);
                    }
                }

                if (hasUvs)
                {
                    for (int i = 0; i < meshCacheUvs.Count; i++)
                    {
                        WriteVector2(writer, meshCacheUvs[i]);
                    }
                }

                if (hasColors)
                {
                    for (int i = 0; i < meshCacheColors.Count; i++)
                    {
                        Color32 color = meshCacheColors[i];
                        writer.Write(color.r);
                        writer.Write(color.g);
                        writer.Write(color.b);
                        writer.Write(color.a);
                    }
                }

                for (int i = 0; i < meshCacheIndices.Count; i++)
                {
                    writer.Write(meshCacheIndices[i]);
                }
            }
        }

        private bool TryReadMesh(string path, string meshName, out Mesh mesh)
        {
            mesh = null;
            try
            {
                mesh = new Mesh
                {
                    name = meshName
                };
                if (!TryReadMeshInto(path, meshName, mesh))
                {
                    DestroyRuntimeObject(mesh);
                    mesh = null;
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                lastDiagnostic = "Chunk cache load failed for " + path + ": " + exception.Message;
                if (mesh != null)
                {
                    DestroyRuntimeObject(mesh);
                    mesh = null;
                }

                return false;
            }
        }

        private bool TryReadMeshInto(string path, string meshName, Mesh mesh)
        {
            try
            {
                using (BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    int vertexCount = reader.ReadInt32();
                    int indexCount = reader.ReadInt32();
                    Bounds bounds = ReadBounds(reader);
                    bool hasNormals = reader.ReadBoolean();
                    bool hasUvs = reader.ReadBoolean();
                    bool hasColors = reader.ReadBoolean();

                    if (vertexCount < 0 || indexCount < 0 || indexCount % 3 != 0)
                    {
                        lastDiagnostic = "Chunk cache load failed: invalid mesh counts in " + path;
                        return false;
                    }

                    PrepareMeshCacheBuffers(vertexCount, indexCount);
                    for (int i = 0; i < vertexCount; i++)
                    {
                        meshCacheVertices.Add(ReadVector3(reader));
                    }

                    if (hasNormals)
                    {
                        for (int i = 0; i < vertexCount; i++)
                        {
                            meshCacheNormals.Add(ReadVector3(reader));
                        }
                    }

                    if (hasUvs)
                    {
                        for (int i = 0; i < vertexCount; i++)
                        {
                            meshCacheUvs.Add(ReadVector2(reader));
                        }
                    }

                    if (hasColors)
                    {
                        for (int i = 0; i < vertexCount; i++)
                        {
                            meshCacheColors.Add(new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte()));
                        }
                    }

                    for (int i = 0; i < indexCount; i++)
                    {
                        meshCacheIndices.Add(reader.ReadInt32());
                    }

                    mesh.name = meshName;
                    mesh.indexFormat = vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
                    mesh.Clear();
                    mesh.SetVertices(meshCacheVertices);
                    if (hasNormals)
                    {
                        mesh.SetNormals(meshCacheNormals);
                    }

                    if (hasUvs)
                    {
                        mesh.SetUVs(0, meshCacheUvs);
                    }

                    if (hasColors)
                    {
                        mesh.SetColors(meshCacheColors);
                    }

                    mesh.SetTriangles(meshCacheIndices, 0, true);
                    mesh.bounds = bounds;
                    LogLoad("Loaded mesh from disk: " + path + " vertices=" + vertexCount + " indices=" + indexCount);
                    return true;
                }
            }
            catch (Exception exception)
            {
                lastDiagnostic = "Chunk cache load failed for " + path + ": " + exception.Message;
                return false;
            }
        }

        private bool TryReadMeshData(
            string path,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<Color32> colors,
            List<int> indices,
            out Bounds bounds)
        {
            bounds = default;
            try
            {
                using (BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    int vertexCount = reader.ReadInt32();
                    int indexCount = reader.ReadInt32();
                    bounds = ReadBounds(reader);
                    bool hasNormals = reader.ReadBoolean();
                    bool hasUvs = reader.ReadBoolean();
                    bool hasColors = reader.ReadBoolean();

                    if (vertexCount < 0 || indexCount < 0 || indexCount % 3 != 0)
                    {
                        lastDiagnostic = "Chunk cache load failed: invalid mesh counts in " + path;
                        return false;
                    }

                    EnsureListCapacity(vertices, vertexCount);
                    EnsureListCapacity(normals, vertexCount);
                    EnsureListCapacity(uvs, vertexCount);
                    EnsureListCapacity(colors, vertexCount);
                    EnsureListCapacity(indices, indexCount);
                    vertices.Clear();
                    normals.Clear();
                    uvs.Clear();
                    colors.Clear();
                    indices.Clear();

                    for (int i = 0; i < vertexCount; i++)
                    {
                        vertices.Add(ReadVector3(reader));
                    }

                    if (hasNormals)
                    {
                        for (int i = 0; i < vertexCount; i++)
                        {
                            normals.Add(ReadVector3(reader));
                        }
                    }
                    else
                    {
                        for (int i = 0; i < vertexCount; i++)
                        {
                            normals.Add(Vector3.up);
                        }
                    }

                    if (hasUvs)
                    {
                        for (int i = 0; i < vertexCount; i++)
                        {
                            uvs.Add(ReadVector2(reader));
                        }
                    }
                    else
                    {
                        for (int i = 0; i < vertexCount; i++)
                        {
                            uvs.Add(Vector2.zero);
                        }
                    }

                    if (hasColors)
                    {
                        for (int i = 0; i < vertexCount; i++)
                        {
                            colors.Add(new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte()));
                        }
                    }
                    else
                    {
                        for (int i = 0; i < vertexCount; i++)
                        {
                            colors.Add(Color.white);
                        }
                    }

                    for (int i = 0; i < indexCount; i++)
                    {
                        indices.Add(reader.ReadInt32());
                    }

                    return true;
                }
            }
            catch (Exception exception)
            {
                lastDiagnostic = "Chunk cache GPU data load failed for " + path + ": " + exception.Message;
                return false;
            }
        }

        private void PrepareMeshCacheBuffers(int vertexCapacity, int indexCapacity)
        {
            EnsureListCapacity(meshCacheVertices, vertexCapacity);
            EnsureListCapacity(meshCacheNormals, vertexCapacity);
            EnsureListCapacity(meshCacheUvs, vertexCapacity);
            EnsureListCapacity(meshCacheColors, vertexCapacity);
            EnsureListCapacity(meshCacheIndices, indexCapacity);
            meshCacheVertices.Clear();
            meshCacheNormals.Clear();
            meshCacheUvs.Clear();
            meshCacheColors.Clear();
            meshCacheIndices.Clear();
        }

        private static void EnsureListCapacity<T>(List<T> list, int capacity)
        {
            if (list.Capacity < capacity)
            {
                list.Capacity = capacity;
            }
        }

        private List<string> CollectLodDirectoriesWithMeshes(int lod)
        {
            List<string> lodDirectories = new List<string>();
            if (!Directory.Exists(ChunksPath))
            {
                return lodDirectories;
            }

            string lodFolderName = "LOD" + Mathf.Max(0, lod);
            string[] meshFiles = Directory.GetFiles(ChunksPath, "*.pmesh", SearchOption.AllDirectories);
            Array.Sort(meshFiles, StringComparer.Ordinal);
            for (int i = 0; i < meshFiles.Length; i++)
            {
                string fileName = Path.GetFileName(meshFiles[i]);
                if (!string.Equals(fileName, MeshFileName, StringComparison.Ordinal) &&
                    !string.Equals(fileName, WaterMeshFileName, StringComparison.Ordinal))
                {
                    continue;
                }

                string lodDirectory = Path.GetDirectoryName(meshFiles[i]);
                if (string.IsNullOrEmpty(lodDirectory) ||
                    !string.Equals(Path.GetFileName(lodDirectory), lodFolderName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!ContainsPath(lodDirectories, lodDirectory))
                {
                    lodDirectories.Add(lodDirectory);
                }
            }

            return lodDirectories;
        }

        private static bool ContainsPath(List<string> paths, string path)
        {
            for (int i = 0; i < paths.Count; i++)
            {
                if (string.Equals(paths[i], path, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryReadRequiredChunkData(
            string path,
            int expectedChunkId,
            int expectedLod,
            bool expectedIsWater,
            ref PlanetChunkCacheLoadSummary summary,
            out PlanetCachedChunkData data)
        {
            data = null;
            if (!File.Exists(path))
            {
                summary.RecordMissingChunkData();
                lastDiagnostic = "Chunk cache load failed: required .pchunk is missing. path=" + path;
                return false;
            }

            if (!TryReadChunkData(path, expectedChunkId, expectedLod, expectedIsWater, out data))
            {
                return false;
            }

            return true;
        }

        private bool TryReadChunkData(
            string path,
            int expectedChunkId,
            int expectedLod,
            bool expectedIsWater,
            out PlanetCachedChunkData data)
        {
            data = null;
            try
            {
                using (BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    int chunkId = reader.ReadInt32();
                    int lod = reader.ReadInt32();
                    bool isWater = reader.ReadBoolean();
                    int vertexCount = reader.ReadInt32();
                    int triangleCount = reader.ReadInt32();
                    Bounds bounds = ReadBounds(reader);
                    int borderPayloadCount = reader.ReadInt32();

                    if (chunkId != expectedChunkId || lod != expectedLod || isWater != expectedIsWater)
                    {
                        lastDiagnostic = "Chunk cache load failed: .pchunk identity mismatch in " + path;
                        return false;
                    }

                    if (vertexCount < 0 || triangleCount < 0 || borderPayloadCount < 0)
                    {
                        lastDiagnostic = "Chunk cache load failed: invalid .pchunk counts in " + path;
                        return false;
                    }

                    data = new PlanetCachedChunkData(chunkId, lod, isWater, vertexCount, triangleCount, bounds, borderPayloadCount);
                    LogLoad(
                        "Loaded chunk data from disk: " + path +
                        " triangles=" + triangleCount +
                        " borderPayloadCount=" + borderPayloadCount);
                    return true;
                }
            }
            catch (Exception exception)
            {
                lastDiagnostic = "Chunk cache load failed for " + path + ": " + exception.Message;
                return false;
            }
        }

        private static void WriteChunkData(string path, int chunkId, int lod, Mesh mesh, bool isWater)
        {
            int vertexCount = mesh != null ? mesh.vertexCount : 0;
            int indexCount = mesh != null ? (int)mesh.GetIndexCount(0) : 0;
            using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None)))
            {
                writer.Write(chunkId);
                writer.Write(lod);
                writer.Write(isWater);
                writer.Write(vertexCount);
                writer.Write(indexCount / 3);
                WriteBounds(writer, mesh != null ? mesh.bounds : new Bounds());
                writer.Write(0);
            }
        }

        private static void WriteBounds(BinaryWriter writer, Bounds bounds)
        {
            WriteVector3(writer, bounds.center);
            WriteVector3(writer, bounds.size);
        }

        private static Bounds ReadBounds(BinaryReader reader)
        {
            Vector3 center = ReadVector3(reader);
            Vector3 size = ReadVector3(reader);
            return new Bounds(center, size);
        }

        private static void WriteVector3(BinaryWriter writer, Vector3 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }

        private static Vector3 ReadVector3(BinaryReader reader)
        {
            return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        private static void WriteVector2(BinaryWriter writer, Vector2 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
        }

        private static Vector2 ReadVector2(BinaryReader reader)
        {
            return new Vector2(reader.ReadSingle(), reader.ReadSingle());
        }

        private static void ReleaseLoadedMeshes(List<PlanetCachedChunkMesh> loadedChunks)
        {
            for (int i = 0; i < loadedChunks.Count; i++)
            {
                loadedChunks[i].ReleaseMeshes();
            }

            loadedChunks.Clear();
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

        private static void LogCreate(string message)
        {
            Debug.Log(FormatColor(CreateColor, "CREATE " + message));
        }

        private static void LogDelete(string message)
        {
            Debug.Log(FormatColor(DeleteColor, "DELETE " + message));
        }

        private static void LogLoad(string message)
        {
            Debug.Log(FormatColor(LoadColor, "LOAD " + message));
        }

        private static string FormatColor(string color, string message)
        {
            return "<color=" + color + ">" + DebugPrefix + " " + message + "</color>";
        }
    }

    public sealed class PlanetCachedChunkMesh
    {
        public PlanetCachedChunkMesh(int chunkId, int lod, Mesh surfaceMesh, Mesh waterMesh)
            : this(chunkId, lod, surfaceMesh, waterMesh, null, null)
        {
        }

        public PlanetCachedChunkMesh(
            int chunkId,
            int lod,
            Mesh surfaceMesh,
            Mesh waterMesh,
            PlanetCachedChunkData surfaceChunkData,
            PlanetCachedChunkData waterChunkData)
        {
            ChunkId = chunkId;
            Lod = lod;
            SurfaceMesh = surfaceMesh;
            WaterMesh = waterMesh;
            SurfaceChunkData = surfaceChunkData;
            WaterChunkData = waterChunkData;
        }

        public int ChunkId { get; }
        public int Lod { get; }
        public Mesh SurfaceMesh { get; private set; }
        public Mesh WaterMesh { get; private set; }
        public PlanetCachedChunkData SurfaceChunkData { get; }
        public PlanetCachedChunkData WaterChunkData { get; }
        public bool HasAnyChunkData => SurfaceChunkData != null || WaterChunkData != null;
        public int SurfaceVertexCount => SurfaceMesh != null ? SurfaceMesh.vertexCount : 0;
        public int SurfaceTriangleCount => SurfaceMesh != null ? (int)SurfaceMesh.GetIndexCount(0) / 3 : 0;
        public int WaterVertexCount => WaterMesh != null ? WaterMesh.vertexCount : 0;
        public int WaterTriangleCount => WaterMesh != null ? (int)WaterMesh.GetIndexCount(0) / 3 : 0;
        public long SurfaceEstimatedBytes => PlanetMarchingCubesPaintResult.CalculateMeshEstimatedBytes(
            SurfaceVertexCount,
            SurfaceTriangleCount);
        public long WaterEstimatedBytes => PlanetMarchingCubesPaintResult.CalculateMeshEstimatedBytes(
            WaterVertexCount,
            WaterTriangleCount);

        public void ReleaseMeshes()
        {
            DestroyMesh(SurfaceMesh);
            DestroyMesh(WaterMesh);
            SurfaceMesh = null;
            WaterMesh = null;
        }

        private static void DestroyMesh(Mesh mesh)
        {
            if (mesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(mesh);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
    }

    public enum PlanetChunkCachePayloadMode
    {
        MeshOnly = 0,
        MeshAndChunkData = 1
    }

    public struct PlanetChunkCacheLoadSummary
    {
        public PlanetChunkCacheLoadSummary(int lod, PlanetChunkCachePayloadMode payloadMode)
        {
            Lod = lod;
            PayloadMode = payloadMode;
            RequestedChunkCount = 0;
            LoadedChunkCount = 0;
            SurfaceMeshLoadCount = 0;
            WaterMeshLoadCount = 0;
            MeshOnlyLoadCount = 0;
            ChunkDataLoadCount = 0;
            MissingChunkDataCount = 0;
        }

        public int Lod { get; }
        public PlanetChunkCachePayloadMode PayloadMode { get; }
        public int RequestedChunkCount { get; private set; }
        public int LoadedChunkCount { get; private set; }
        public int SurfaceMeshLoadCount { get; private set; }
        public int WaterMeshLoadCount { get; private set; }
        public int MeshOnlyLoadCount { get; private set; }
        public int ChunkDataLoadCount { get; private set; }
        public int MissingChunkDataCount { get; private set; }

        public void RecordRequestedChunk()
        {
            RequestedChunkCount++;
        }

        public void RecordLoadedChunk(bool hasSurfaceMesh, bool hasWaterMesh, bool hasSurfaceChunkData, bool hasWaterChunkData)
        {
            LoadedChunkCount++;
            if (hasSurfaceMesh)
            {
                SurfaceMeshLoadCount++;
            }

            if (hasWaterMesh)
            {
                WaterMeshLoadCount++;
            }

            int chunkDataCount = 0;
            if (hasSurfaceChunkData)
            {
                chunkDataCount++;
            }

            if (hasWaterChunkData)
            {
                chunkDataCount++;
            }

            ChunkDataLoadCount += chunkDataCount;
            if (chunkDataCount == 0)
            {
                MeshOnlyLoadCount++;
            }
        }

        public void RecordMissingChunkData()
        {
            MissingChunkDataCount++;
        }
    }

    public sealed class PlanetCachedChunkData
    {
        public PlanetCachedChunkData(
            int chunkId,
            int lod,
            bool isWater,
            int vertexCount,
            int triangleCount,
            Bounds bounds,
            int borderPayloadCount)
        {
            ChunkId = chunkId;
            Lod = lod;
            IsWater = isWater;
            VertexCount = vertexCount;
            TriangleCount = triangleCount;
            Bounds = bounds;
            BorderPayloadCount = borderPayloadCount;
        }

        public int ChunkId { get; }
        public int Lod { get; }
        public bool IsWater { get; }
        public int VertexCount { get; }
        public int TriangleCount { get; }
        public Bounds Bounds { get; }
        public int BorderPayloadCount { get; }
    }
}
