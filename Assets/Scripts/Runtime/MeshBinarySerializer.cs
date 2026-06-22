using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    public static class MeshBinarySerializer
    {
        private const int FormatVersion = 1;
        private const int Magic = 0x4D43504D;

        public static byte[] ToBinary(Mesh mesh)
        {
            using MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);

            writer.Write(Magic);
            writer.Write(FormatVersion);
            writer.Write(mesh.indexFormat == IndexFormat.UInt32 ? 32 : 16);
            WriteBounds(writer, mesh.bounds);

            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            mesh.GetVertices(vertices);
            mesh.GetNormals(normals);
            mesh.GetUVs(0, uvs);

            WriteVector3List(writer, vertices);
            WriteVector3List(writer, normals);
            WriteVector2List(writer, uvs);

            writer.Write(mesh.subMeshCount);
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                int[] triangles = mesh.GetTriangles(subMesh);
                writer.Write(triangles.Length);
                for (int i = 0; i < triangles.Length; i++)
                {
                    writer.Write(triangles[i]);
                }
            }

            return stream.ToArray();
        }

        public static Mesh FromBinary(byte[] binary, string meshName)
        {
            using MemoryStream stream = new MemoryStream(binary);
            using BinaryReader reader = new BinaryReader(stream);

            if (reader.ReadInt32() != Magic)
            {
                throw new InvalidDataException("Invalid meshbin magic.");
            }

            int version = reader.ReadInt32();
            if (version != FormatVersion)
            {
                throw new InvalidDataException($"Unsupported meshbin version {version}.");
            }

            int indexFormatBits = reader.ReadInt32();
            Bounds bounds = ReadBounds(reader);
            List<Vector3> vertices = ReadVector3List(reader);
            List<Vector3> normals = ReadVector3List(reader);
            List<Vector2> uvs = ReadVector2List(reader);

            Mesh mesh = new Mesh
            {
                name = meshName,
                indexFormat = indexFormatBits == 32 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.MarkDynamic();
            mesh.SetVertices(vertices);
            if (normals.Count == vertices.Count)
            {
                mesh.SetNormals(normals);
            }

            if (uvs.Count == vertices.Count)
            {
                mesh.SetUVs(0, uvs);
            }

            int subMeshCount = reader.ReadInt32();
            mesh.subMeshCount = subMeshCount;
            for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
            {
                int indexCount = reader.ReadInt32();
                int[] triangles = new int[indexCount];
                for (int i = 0; i < indexCount; i++)
                {
                    triangles[i] = reader.ReadInt32();
                }

                mesh.SetTriangles(triangles, subMesh, false);
            }

            mesh.bounds = bounds;
            return mesh;
        }

        private static void WriteBounds(BinaryWriter writer, Bounds bounds)
        {
            WriteVector3(writer, bounds.center);
            WriteVector3(writer, bounds.size);
        }

        private static Bounds ReadBounds(BinaryReader reader)
        {
            return new Bounds(ReadVector3(reader), ReadVector3(reader));
        }

        private static void WriteVector3List(BinaryWriter writer, List<Vector3> values)
        {
            writer.Write(values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                WriteVector3(writer, values[i]);
            }
        }

        private static List<Vector3> ReadVector3List(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            List<Vector3> values = new List<Vector3>(count);
            for (int i = 0; i < count; i++)
            {
                values.Add(ReadVector3(reader));
            }

            return values;
        }

        private static void WriteVector2List(BinaryWriter writer, List<Vector2> values)
        {
            writer.Write(values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                writer.Write(values[i].x);
                writer.Write(values[i].y);
            }
        }

        private static List<Vector2> ReadVector2List(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            List<Vector2> values = new List<Vector2>(count);
            for (int i = 0; i < count; i++)
            {
                values.Add(new Vector2(reader.ReadSingle(), reader.ReadSingle()));
            }

            return values;
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
    }
}
