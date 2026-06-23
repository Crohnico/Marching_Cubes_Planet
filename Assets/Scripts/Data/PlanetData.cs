using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;

namespace MarchingCubesPlanet.VoxelEngine.Data
{
    public sealed class PlanetData
    {
        private const int Magic = 0x504C4454;
        private const int FormatVersion = 2;

        public string stellarID = string.Empty;
        public string planetID = string.Empty;
        public float3 worldPosition;
        public float radius;
        public int seed;
        public int3 chunkSize;
        public int segmentCount;
        public int3 segmentGrid;
        public HashSet<int3> declaredChunks = new HashSet<int3>();
        public List<PlanetChunkBuildData> chunks = new List<PlanetChunkBuildData>();
        public PlanetMeshData farMesh;

        public byte[] ToBinary()
        {
            using MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);

            writer.Write(Magic);
            writer.Write(FormatVersion);
            writer.Write(stellarID ?? string.Empty);
            writer.Write(planetID ?? string.Empty);
            WriteFloat3(writer, worldPosition);
            writer.Write(radius);
            writer.Write(seed);
            WriteInt3(writer, chunkSize);
            writer.Write(segmentCount);
            WriteInt3(writer, segmentGrid);

            writer.Write(declaredChunks.Count);
            foreach (int3 coord in declaredChunks)
            {
                WriteInt3(writer, coord);
            }

            writer.Write(chunks.Count);
            for (int i = 0; i < chunks.Count; i++)
            {
                WriteChunk(writer, chunks[i]);
            }

            WriteMesh(writer, farMesh);
            return stream.ToArray();
        }

        public static PlanetData FromBinary(byte[] binary)
        {
            PlanetData data = new PlanetData();
            if (binary == null || binary.Length == 0)
            {
                return data;
            }

            using MemoryStream stream = new MemoryStream(binary);
            using BinaryReader reader = new BinaryReader(stream);

            int first = reader.ReadInt32();
            if (first != Magic)
            {
                ReadLegacy(data, reader, first);
                return data;
            }

            int version = reader.ReadInt32();
            if (version != FormatVersion)
            {
                throw new InvalidDataException($"Unsupported PlanetData version {version}.");
            }

            data.stellarID = reader.ReadString();
            data.planetID = reader.ReadString();
            data.worldPosition = ReadFloat3(reader);
            data.radius = reader.ReadSingle();
            data.seed = reader.ReadInt32();
            data.chunkSize = ReadInt3(reader);
            data.segmentCount = reader.ReadInt32();
            data.segmentGrid = ReadInt3(reader);

            int declaredChunkCount = reader.ReadInt32();
            for (int i = 0; i < declaredChunkCount; i++)
            {
                data.declaredChunks.Add(ReadInt3(reader));
            }

            int chunkCount = reader.ReadInt32();
            data.chunks.Capacity = math.max(data.chunks.Capacity, chunkCount);
            for (int i = 0; i < chunkCount; i++)
            {
                data.chunks.Add(ReadChunk(reader));
            }

            data.farMesh = ReadMesh(reader);
            return data;
        }

        private static void ReadLegacy(PlanetData data, BinaryReader reader, int declaredChunkCount)
        {
            for (int i = 0; i < declaredChunkCount; i++)
            {
                data.declaredChunks.Add(ReadInt3(reader));
            }

            int chunkCount = reader.ReadInt32();
            data.chunks.Capacity = math.max(data.chunks.Capacity, chunkCount);
            for (int i = 0; i < chunkCount; i++)
            {
                data.chunks.Add(new PlanetChunkBuildData
                {
                    coord = ReadInt3(reader),
                    segmentId = reader.ReadInt32(),
                    cellSize = reader.ReadInt32(),
                    detailFocusKey = ReadInt3(reader)
                });
            }
        }

        private static void WriteChunk(BinaryWriter writer, PlanetChunkBuildData chunk)
        {
            WriteInt3(writer, chunk.coord);
            writer.Write(chunk.segmentId);
            writer.Write(chunk.cellSize);
            WriteInt3(writer, chunk.detailFocusKey);
            WriteInt3(writer, chunk.origin);
            WriteFloat3(writer, chunk.boundsCenter);
            WriteFloat3(writer, chunk.boundsSize);
            WriteMesh(writer, chunk.mesh);
        }

