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
        public int TriangleCount => triangles.Count / 3;

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

        public void Append(MarchingCubesMeshData source, Vector3 vertexOffset)
        {
            if (source == null || source.vertices.Count == 0 || source.triangles.Count == 0)
            {
                return;
            }

            int firstIndex = vertices.Count;
            bool sourceHasUvs = source.uvs.Count == source.vertices.Count;
            bool keepUvs = uvs.Count == vertices.Count && sourceHasUvs;

            if (!keepUvs)
            {
                uvs.Clear();
            }

            for (int i = 0; i < source.vertices.Count; i++)
            {
                vertices.Add(source.vertices[i] + vertexOffset);
                if (keepUvs)
                {
                    uvs.Add(source.uvs[i]);
                }
            }

            for (int i = 0; i < source.triangles.Count; i++)
            {
                triangles.Add(source.triangles[i] + firstIndex);
            }
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

        public int SimplifyConservative(
            float targetTriangleRatio,
            float maxEdgeLength,
            float maxSurfaceError,
            float maxUvDelta,
            float protectedUvV,
            float minNormalDot,
            float weldTolerance,
            Predicate<Vector3> protectedVertexResolver = null)
        {
            int originalTriangleCount = TriangleCount;
            if (originalTriangleCount <= 1 || targetTriangleRatio >= 0.999f)
            {
                return 0;
            }

            int targetTriangleCount = Mathf.Max(1, Mathf.FloorToInt(originalTriangleCount * Mathf.Clamp01(targetTriangleRatio)));
            if (targetTriangleCount >= originalTriangleCount)
            {
                return 0;
            }

            WeldVertices(Mathf.Max(0.000001f, weldTolerance));

            int maxCollapses = Mathf.Max(16, originalTriangleCount * 2);
            for (int i = 0; i < maxCollapses && TriangleCount > targetTriangleCount; i++)
            {
                if (!TryFindBestCollapse(
                    Mathf.Max(0f, maxEdgeLength),
                    Mathf.Max(0f, maxSurfaceError),
                    Mathf.Max(0f, maxUvDelta),
                    protectedUvV,
                    Mathf.Clamp(minNormalDot, -1f, 1f),
                    protectedVertexResolver,
                    out EdgeCollapse collapse))
                {
                    break;
                }

                CollapseEdge(collapse.KeepVertex, collapse.RemoveVertex);
            }

            RemoveUnusedVertices();
            UnshareTriangleVertices();
            return originalTriangleCount - TriangleCount;
        }

        public Mesh ToMesh(string meshName = "Marching Cubes Mesh")
        {
            return ToMesh(meshName, true);
        }

        public Mesh ToMesh(string meshName, bool useFlatShading, float smoothWeldTolerance = 0.0001f)
        {
            List<Vector3> meshVertices = vertices;
            List<int> meshTriangles = triangles;
            List<Vector2> meshUvs = uvs;

            if (!useFlatShading)
            {
                BuildSharedVertexMeshData(
                    Mathf.Max(0.000001f, smoothWeldTolerance),
                    out meshVertices,
                    out meshTriangles,
                    out meshUvs);
            }

            Mesh mesh = new Mesh
            {
                name = meshName
            };

            if (meshVertices.Count > 65535)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.SetVertices(meshVertices);
            mesh.SetTriangles(meshTriangles, 0);
            if (meshUvs.Count == meshVertices.Count)
            {
                mesh.SetUVs(0, meshUvs);
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void BuildSharedVertexMeshData(
            float tolerance,
            out List<Vector3> sharedVertices,
            out List<int> sharedTriangles,
            out List<Vector2> sharedUvs)
        {
            bool hasUvs = uvs.Count == vertices.Count;
            float inverseTolerance = 1f / tolerance;
            Dictionary<VertexKey, int> sharedIndices = new Dictionary<VertexKey, int>(vertices.Count);
            sharedVertices = new List<Vector3>(vertices.Count);
            sharedTriangles = new List<int>(triangles.Count);
            sharedUvs = hasUvs ? new List<Vector2>(uvs.Count) : new List<Vector2>();

            for (int i = 0; i < triangles.Count; i++)
            {
                int sourceIndex = triangles[i];
                VertexKey key = new VertexKey(vertices[sourceIndex], inverseTolerance);
                if (!sharedIndices.TryGetValue(key, out int sharedIndex))
                {
                    sharedIndex = sharedVertices.Count;
                    sharedIndices.Add(key, sharedIndex);
                    sharedVertices.Add(vertices[sourceIndex]);
                    if (hasUvs)
                    {
                        sharedUvs.Add(uvs[sourceIndex]);
                    }
                }

                sharedTriangles.Add(sharedIndex);
            }
        }

        private void WeldVertices(float tolerance)
        {
            if (vertices.Count == 0)
            {
                return;
            }

            bool hasUvs = uvs.Count == vertices.Count;
            float inverseTolerance = 1f / tolerance;
            Dictionary<VertexKey, int> weldedIndices = new Dictionary<VertexKey, int>(vertices.Count);
            List<Vector3> weldedVertices = new List<Vector3>(vertices.Count);
            List<Vector2> weldedUvs = hasUvs ? new List<Vector2>(uvs.Count) : null;
            int[] remap = new int[vertices.Count];

            for (int i = 0; i < vertices.Count; i++)
            {
                VertexKey key = new VertexKey(vertices[i], inverseTolerance);
                if (!weldedIndices.TryGetValue(key, out int weldedIndex))
                {
                    weldedIndex = weldedVertices.Count;
                    weldedIndices.Add(key, weldedIndex);
                    weldedVertices.Add(vertices[i]);
                    if (hasUvs)
                    {
                        weldedUvs.Add(uvs[i]);
                    }
                }

                remap[i] = weldedIndex;
            }

            for (int i = 0; i < triangles.Count; i++)
            {
                triangles[i] = remap[triangles[i]];
            }

            vertices.Clear();
            vertices.AddRange(weldedVertices);
            uvs.Clear();
            if (hasUvs)
            {
                uvs.AddRange(weldedUvs);
            }

            RemoveDegenerateTriangles();
            RemoveDuplicateTriangles();
            RemoveUnusedVertices();
        }

        private bool TryFindBestCollapse(
            float maxEdgeLength,
            float maxSurfaceError,
            float maxUvDelta,
            float protectedUvV,
            float minNormalDot,
            Predicate<Vector3> protectedVertexResolver,
            out EdgeCollapse bestCollapse)
        {
            bestCollapse = default;
            bool foundCollapse = false;
            float bestScore = float.PositiveInfinity;
            HashSet<EdgeKey> edges = CollectEdges();

            foreach (EdgeKey edge in edges)
            {
                if (!IsEdgeUvCompatible(edge.A, edge.B, maxUvDelta, protectedUvV))
                {
                    continue;
                }

                float edgeLength = Vector3.Distance(vertices[edge.A], vertices[edge.B]);
                if (edgeLength > maxEdgeLength)
                {
                    continue;
                }

                if (TryEvaluateCollapse(edge.A, edge.B, edgeLength, maxSurfaceError, minNormalDot, protectedVertexResolver, out float scoreAB)
                    && scoreAB < bestScore)
                {
                    bestCollapse = new EdgeCollapse(edge.A, edge.B, scoreAB);
                    bestScore = scoreAB;
                    foundCollapse = true;
                }

                if (TryEvaluateCollapse(edge.B, edge.A, edgeLength, maxSurfaceError, minNormalDot, protectedVertexResolver, out float scoreBA)
                    && scoreBA < bestScore)
                {
                    bestCollapse = new EdgeCollapse(edge.B, edge.A, scoreBA);
                    bestScore = scoreBA;
                    foundCollapse = true;
                }
            }

            return foundCollapse;
        }

        private HashSet<EdgeKey> CollectEdges()
        {
            HashSet<EdgeKey> edges = new HashSet<EdgeKey>();
            for (int i = 0; i < triangles.Count; i += 3)
            {
                edges.Add(new EdgeKey(triangles[i], triangles[i + 1]));
                edges.Add(new EdgeKey(triangles[i + 1], triangles[i + 2]));
                edges.Add(new EdgeKey(triangles[i + 2], triangles[i]));
            }

            return edges;
        }

        private bool IsEdgeUvCompatible(int a, int b, float maxUvDelta, float protectedUvV)
        {
            if (uvs.Count != vertices.Count)
            {
                return true;
            }

            Vector2 uvA = uvs[a];
            Vector2 uvB = uvs[b];
            if (Mathf.Abs(uvA.y - uvB.y) > maxUvDelta)
            {
                return false;
            }

            if (!float.IsNaN(protectedUvV)
                && ((uvA.y <= protectedUvV && uvB.y > protectedUvV) || (uvB.y <= protectedUvV && uvA.y > protectedUvV)))
            {
                return false;
            }

            return true;
        }

        private bool TryEvaluateCollapse(
            int keepVertex,
            int removeVertex,
            float edgeLength,
            float maxSurfaceError,
            float minNormalDot,
            Predicate<Vector3> protectedVertexResolver,
            out float score)
        {
            score = float.PositiveInfinity;
            if (protectedVertexResolver != null
                && (protectedVertexResolver.Invoke(vertices[keepVertex]) || protectedVertexResolver.Invoke(vertices[removeVertex])))
            {
                return false;
            }

            bool removesTriangle = false;
            int remainingAffectedTriangles = 0;
            float worstNormalDot = 1f;
            float worstSurfaceError = 0f;

            for (int i = 0; i < triangles.Count; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];
                bool containsKeep = a == keepVertex || b == keepVertex || c == keepVertex;
                bool containsRemove = a == removeVertex || b == removeVertex || c == removeVertex;

                if (!containsRemove)
                {
                    continue;
                }

                if (containsKeep)
                {
                    removesTriangle = true;
                    continue;
                }

                int newA = a == removeVertex ? keepVertex : a;
                int newB = b == removeVertex ? keepVertex : b;
                int newC = c == removeVertex ? keepVertex : c;
                if (newA == newB || newB == newC || newC == newA)
                {
                    return false;
                }

                Vector3 oldA = vertices[a];
                Vector3 oldB = vertices[b];
                Vector3 oldC = vertices[c];
                Vector3 newPositionA = vertices[newA];
                Vector3 newPositionB = vertices[newB];
                Vector3 newPositionC = vertices[newC];
                Vector3 oldNormal = Vector3.Cross(oldB - oldA, oldC - oldA);
                Vector3 newNormal = Vector3.Cross(newPositionB - newPositionA, newPositionC - newPositionA);
                if (oldNormal.sqrMagnitude <= 0.0000000001f || newNormal.sqrMagnitude <= 0.0000000001f)
                {
                    return false;
                }

                float normalDot = Vector3.Dot(oldNormal.normalized, newNormal.normalized);
                if (normalDot < minNormalDot)
                {
                    return false;
                }

                float surfaceError = MaxDistanceToPlane(newPositionA, newPositionB, newPositionC, oldA, oldB, oldC);
                if (surfaceError > maxSurfaceError)
                {
                    return false;
                }

                worstNormalDot = Mathf.Min(worstNormalDot, normalDot);
                worstSurfaceError = Mathf.Max(worstSurfaceError, surfaceError);
                remainingAffectedTriangles++;
            }

            if (!removesTriangle || remainingAffectedTriangles == 0)
            {
                return false;
            }

            score = worstSurfaceError + edgeLength * 0.001f + (1f - worstNormalDot) * Mathf.Max(0.0001f, maxSurfaceError);
            return true;
        }

        private void CollapseEdge(int keepVertex, int removeVertex)
        {
            for (int i = 0; i < triangles.Count; i++)
            {
                if (triangles[i] == removeVertex)
                {
                    triangles[i] = keepVertex;
                }
            }

            RemoveDegenerateTriangles();
            RemoveDuplicateTriangles();
            RemoveUnusedVertices();
        }

        private void RemoveDegenerateTriangles()
        {
            int writeIndex = 0;
            for (int i = 0; i < triangles.Count; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];
                if (a == b || b == c || c == a)
                {
                    continue;
                }

                Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                if (normal.sqrMagnitude <= 0.0000000001f)
                {
                    continue;
                }

                triangles[writeIndex++] = a;
                triangles[writeIndex++] = b;
                triangles[writeIndex++] = c;
            }

            if (writeIndex < triangles.Count)
            {
                triangles.RemoveRange(writeIndex, triangles.Count - writeIndex);
            }
        }

        private void RemoveDuplicateTriangles()
        {
            HashSet<TriangleKey> usedTriangles = new HashSet<TriangleKey>();
            int writeIndex = 0;
            for (int i = 0; i < triangles.Count; i += 3)
            {
                TriangleKey key = new TriangleKey(triangles[i], triangles[i + 1], triangles[i + 2]);
                if (!usedTriangles.Add(key))
                {
                    continue;
                }

                triangles[writeIndex++] = triangles[i];
                triangles[writeIndex++] = triangles[i + 1];
                triangles[writeIndex++] = triangles[i + 2];
            }

            if (writeIndex < triangles.Count)
            {
                triangles.RemoveRange(writeIndex, triangles.Count - writeIndex);
            }
        }

        private void RemoveUnusedVertices()
        {
            if (vertices.Count == 0)
            {
                return;
            }

            bool hasUvs = uvs.Count == vertices.Count;
            int[] remap = new int[vertices.Count];
            for (int i = 0; i < remap.Length; i++)
            {
                remap[i] = -1;
            }

            List<Vector3> compactVertices = new List<Vector3>(vertices.Count);
            List<Vector2> compactUvs = hasUvs ? new List<Vector2>(uvs.Count) : null;
            for (int i = 0; i < triangles.Count; i++)
            {
                int vertexIndex = triangles[i];
                int compactIndex = remap[vertexIndex];
                if (compactIndex < 0)
                {
                    compactIndex = compactVertices.Count;
                    remap[vertexIndex] = compactIndex;
                    compactVertices.Add(vertices[vertexIndex]);
                    if (hasUvs)
                    {
                        compactUvs.Add(uvs[vertexIndex]);
                    }
                }

                triangles[i] = compactIndex;
            }

            vertices.Clear();
            vertices.AddRange(compactVertices);
            uvs.Clear();
            if (hasUvs)
            {
                uvs.AddRange(compactUvs);
            }
        }

        private void UnshareTriangleVertices()
        {
            if (triangles.Count == 0)
            {
                vertices.Clear();
                uvs.Clear();
                return;
            }

            bool hasUvs = uvs.Count == vertices.Count;
            List<Vector3> unsharedVertices = new List<Vector3>(triangles.Count);
            List<Vector2> unsharedUvs = hasUvs ? new List<Vector2>(triangles.Count) : null;
            List<int> unsharedTriangles = new List<int>(triangles.Count);

            for (int i = 0; i < triangles.Count; i++)
            {
                int sourceIndex = triangles[i];
                int newIndex = unsharedVertices.Count;
                unsharedVertices.Add(vertices[sourceIndex]);
                if (hasUvs)
                {
                    unsharedUvs.Add(uvs[sourceIndex]);
                }

                unsharedTriangles.Add(newIndex);
            }

            vertices.Clear();
            vertices.AddRange(unsharedVertices);
            triangles.Clear();
            triangles.AddRange(unsharedTriangles);
            uvs.Clear();
            if (hasUvs)
            {
                uvs.AddRange(unsharedUvs);
            }
        }

        private static float MaxDistanceToPlane(
            Vector3 planeA,
            Vector3 planeB,
            Vector3 planeC,
            Vector3 pointA,
            Vector3 pointB,
            Vector3 pointC)
        {
            Vector3 normal = Vector3.Cross(planeB - planeA, planeC - planeA);
            float magnitude = normal.magnitude;
            if (magnitude <= 0.000001f)
            {
                return float.PositiveInfinity;
            }

            normal /= magnitude;
            return Mathf.Max(
                Mathf.Abs(Vector3.Dot(pointA - planeA, normal)),
                Mathf.Abs(Vector3.Dot(pointB - planeA, normal)),
                Mathf.Abs(Vector3.Dot(pointC - planeA, normal)));
        }

        private readonly struct EdgeCollapse
        {
            public EdgeCollapse(int keepVertex, int removeVertex, float score)
            {
                KeepVertex = keepVertex;
                RemoveVertex = removeVertex;
                Score = score;
            }

            public int KeepVertex { get; }
            public int RemoveVertex { get; }
            public float Score { get; }
        }

        private readonly struct EdgeKey : IEquatable<EdgeKey>
        {
            public EdgeKey(int a, int b)
            {
                if (a < b)
                {
                    A = a;
                    B = b;
                }
                else
                {
                    A = b;
                    B = a;
                }
            }

            public int A { get; }
            public int B { get; }

            public bool Equals(EdgeKey other)
            {
                return A == other.A && B == other.B;
            }

            public override bool Equals(object obj)
            {
                return obj is EdgeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (A * 397) ^ B;
                }
            }
        }

        private readonly struct TriangleKey : IEquatable<TriangleKey>
        {
            public TriangleKey(int a, int b, int c)
            {
                if (a > b)
                {
                    (a, b) = (b, a);
                }

                if (b > c)
                {
                    (b, c) = (c, b);
                }

                if (a > b)
                {
                    (a, b) = (b, a);
                }

                A = a;
                B = b;
                C = c;
            }

            private int A { get; }
            private int B { get; }
            private int C { get; }

            public bool Equals(TriangleKey other)
            {
                return A == other.A && B == other.B && C == other.C;
            }

            public override bool Equals(object obj)
            {
                return obj is TriangleKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = A;
                    hash = (hash * 397) ^ B;
                    hash = (hash * 397) ^ C;
                    return hash;
                }
            }
        }

        private readonly struct VertexKey : IEquatable<VertexKey>
        {
            public VertexKey(Vector3 vertex, float inverseTolerance)
            {
                X = Mathf.RoundToInt(vertex.x * inverseTolerance);
                Y = Mathf.RoundToInt(vertex.y * inverseTolerance);
                Z = Mathf.RoundToInt(vertex.z * inverseTolerance);
            }

            private int X { get; }
            private int Y { get; }
            private int Z { get; }

            public bool Equals(VertexKey other)
            {
                return X == other.X && Y == other.Y && Z == other.Z;
            }

            public override bool Equals(object obj)
            {
                return obj is VertexKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = X;
                    hash = (hash * 397) ^ Y;
                    hash = (hash * 397) ^ Z;
                    return hash;
                }
            }
        }
    }
}
