using System;
using System.Collections.Generic;
using UnityEngine;

namespace MarchingCubesPlanet.MarchingCubes
{
    public sealed class MarchingCubesMeshData
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();
        private readonly List<Vector2> uvs = new List<Vector2>();

        public IReadOnlyList<Vector3> Vertices => vertices;
        public IReadOnlyList<int> Triangles => triangles;
        public IReadOnlyList<Vector2> UVs => uvs;

        public void Clear()
        {
            vertices.Clear();
            triangles.Clear();
            uvs.Clear();
        }

        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c)
        {
            int firstIndex = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            triangles.Add(firstIndex);
            triangles.Add(firstIndex + 1);
            triangles.Add(firstIndex + 2);
        }

        public void ApplyUVs(Func<Vector3, Vector2> uvResolver)
        {
            uvs.Clear();
            if (uvResolver == null)
            {
                return;
            }

            for (int i = 0; i < vertices.Count; i++)
            {
                uvs.Add(uvResolver.Invoke(vertices[i]));
            }
        }

        public Mesh ToMesh(string meshName = "Marching Cubes Mesh")
        {
            Mesh mesh = new Mesh
            {
                name = meshName
            };

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            if (uvs.Count == vertices.Count)
            {
                mesh.SetUVs(0, uvs);
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