        private static PlanetChunkBuildData ReadChunk(BinaryReader reader)
        {
            return new PlanetChunkBuildData
            {
                coord = ReadInt3(reader),
                segmentId = reader.ReadInt32(),
                cellSize = reader.ReadInt32(),
                detailFocusKey = ReadInt3(reader),
                origin = ReadInt3(reader),
                boundsCenter = ReadFloat3(reader),
                boundsSize = ReadFloat3(reader),
                mesh = ReadMesh(reader)
            };
        }

        private static void WriteMesh(BinaryWriter writer, PlanetMeshData mesh)
        {
            writer.Write(mesh != null);
            if (mesh == null)
            {
                return;
            }

            WriteFloat3(writer, mesh.boundsCenter);
            WriteFloat3(writer, mesh.boundsSize);
            WriteFloat3List(writer, mesh.vertices);
            WriteFloat3List(writer, mesh.normals);
            WriteFloat2List(writer, mesh.uvs);
            WriteIntList(writer, mesh.interiorIndices);
            WriteIntList(writer, mesh.transitionIndices);
            WriteIntList(writer, mesh.surfaceIndices);
        }

        private static PlanetMeshData ReadMesh(BinaryReader reader)
        {
            if (!reader.ReadBoolean())
            {
                return null;
            }

            return new PlanetMeshData
            {
                boundsCenter = ReadFloat3(reader),
                boundsSize = ReadFloat3(reader),
                vertices = ReadFloat3List(reader),
                normals = ReadFloat3List(reader),
                uvs = ReadFloat2List(reader),
                interiorIndices = ReadIntList(reader),
                transitionIndices = ReadIntList(reader),
                surfaceIndices = ReadIntList(reader)
            };
        }

        private static void WriteInt3(BinaryWriter writer, int3 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }

        private static int3 ReadInt3(BinaryReader reader)
        {
            return new int3(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
        }

        private static void WriteFloat3(BinaryWriter writer, float3 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }

        private static float3 ReadFloat3(BinaryReader reader)
        {
            return new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        private static void WriteFloat3List(BinaryWriter writer, List<float3> values)
        {
            writer.Write(values != null ? values.Count : 0);
            if (values == null)
            {
                return;
            }

            for (int i = 0; i < values.Count; i++)
            {
                WriteFloat3(writer, values[i]);
            }
        }

        private static List<float3> ReadFloat3List(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            List<float3> values = new List<float3>(count);
            for (int i = 0; i < count; i++)
            {
                values.Add(ReadFloat3(reader));
            }

            return values;
        }

        private static void WriteFloat2List(BinaryWriter writer, List<float2> values)
        {
            writer.Write(values != null ? values.Count : 0);
            if (values == null)
            {
                return;
            }

            for (int i = 0; i < values.Count; i++)
            {
                writer.Write(values[i].x);
                writer.Write(values[i].y);
            }
        }

        private static List<float2> ReadFloat2List(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            List<float2> values = new List<float2>(count);
            for (int i = 0; i < count; i++)
            {
                values.Add(new float2(reader.ReadSingle(), reader.ReadSingle()));
            }

            return values;
        }

        private static void WriteIntList(BinaryWriter writer, List<int> values)
        {
            writer.Write(values != null ? values.Count : 0);
            if (values == null)
            {
                return;
            }

            for (int i = 0; i < values.Count; i++)
            {
                writer.Write(values[i]);
            }
        }

        private static List<int> ReadIntList(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            List<int> values = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                values.Add(reader.ReadInt32());
            }

            return values;
        }
    }

    public sealed class PlanetChunkBuildData
    {
        public int3 coord;
        public int segmentId;
        public int cellSize;
        public int3 detailFocusKey;
        public int3 origin;
        public float3 boundsCenter;
        public float3 boundsSize;
        public PlanetMeshData mesh;
    }

    public sealed class PlanetMeshData
    {
        public float3 boundsCenter;
        public float3 boundsSize;
        public List<float3> vertices = new List<float3>();
        public List<float3> normals = new List<float3>();
        public List<float2> uvs = new List<float2>();
        public List<int> interiorIndices = new List<int>();
        public List<int> transitionIndices = new List<int>();
        public List<int> surfaceIndices = new List<int>();
    }
}
