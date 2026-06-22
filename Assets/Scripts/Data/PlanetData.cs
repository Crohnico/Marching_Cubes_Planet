using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;

namespace MarchingCubesPlanet.VoxelEngine.Data
{
    // Minimal build manifest for a generated planet.
    // It stores only data needed to rebuild planet meshes without asking the generator
    // to rediscover chunk occupancy or segment assignment.
    public sealed class PlanetData
    {
        public HashSet<int3> declaredChunks = new HashSet<int3>();
        public List<PlanetChunkBuildData> chunks = new List<PlanetChunkBuildData>();

        public byte[] ToBinary()
        {
            using MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);

            writer.Write(declaredChunks.Count);
            foreach (int3 coord in declaredChunks)
            {
                WriteInt3(writer, coord);
            }

            writer.Write(chunks.Count);
            for (int i = 0; i < chunks.Count; i++)
            {
                WriteInt3(writer, chunks[i].coord);
                writer.Write(chunks[i].segmentId);
            }

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

            int declaredChunkCount = reader.ReadInt32();
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
                    segmentId = reader.ReadInt32()
                });
            }

            return data;
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
    }

    public sealed class PlanetChunkBuildData
    {
        public int3 coord;
        public int segmentId;
    }
}
