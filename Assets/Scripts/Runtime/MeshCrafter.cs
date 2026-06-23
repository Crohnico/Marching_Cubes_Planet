using System.Collections.Generic;
using System.Threading.Tasks;
using MarchingCubesPlanet.VoxelEngine.Data;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    public static class MeshCrafter
    {
        public static Task<Mesh> CraftFarMesh(PlanetData planetData)
        {
            PlanetMeshData farMeshData = CraftFarMeshData(planetData);
            planetData.farMesh = farMeshData;
            return Task.FromResult(ToUnityMesh(farMeshData, "VoxelCombinedMesh_Far"));
        }

        public static Mesh ToUnityMesh(PlanetMeshData meshData, string meshName)
        {
            Mesh mesh = new Mesh
            {
                name = meshName,
                indexFormat = meshData != null && meshData.vertices.Count > 65535
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16
            };

            if (meshData == null || meshData.vertices.Count == 0)
            {
                mesh.bounds = new Bounds(Vector3.zero, Vector3.zero);
                return mesh;
            }

            List<Vector3> vertices = new List<Vector3>(meshData.vertices.Count);
            List<Vector3> normals = new List<Vector3>(meshData.normals.Count);
            List<Vector2> uvs = new List<Vector2>(meshData.uvs.Count);

            for (int i = 0; i < meshData.vertices.Count; i++)
            {
                vertices.Add(ToVector3(meshData.vertices[i]));
            }

            for (int i = 0; i < meshData.normals.Count; i++)
            {
                normals.Add(ToVector3(meshData.normals[i]));
            }

            for (int i = 0; i < meshData.uvs.Count; i++)
            {
                float2 uv = meshData.uvs[i];
                uvs.Add(new Vector2(uv.x, uv.y));
            }

            mesh.SetVertices(vertices);
            if (normals.Count == vertices.Count)
            {
                mesh.SetNormals(normals);
            }

            if (uvs.Count == vertices.Count)
            {
                mesh.SetUVs(0, uvs);
            }

            List<int> indices = new List<int>(
                meshData.interiorIndices.Count
                + meshData.transitionIndices.Count
                + meshData.surfaceIndices.Count);
            indices.AddRange(meshData.interiorIndices);
            indices.AddRange(meshData.transitionIndices);
            indices.AddRange(meshData.surfaceIndices);

            mesh.subMeshCount = 1;
            mesh.SetTriangles(indices, 0, false);
            mesh.bounds = new Bounds(ToVector3(meshData.boundsCenter), ToVector3(meshData.boundsSize));
            return mesh;
        }

        public static Mesh ToChunkUnityMesh(PlanetMeshData meshData, string meshName)
        {
            Mesh mesh = new Mesh
            {
                name = meshName,
                indexFormat = meshData != null && meshData.vertices.Count > 65535
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16
            };

            if (meshData == null || meshData.vertices.Count == 0)
            {
                mesh.bounds = new Bounds(Vector3.zero, Vector3.zero);
                return mesh;
            }

            List<Vector3> vertices = new List<Vector3>(meshData.vertices.Count);
            List<Vector3> normals = new List<Vector3>(meshData.normals.Count);
            List<Vector2> uvs = new List<Vector2>(meshData.uvs.Count);

            for (int i = 0; i < meshData.vertices.Count; i++)
            {
                vertices.Add(ToVector3(meshData.vertices[i]));
            }

            for (int i = 0; i < meshData.normals.Count; i++)
            {
                normals.Add(ToVector3(meshData.normals[i]));
            }

            for (int i = 0; i < meshData.uvs.Count; i++)
            {
                float2 uv = meshData.uvs[i];
                uvs.Add(new Vector2(uv.x, uv.y));
            }

            mesh.SetVertices(vertices);
            if (normals.Count == vertices.Count)
            {
                mesh.SetNormals(normals);
            }

            if (uvs.Count == vertices.Count)
            {
                mesh.SetUVs(0, uvs);
            }

            mesh.subMeshCount = 3;
            mesh.SetTriangles(meshData.interiorIndices, 0, false);
            mesh.SetTriangles(meshData.transitionIndices, 1, false);
            mesh.SetTriangles(meshData.surfaceIndices, 2, false);
            mesh.bounds = new Bounds(ToVector3(meshData.boundsCenter), ToVector3(meshData.boundsSize));
            return mesh;
        }

        private static PlanetMeshData CraftFarMeshData(PlanetData planetData)
        {
            PlanetMeshData combined = new PlanetMeshData();
            if (planetData == null)
            {
                return combined;
            }

            for (int i = 0; i < planetData.chunks.Count; i++)
            {
                PlanetChunkBuildData chunk = planetData.chunks[i];
                if (chunk.mesh == null || chunk.mesh.vertices.Count == 0)
                {
                    continue;
                }

                AppendChunk(combined, chunk, planetData.worldPosition);
            }

            if (combined.vertices.Count > 0)
            {
                CalculateBounds(combined.vertices, out combined.boundsCenter, out combined.boundsSize);
            }

            return combined;
        }

        private static void AppendChunk(PlanetMeshData combined, PlanetChunkBuildData chunk, float3 renderOrigin)
        {
            int baseVertex = combined.vertices.Count;
            float3 chunkOrigin = new float3(chunk.origin.x, chunk.origin.y, chunk.origin.z) - renderOrigin;
            PlanetMeshData source = chunk.mesh;

            for (int i = 0; i < source.vertices.Count; i++)
            {
                combined.vertices.Add(source.vertices[i] + chunkOrigin);
            }

            combined.normals.AddRange(source.normals);
            combined.uvs.AddRange(source.uvs);
            AppendIndices(combined.interiorIndices, source.interiorIndices, baseVertex);
            AppendIndices(combined.transitionIndices, source.transitionIndices, baseVertex);
            AppendIndices(combined.surfaceIndices, source.surfaceIndices, baseVertex);
        }

        private static void AppendIndices(List<int> destination, List<int> source, int baseVertex)
        {
            for (int i = 0; i < source.Count; i++)
            {
                destination.Add(source[i] + baseVertex);
            }
        }

        private static void CalculateBounds(List<float3> vertices, out float3 center, out float3 size)
        {
            float3 min = vertices[0];
            float3 max = vertices[0];
            for (int i = 1; i < vertices.Count; i++)
            {
                min = math.min(min, vertices[i]);
                max = math.max(max, vertices[i]);
            }

            center = (min + max) * 0.5f;
            size = max - min;
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
    }
}
