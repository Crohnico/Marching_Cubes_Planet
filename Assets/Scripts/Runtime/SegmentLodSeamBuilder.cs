using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    internal static class SegmentLodSeamBuilder
    {
        public const int CacheVersion = 2;

        private const float MinimumPlaneTolerance = 0.05f;
        private const float MinimumEdgeLength = 0.001f;
        private const float MinimumParallelDot = 0.45f;

        public static Mesh Build(
            SegmentSeamKey key,
            Mesh meshA,
            Mesh meshB,
            Matrix4x4 meshAToSeamRoot,
            Matrix4x4 meshBToSeamRoot,
            int cellSizeA,
            int cellSizeB,
            string meshName)
        {
            Mesh seamMesh = new Mesh
            {
                name = meshName,
                indexFormat = IndexFormat.UInt32
            };
            seamMesh.MarkDynamic();

            if (meshA == null || meshB == null || meshA.vertexCount == 0 || meshB.vertexCount == 0)
            {
                seamMesh.bounds = new Bounds(Vector3.zero, Vector3.zero);
                return seamMesh;
            }

            Bounds boundsA = TransformBounds(meshA.bounds, meshAToSeamRoot);
            Bounds boundsB = TransformBounds(meshB.bounds, meshBToSeamRoot);
            float plane = (GetBoundsMax(boundsA, key.axis) + GetBoundsMin(boundsB, key.axis)) * 0.5f;
            int largestCellSize = Mathf.Max(1, Mathf.Max(cellSizeA, cellSizeB));
            float tolerance = Mathf.Max(MinimumPlaneTolerance, largestCellSize * 0.65f);
            float maxMatchDistance = largestCellSize * 2.75f;
            float maxEdgeLength = largestCellSize * 3.25f;

            List<SeamEdge> edgesA = ExtractBoundaryEdges(meshA, meshAToSeamRoot, key.axis, plane, tolerance, maxEdgeLength);
            List<SeamEdge> edgesB = ExtractBoundaryEdges(meshB, meshBToSeamRoot, key.axis, plane, tolerance, maxEdgeLength);
            if (edgesA.Count == 0 || edgesB.Count == 0)
            {
                seamMesh.bounds = new Bounds(Vector3.zero, Vector3.zero);
                return seamMesh;
            }

            List<SeamEdge> source = edgesA.Count >= edgesB.Count ? edgesA : edgesB;
            List<SeamEdge> target = edgesA.Count >= edgesB.Count ? edgesB : edgesA;

            List<Vector3> vertices = new List<Vector3>(source.Count * 4);
            List<Vector3> normals = new List<Vector3>(source.Count * 4);
            List<Vector2> uvs = new List<Vector2>(source.Count * 4);
            List<int> triangles = new List<int>(source.Count * 6);

            for (int i = 0; i < source.Count; i++)
            {
                SeamEdge sourceEdge = source[i];
                int matchIndex = FindBestMatch(sourceEdge, target, maxMatchDistance);
                if (matchIndex < 0)
                {
                    continue;
                }

                SeamEdge targetEdge = target[matchIndex];
                AddBridgeQuad(vertices, normals, uvs, triangles, sourceEdge, targetEdge);
            }

            if (triangles.Count == 0)
            {
                seamMesh.bounds = new Bounds(Vector3.zero, Vector3.zero);
                return seamMesh;
            }

            seamMesh.SetVertices(vertices);
            seamMesh.SetNormals(normals);
            seamMesh.SetUVs(0, uvs);
            seamMesh.subMeshCount = 1;
            seamMesh.SetTriangles(triangles, 0, false);
            seamMesh.RecalculateBounds();
            return seamMesh;
        }

        private static List<SeamEdge> ExtractBoundaryEdges(
            Mesh mesh,
            Matrix4x4 meshToSeamRoot,
            int axis,
            float plane,
            float tolerance,
            float maxEdgeLength)
        {
            List<Vector3> sourceVertices = new List<Vector3>(mesh.vertexCount);
            List<Vector3> sourceNormals = new List<Vector3>(mesh.vertexCount);
            List<Vector2> sourceUvs = new List<Vector2>(mesh.vertexCount);
            mesh.GetVertices(sourceVertices);
            mesh.GetNormals(sourceNormals);
            mesh.GetUVs(0, sourceUvs);

            Dictionary<EdgeKey, EdgeBuildData> edgeCounts = new Dictionary<EdgeKey, EdgeBuildData>();
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                int[] triangles = mesh.GetTriangles(subMesh);
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    AddTriangleEdge(edgeCounts, triangles[i], triangles[i + 1]);
                    AddTriangleEdge(edgeCounts, triangles[i + 1], triangles[i + 2]);
                    AddTriangleEdge(edgeCounts, triangles[i + 2], triangles[i]);
                }
            }

            List<SeamEdge> boundaryEdges = new List<SeamEdge>();
            List<SeamEdge> fallbackEdges = new List<SeamEdge>();
            foreach (KeyValuePair<EdgeKey, EdgeBuildData> pair in edgeCounts)
            {
                EdgeKey edgeKey = pair.Key;
                if (!TryBuildSeamEdge(
                    edgeKey.a,
                    edgeKey.b,
                    sourceVertices,
                    sourceNormals,
                    sourceUvs,
                    meshToSeamRoot,
                    axis,
                    plane,
                    tolerance,
                    maxEdgeLength,
                    out SeamEdge edge))
                {
                    continue;
                }

                if (pair.Value.count == 1)
                {
                    boundaryEdges.Add(edge);
                }
                else
                {
                    fallbackEdges.Add(edge);
                }
            }

            return boundaryEdges.Count > 0 ? boundaryEdges : fallbackEdges;
        }

        private static bool TryBuildSeamEdge(
            int vertexA,
            int vertexB,
            List<Vector3> sourceVertices,
            List<Vector3> sourceNormals,
            List<Vector2> sourceUvs,
            Matrix4x4 meshToSeamRoot,
            int axis,
            float plane,
            float tolerance,
            float maxEdgeLength,
            out SeamEdge edge)
        {
            Vector3 a = meshToSeamRoot.MultiplyPoint3x4(sourceVertices[vertexA]);
            Vector3 b = meshToSeamRoot.MultiplyPoint3x4(sourceVertices[vertexB]);
            edge = default;

            if (Mathf.Abs(GetAxis(a, axis) - plane) > tolerance
                || Mathf.Abs(GetAxis(b, axis) - plane) > tolerance)
            {
                return false;
            }

            float length = Vector3.Distance(a, b);
            if (length < MinimumEdgeLength || length > maxEdgeLength)
            {
                return false;
            }

            Vector2 projectedA = Project(a, axis);
            Vector2 projectedB = Project(b, axis);
            float projectedLength = Vector2.Distance(projectedA, projectedB);
            if (projectedLength < MinimumEdgeLength)
            {
                return false;
            }

            Vector3 normalA = vertexA < sourceNormals.Count
                ? meshToSeamRoot.MultiplyVector(sourceNormals[vertexA]).normalized
                : Vector3.up;
            Vector3 normalB = vertexB < sourceNormals.Count
                ? meshToSeamRoot.MultiplyVector(sourceNormals[vertexB]).normalized
                : normalA;
            Vector2 uvA = vertexA < sourceUvs.Count ? sourceUvs[vertexA] : Vector2.zero;
            Vector2 uvB = vertexB < sourceUvs.Count ? sourceUvs[vertexB] : Vector2.zero;

            edge = new SeamEdge(
                a,
                b,
                normalA,
                normalB,
                uvA,
                uvB,
                projectedA,
                projectedB,
                (projectedA + projectedB) * 0.5f,
                (projectedB - projectedA).normalized);
            return true;
        }

        private static int FindBestMatch(SeamEdge source, List<SeamEdge> target, float maxMatchDistance)
        {
            int bestIndex = -1;
            float bestScore = maxMatchDistance * maxMatchDistance;
            for (int i = 0; i < target.Count; i++)
            {
                SeamEdge candidate = target[i];
                float parallelDot = Mathf.Abs(Vector2.Dot(source.projectedDirection, candidate.projectedDirection));
                if (parallelDot < MinimumParallelDot)
                {
                    continue;
                }

                float distance = (source.projectedMidpoint - candidate.projectedMidpoint).sqrMagnitude;
                if (distance >= bestScore)
                {
                    continue;
                }

                bestScore = distance;
                bestIndex = i;
            }

            return bestIndex;
        }

        private static void AddBridgeQuad(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            SeamEdge source,
            SeamEdge target)
        {
            bool sameDirection =
                Vector2.Distance(source.projectedA, target.projectedA) + Vector2.Distance(source.projectedB, target.projectedB)
                <= Vector2.Distance(source.projectedA, target.projectedB) + Vector2.Distance(source.projectedB, target.projectedA);

            Vector3 targetA = sameDirection ? target.a : target.b;
            Vector3 targetB = sameDirection ? target.b : target.a;
            Vector3 targetNormalA = sameDirection ? target.normalA : target.normalB;
            Vector3 targetNormalB = sameDirection ? target.normalB : target.normalA;
            Vector2 targetUvA = sameDirection ? target.uvA : target.uvB;
            Vector2 targetUvB = sameDirection ? target.uvB : target.uvA;

            int baseIndex = vertices.Count;
            vertices.Add(source.a);
            vertices.Add(source.b);
            vertices.Add(targetA);
            vertices.Add(targetB);
            normals.Add(source.normalA);
            normals.Add(source.normalB);
            normals.Add(targetNormalA);
            normals.Add(targetNormalB);
            uvs.Add(source.uvA);
            uvs.Add(source.uvB);
            uvs.Add(targetUvA);
            uvs.Add(targetUvB);

            Vector3 expectedNormal = (source.normalA + source.normalB + targetNormalA + targetNormalB).normalized;
            Vector3 triangleNormal = Vector3.Cross(vertices[baseIndex + 2] - vertices[baseIndex], vertices[baseIndex + 1] - vertices[baseIndex]);
            if (Vector3.Dot(triangleNormal, expectedNormal) >= 0f)
            {
                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 3);
            }
            else
            {
                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 3);
                triangles.Add(baseIndex + 2);
            }
        }

        private static void AddTriangleEdge(Dictionary<EdgeKey, EdgeBuildData> edges, int a, int b)
        {
            EdgeKey key = new EdgeKey(a, b);
            if (edges.TryGetValue(key, out EdgeBuildData data))
            {
                data.count++;
                edges[key] = data;
                return;
            }

            edges.Add(key, new EdgeBuildData { count = 1 });
        }

        private static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
        {
            Vector3 center = matrix.MultiplyPoint3x4(bounds.center);
            Vector3 extents = bounds.extents;
            Vector3 axisX = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
            Vector3 axisY = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
            Vector3 axisZ = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));
            extents = new Vector3(
                Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
            return new Bounds(center, extents * 2f);
        }

        private static Vector2 Project(Vector3 position, int axis)
        {
            switch (axis)
            {
                case 0: return new Vector2(position.y, position.z);
                case 1: return new Vector2(position.x, position.z);
                default: return new Vector2(position.x, position.y);
            }
        }

        private static float GetAxis(Vector3 value, int axis)
        {
            switch (axis)
            {
                case 0: return value.x;
                case 1: return value.y;
                default: return value.z;
            }
        }

        private static float GetBoundsMin(Bounds bounds, int axis)
        {
            return GetAxis(bounds.min, axis);
        }

        private static float GetBoundsMax(Bounds bounds, int axis)
        {
            return GetAxis(bounds.max, axis);
        }

        private readonly struct EdgeKey
        {
            public readonly int a;
            public readonly int b;

            public EdgeKey(int a, int b)
            {
                if (a < b)
                {
                    this.a = a;
                    this.b = b;
                }
                else
                {
                    this.a = b;
                    this.b = a;
                }
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (a * 397) ^ b;
                }
            }

            public override bool Equals(object obj)
            {
                return obj is EdgeKey other && a == other.a && b == other.b;
            }
        }

        private struct EdgeBuildData
        {
            public int count;
        }

        private readonly struct SeamEdge
        {
            public readonly Vector3 a;
            public readonly Vector3 b;
            public readonly Vector3 normalA;
            public readonly Vector3 normalB;
            public readonly Vector2 uvA;
            public readonly Vector2 uvB;
            public readonly Vector2 projectedA;
            public readonly Vector2 projectedB;
            public readonly Vector2 projectedMidpoint;
            public readonly Vector2 projectedDirection;

            public SeamEdge(
                Vector3 a,
                Vector3 b,
                Vector3 normalA,
                Vector3 normalB,
                Vector2 uvA,
                Vector2 uvB,
                Vector2 projectedA,
                Vector2 projectedB,
                Vector2 projectedMidpoint,
                Vector2 projectedDirection)
            {
                this.a = a;
                this.b = b;
                this.normalA = normalA;
                this.normalB = normalB;
                this.uvA = uvA;
                this.uvB = uvB;
                this.projectedA = projectedA;
                this.projectedB = projectedB;
                this.projectedMidpoint = projectedMidpoint;
                this.projectedDirection = projectedDirection;
            }
        }
    }
}
