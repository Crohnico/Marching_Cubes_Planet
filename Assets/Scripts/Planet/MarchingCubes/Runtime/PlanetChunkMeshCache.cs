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
        public const string DefaultFolderName = "PlanetChunkCache";
        public const string PlanetIdFileName = "planet_id";
        public const string ChunksFolderName = "chunks";
        public const string MeshFileName = "mesh.pmesh";
        public const string ChunkDataFileName = "chunk_data.pchunk";
        public const string WaterMeshFileName = "water.pmesh";
        public const string WaterDataFileName = "water_data.pchunk";

        private string planetId;
        private string lastDiagnostic;

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
            string signature = BuildRecipeSignature(in recipe);
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
            if (!recipe.IsValid(out string recipeMessage))
            {
                lastDiagnostic = "Chunk cache prepare blocked: invalid recipe. " + recipeMessage;
                return false;
            }

            planetId = BuildPlanetId(in recipe);

            try
            {
                Directory.CreateDirectory(RootPath);
                string planetIdPath = Path.Combine(RootPath, PlanetIdFileName);
                string existingPlanetId = File.Exists(planetIdPath)
                    ? File.ReadAllText(planetIdPath, Encoding.UTF8).Trim()
                    : string.Empty;

                if (!string.IsNullOrEmpty(existingPlanetId) &&
                    !string.Equals(existingPlanetId, planetId, StringComparison.Ordinal))
                {
                    if (Directory.Exists(ChunksPath))
                    {
                        Directory.Delete(ChunksPath, true);
                    }
                }

                Directory.CreateDirectory(ChunksPath);
                File.WriteAllText(planetIdPath, planetId, Encoding.UTF8);
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
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            if (!Directory.Exists(ChunksPath))
            {
                lastDiagnostic = "Chunk cache miss: chunks folder does not exist.";
                return false;
            }

            string[] chunkDirectories = Directory.GetDirectories(ChunksPath);
            Array.Sort(chunkDirectories, StringComparer.Ordinal);
            for (int i = 0; i < chunkDirectories.Length; i++)
            {
                string chunkName = Path.GetFileName(chunkDirectories[i]);
                if (!int.TryParse(chunkName, NumberStyles.Integer, CultureInfo.InvariantCulture, out int chunkId))
                {
                    continue;
                }

                string lodDirectory = Path.Combine(chunkDirectories[i], "LOD" + Mathf.Max(0, lod));
                string surfaceMeshPath = Path.Combine(lodDirectory, MeshFileName);
                if (!File.Exists(surfaceMeshPath))
                {
                    continue;
                }

                if (!TryReadMesh(surfaceMeshPath, "PlanetChunk_" + chunkId + "_SurfaceMesh_Cached", out Mesh surfaceMesh))
                {
                    ReleaseLoadedMeshes(results);
                    return false;
                }

                Mesh waterMesh = null;
                string waterMeshPath = Path.Combine(lodDirectory, WaterMeshFileName);
                if (File.Exists(waterMeshPath) &&
                    !TryReadMesh(waterMeshPath, "PlanetChunk_" + chunkId + "_WaterMesh_Cached", out waterMesh))
                {
                    DestroyRuntimeObject(surfaceMesh);
                    ReleaseLoadedMeshes(results);
                    return false;
                }

                results.Add(new PlanetCachedChunkMesh(chunkId, lod, surfaceMesh, waterMesh));
            }

            if (results.Count <= 0)
            {
                lastDiagnostic = "Chunk cache miss: no LOD" + Mathf.Max(0, lod) + " .pmesh files were found.";
                return false;
            }

            lastDiagnostic = "Chunk cache loaded " + results.Count + " chunks for LOD" + Mathf.Max(0, lod) + ".";
            return true;
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
                Directory.CreateDirectory(lodDirectory);

                if (surfaceMesh != null)
                {
                    WriteMesh(Path.Combine(lodDirectory, MeshFileName), surfaceMesh);
                    WriteChunkData(Path.Combine(lodDirectory, ChunkDataFileName), chunkId, lod, surfaceMesh, false);
                }

                if (waterMesh != null)
                {
                    WriteMesh(Path.Combine(lodDirectory, WaterMeshFileName), waterMesh);
                    WriteChunkData(Path.Combine(lodDirectory, WaterDataFileName), chunkId, lod, waterMesh, true);
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

        private static string BuildRecipeSignature(in PlanetRecipe recipe)
        {
            StringBuilder builder = new StringBuilder(1024);
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

        private static void WriteMesh(string path, Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] uvs = mesh.uv;
            Color32[] colors = mesh.colors32;
            int[] indices = mesh.GetIndices(0);
            bool hasNormals = normals != null && normals.Length == vertices.Length;
            bool hasUvs = uvs != null && uvs.Length == vertices.Length;
            bool hasColors = colors != null && colors.Length == vertices.Length;

            using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None)))
            {
                writer.Write(vertices.Length);
                writer.Write(indices.Length);
                WriteBounds(writer, mesh.bounds);
                writer.Write(hasNormals);
                writer.Write(hasUvs);
                writer.Write(hasColors);

                for (int i = 0; i < vertices.Length; i++)
                {
                    WriteVector3(writer, vertices[i]);
                }

                if (hasNormals)
                {
                    for (int i = 0; i < normals.Length; i++)
                    {
                        WriteVector3(writer, normals[i]);
                    }
                }

                if (hasUvs)
                {
                    for (int i = 0; i < uvs.Length; i++)
                    {
                        WriteVector2(writer, uvs[i]);
                    }
                }

                if (hasColors)
                {
                    for (int i = 0; i < colors.Length; i++)
                    {
                        Color32 color = colors[i];
                        writer.Write(color.r);
                        writer.Write(color.g);
                        writer.Write(color.b);
                        writer.Write(color.a);
                    }
                }

                for (int i = 0; i < indices.Length; i++)
                {
                    writer.Write(indices[i]);
                }
            }
        }

        private bool TryReadMesh(string path, string meshName, out Mesh mesh)
        {
            mesh = null;
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

                    Vector3[] vertices = new Vector3[vertexCount];
                    for (int i = 0; i < vertexCount; i++)
                    {
                        vertices[i] = ReadVector3(reader);
                    }

                    Vector3[] normals = null;
                    if (hasNormals)
                    {
                        normals = new Vector3[vertexCount];
                        for (int i = 0; i < vertexCount; i++)
                        {
                            normals[i] = ReadVector3(reader);
                        }
                    }

                    Vector2[] uvs = null;
                    if (hasUvs)
                    {
                        uvs = new Vector2[vertexCount];
                        for (int i = 0; i < vertexCount; i++)
                        {
                            uvs[i] = ReadVector2(reader);
                        }
                    }

                    Color32[] colors = null;
                    if (hasColors)
                    {
                        colors = new Color32[vertexCount];
                        for (int i = 0; i < vertexCount; i++)
                        {
                            colors[i] = new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
                        }
                    }

                    int[] indices = new int[indexCount];
                    for (int i = 0; i < indexCount; i++)
                    {
                        indices[i] = reader.ReadInt32();
                    }

                    mesh = new Mesh
                    {
                        name = meshName,
                        indexFormat = vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
                    };
                    mesh.vertices = vertices;
                    if (normals != null)
                    {
                        mesh.normals = normals;
                    }

                    if (uvs != null)
                    {
                        mesh.uv = uvs;
                    }

                    if (colors != null)
                    {
                        mesh.colors32 = colors;
                    }

                    mesh.SetIndices(indices, MeshTopology.Triangles, 0, true);
                    mesh.bounds = bounds;
                    return true;
                }
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
    }

    public sealed class PlanetCachedChunkMesh
    {
        public PlanetCachedChunkMesh(int chunkId, int lod, Mesh surfaceMesh, Mesh waterMesh)
        {
            ChunkId = chunkId;
            Lod = lod;
            SurfaceMesh = surfaceMesh;
            WaterMesh = waterMesh;
        }

        public int ChunkId { get; }
        public int Lod { get; }
        public Mesh SurfaceMesh { get; private set; }
        public Mesh WaterMesh { get; private set; }
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
}
