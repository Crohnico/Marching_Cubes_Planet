using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    public static class FileManager
    {
        public static bool GetFile(string relativePath, out byte[] bytes)
        {
            bytes = null;
            if (!TryGetFullPath(relativePath, out string fullPath))
            {
                return false;
            }

            if (!File.Exists(fullPath))
            {
                return false;
            }

            try
            {
                bytes = File.ReadAllBytes(fullPath);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"No se pudo leer el archivo '{relativePath}': {exception.Message}");
                bytes = null;
                return false;
            }
        }

        public static bool SaveFile(string relativePath, byte[] bytes)
        {
            if (bytes == null || !TryGetFullPath(relativePath, out string fullPath))
            {
                return false;
            }

            try
            {
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string temporaryPath = fullPath + ".tmp";
                File.WriteAllBytes(temporaryPath, bytes);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }

                File.Move(temporaryPath, fullPath);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"No se pudo guardar el archivo '{relativePath}': {exception.Message}");
                return false;
            }
        }

        public static bool GetFile(string relativePath, out Mesh mesh)
        {
            mesh = null;
            if (!GetFile(relativePath, out byte[] bytes))
            {
                return false;
            }

            try
            {
                mesh = DeserializeMesh(bytes);
                return mesh != null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"No se pudo leer la mesh '{relativePath}': {exception.Message}");
                mesh = null;
                return false;
            }
        }

        public static bool SaveFile(string relativePath, Mesh mesh)
        {
            if (mesh == null)
            {
                return false;
            }

            try
            {
                return SaveFile(relativePath, SerializeMesh(mesh));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"No se pudo serializar la mesh '{relativePath}': {exception.Message}");
                return false;
            }
        }

        public static bool DeleteDirectory(string relativePath)
        {
            if (!TryGetFullPath(relativePath, out string fullPath))
            {
                return false;
            }

            if (!Directory.Exists(fullPath))
            {
                return true;
            }

            try
            {
                Directory.Delete(fullPath, true);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"No se pudo borrar el directorio '{relativePath}': {exception.Message}");
                return false;
            }
        }

        private static bool TryGetFullPath(string relativePath, out string fullPath)
        {
            fullPath = null;
            if (string.IsNullOrWhiteSpace(relativePath)
                || Path.IsPathRooted(relativePath)
                || relativePath.Contains(".."))
            {
                Debug.LogWarning($"Ruta de archivo invalida: '{relativePath}'.");
                return false;
            }

            string root = Path.GetFullPath(Application.persistentDataPath);
            string candidate = Path.GetFullPath(Path.Combine(root, relativePath));
            string rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar.ToString())
                ? root
                : root + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"Ruta fuera del directorio persistente: '{relativePath}'.");
                return false;
            }

            fullPath = candidate;
            return true;
        }

        private static byte[] SerializeMesh(Mesh mesh)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(0x4d43504d);
                writer.Write(1);
                writer.Write(mesh.name ?? string.Empty);
                writer.Write((int)mesh.indexFormat);
                WriteBounds(writer, mesh.bounds);

                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                Vector2[] uvs = mesh.uv;
                writer.Write(vertices.Length);
                for (int i = 0; i < vertices.Length; i++)
                {
                    WriteVector3(writer, vertices[i]);
                }

                writer.Write(normals.Length);
                for (int i = 0; i < normals.Length; i++)
                {
                    WriteVector3(writer, normals[i]);
                }

                writer.Write(uvs.Length);
                for (int i = 0; i < uvs.Length; i++)
                {
                    WriteVector2(writer, uvs[i]);
                }

                writer.Write(mesh.subMeshCount);
                for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                {
                    int[] indices = mesh.GetIndices(subMesh);
                    writer.Write(indices.Length);
                    for (int i = 0; i < indices.Length; i++)
                    {
                        writer.Write(indices[i]);
                    }
                }

                return stream.ToArray();
            }
        }

        private static Mesh DeserializeMesh(byte[] bytes)
        {
            using (MemoryStream stream = new MemoryStream(bytes))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                int magic = reader.ReadInt32();
                int version = reader.ReadInt32();
                if (magic != 0x4d43504d || version != 1)
                {
                    return null;
                }

                Mesh mesh = new Mesh
                {
                    name = reader.ReadString(),
                    indexFormat = (IndexFormat)reader.ReadInt32()
                };
                Bounds bounds = ReadBounds(reader);

                int vertexCount = reader.ReadInt32();
                Vector3[] vertices = new Vector3[vertexCount];
                for (int i = 0; i < vertexCount; i++)
                {
                    vertices[i] = ReadVector3(reader);
                }

                int normalCount = reader.ReadInt32();
                Vector3[] normals = new Vector3[normalCount];
                for (int i = 0; i < normalCount; i++)
                {
                    normals[i] = ReadVector3(reader);
                }

                int uvCount = reader.ReadInt32();
                Vector2[] uvs = new Vector2[uvCount];
                for (int i = 0; i < uvCount; i++)
                {
                    uvs[i] = ReadVector2(reader);
                }

                mesh.vertices = vertices;
                if (normalCount == vertexCount)
                {
                    mesh.normals = normals;
                }

                if (uvCount == vertexCount)
                {
                    mesh.uv = uvs;
                }

                int subMeshCount = reader.ReadInt32();
                mesh.subMeshCount = subMeshCount;
                for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
                {
                    int indexCount = reader.ReadInt32();
                    int[] indices = new int[indexCount];
                    for (int i = 0; i < indexCount; i++)
                    {
                        indices[i] = reader.ReadInt32();
                    }

                    mesh.SetTriangles(indices, subMesh, false);
                }

                mesh.bounds = bounds;
                return mesh;
            }
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
    }
}
