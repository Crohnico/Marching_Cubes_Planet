using System.Collections.Generic;
using System.Threading.Tasks;
using MarchingCubesPlanet.VoxelEngine.Data;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    internal static class MeshCrafter
    {
        private const int InteriorSubMesh = 0;
        private const int TransitionSubMesh = 1;
        private const int SurfaceSubMesh = 2;
        private const int LayerSubMeshCount = 3;

        public static Task<Mesh> CraftFarMesh(PlanetData planetData)
        {
            return Task.FromResult(CraftFarMeshNow(planetData));
        }

        public static Mesh CraftFarMeshNow(PlanetData planetData)
        {
            PlanetMeshData farMeshData = CraftFarMeshData(planetData);
            if (planetData != null)
            {
                planetData.farMesh = farMeshData;
            }

            return ToUnityMesh(farMeshData, "VoxelCombinedMesh_Far");
        }

        public static Mesh CraftSegmentMesh(
            PlanetData planetData,
            int segmentId,
            float3 renderOrigin,
            string meshName)
        {
            PlanetMeshData segmentMeshData = CraftSegmentMeshData(planetData, segmentId, renderOrigin);
            return ToUnityMesh(segmentMeshData, meshName);
        }

        public static Mesh BuildChunkMesh(
            string meshName,
            NativeList<float3> vertices,
            NativeList<float3> normals,
            NativeList<float2> uvs,
            NativeList<int> interiorIndices,
            NativeList<int> transitionIndices,
            NativeList<int> surfaceIndices,
            out VoxelChunkAltIndices altIndices)
        {
            altIndices = new VoxelChunkAltIndices(
                interiorIndices.Length,
                transitionIndices.Length,
                surfaceIndices.Length);
            Mesh mesh = new Mesh
            {
                name = meshName,
                indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };

            if (vertices.Length == 0 || altIndices.TotalIndexCount == 0)
            {
                mesh.bounds = new Bounds(Vector3.zero, Vector3.zero);
                return mesh;
            }

            List<Vector3> meshVertices = new List<Vector3>(vertices.Length);
            List<Vector3> meshNormals = new List<Vector3>(normals.Length);
            List<Vector2> meshUvs = new List<Vector2>(uvs.Length);
            List<int> interior = new List<int>(interiorIndices.Length);
            List<int> transition = new List<int>(transitionIndices.Length);
            List<int> surface = new List<int>(surfaceIndices.Length);

            NativeListCopyUtility.CopyToVector3List(vertices, meshVertices);
            NativeListCopyUtility.CopyToVector3List(normals, meshNormals);
            NativeListCopyUtility.CopyToVector2List(uvs, meshUvs);
            NativeListCopyUtility.CopyToIntList(interiorIndices, interior);
            NativeListCopyUtility.CopyToIntList(transitionIndices, transition);
            NativeListCopyUtility.CopyToIntList(surfaceIndices, surface);

            mesh.SetVertices(meshVertices);
            mesh.SetNormals(meshNormals);
            mesh.SetUVs(0, meshUvs);
            mesh.subMeshCount = LayerSubMeshCount;
            mesh.SetTriangles(interior, InteriorSubMesh, false);
            mesh.SetTriangles(transition, TransitionSubMesh, false);
            mesh.SetTriangles(surface, SurfaceSubMesh, false);
            mesh.bounds = CalculateBounds(meshVertices);
            return mesh;
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
                vertices.Add(VoxelRuntimeMath.ToVector3(meshData.vertices[i]));
            }

            for (int i = 0; i < meshData.normals.Count; i++)
            {
                normals.Add(VoxelRuntimeMath.ToVector3(meshData.normals[i]));
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
            mesh.bounds = new Bounds(VoxelRuntimeMath.ToVector3(meshData.boundsCenter), VoxelRuntimeMath.ToVector3(meshData.boundsSize));
            return mesh;
        }

        public static PlanetMeshData ToPlanetMeshData(
            NativeList<float3> vertices,
            NativeList<float3> normals,
            NativeList<float2> uvs,
            NativeList<int> interiorIndices,
            NativeList<int> transitionIndices,
            NativeList<int> surfaceIndices)
        {
            PlanetMeshData meshData = new PlanetMeshData
            {
                boundsCenter = float3.zero,
                boundsSize = float3.zero
            };

            Copy(vertices, meshData.vertices);
            Copy(normals, meshData.normals);
            Copy(uvs, meshData.uvs);
            Copy(interiorIndices, meshData.interiorIndices);
            Copy(transitionIndices, meshData.transitionIndices);
            Copy(surfaceIndices, meshData.surfaceIndices);

            if (meshData.vertices.Count > 0)
            {
                CalculateBounds(meshData.vertices, out meshData.boundsCenter, out meshData.boundsSize);
            }

            return meshData;
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

        private static PlanetMeshData CraftSegmentMeshData(PlanetData planetData, int segmentId, float3 renderOrigin)
        {
            PlanetMeshData combined = new PlanetMeshData();
            if (planetData == null)
            {
                return combined;
            }

            for (int i = 0; i < planetData.chunks.Count; i++)
            {
                PlanetChunkBuildData chunk = planetData.chunks[i];
                if (chunk.segmentId != segmentId || chunk.mesh == null || chunk.mesh.vertices.Count == 0)
                {
                    continue;
                }

                AppendChunk(combined, chunk, renderOrigin);
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

        private static Bounds CalculateBounds(List<Vector3> vertices)
        {
            Vector3 min = vertices[0];
            Vector3 max = vertices[0];
            for (int i = 1; i < vertices.Count; i++)
            {
                min = Vector3.Min(min, vertices[i]);
                max = Vector3.Max(max, vertices[i]);
            }

            return new Bounds((min + max) * 0.5f, max - min);
        }

        private static void Copy(NativeList<float3> source, List<float3> destination)
        {
            NativeListCopyUtility.EnsureListCapacity(destination, source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                destination.Add(source[i]);
            }
        }

        private static void Copy(NativeList<float2> source, List<float2> destination)
        {
            NativeListCopyUtility.EnsureListCapacity(destination, source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                destination.Add(source[i]);
            }
        }

        private static void Copy(NativeList<int> source, List<int> destination)
        {
            NativeListCopyUtility.EnsureListCapacity(destination, source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                destination.Add(source[i]);
            }
        }
    }
}
