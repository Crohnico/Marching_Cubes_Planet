using System;
using System.Collections.Generic;
using MarchingCubesPlanet.Coordinates;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarchingCubesPlanet.Preview
{
    public static class PlanetSpherePayloadMeshBuilder
    {
        public const int MinGeodesicFrequency = 1;
        public const int MaxGeodesicFrequency = 1000;
        public const int BaseIcosahedronTriangleCount = 20;

        private static readonly int[] IcosahedronTriangleVertexIndices =
        {
            0, 11, 5,
            0, 5, 1,
            0, 1, 7,
            0, 7, 10,
            0, 10, 11,
            1, 5, 9,
            5, 11, 4,
            11, 10, 2,
            10, 7, 6,
            7, 1, 8,
            3, 9, 4,
            3, 4, 2,
            3, 2, 6,
            3, 6, 8,
            3, 8, 9,
            4, 9, 5,
            2, 4, 11,
            6, 2, 10,
            8, 6, 7,
            9, 8, 1
        };

        public static int CalculateGeodesicFrequencyForPayload(int requestedTrianglePayload)
        {
            int requested = Mathf.Max(BaseIcosahedronTriangleCount, requestedTrianglePayload);
            int frequency = Mathf.RoundToInt(Mathf.Sqrt(requested / (float)BaseIcosahedronTriangleCount));
            return Mathf.Clamp(frequency, MinGeodesicFrequency, MaxGeodesicFrequency);
        }

        public static int CalculateTriangleCount(int geodesicFrequency)
        {
            ValidateGeodesicFrequency(geodesicFrequency);
            return checked(BaseIcosahedronTriangleCount * geodesicFrequency * geodesicFrequency);
        }

        public static int CalculateIndexCount(int geodesicFrequency)
        {
            return checked(CalculateTriangleCount(geodesicFrequency) * 3);
        }

        public static int CalculateVertexCount(int geodesicFrequency)
        {
            ValidateGeodesicFrequency(geodesicFrequency);
            int edgeVertexCount = checked(geodesicFrequency + 1);
            int nextEdgeVertexCount = checked(geodesicFrequency + 2);
            return checked(10 * edgeVertexCount * nextEdgeVertexCount);
        }

        public static float CalculateSurfaceRadius(in PlanetRecipe recipe)
        {
            float surfaceRadius = recipe.WorldRadius - recipe.IsoLevel;

            if (surfaceRadius <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(recipe),
                    surfaceRadius,
                    "WorldRadius - IsoLevel must be greater than zero.");
            }

            return surfaceRadius;
        }

        public static PlanetSpherePayloadBuildResult Build(
            Mesh mesh,
            in PlanetRecipe recipe,
            int geodesicFrequency,
            bool generateVertexColors)
        {
            if (mesh == null)
            {
                throw new ArgumentNullException(nameof(mesh));
            }

            if (!PlanetRecipeValidator.Validate(in recipe, out string recipeMessage))
            {
                throw new ArgumentException(recipeMessage, nameof(recipe));
            }

            int vertexCount = CalculateVertexCount(geodesicFrequency);
            int triangleCount = CalculateTriangleCount(geodesicFrequency);
            int indexCount = checked(triangleCount * 3);
            float radius = CalculateSurfaceRadius(in recipe);

            List<Vector3> vertices = new List<Vector3>(vertexCount);
            List<Vector3> normals = new List<Vector3>(vertexCount);
            List<Color32> colors = generateVertexColors ? new List<Color32>(vertexCount) : null;
            List<int> indices = new List<int>(indexCount);

            Vector3[] baseVertices = CreateIcosahedronVertices();

            for (int faceIndex = 0; faceIndex < IcosahedronTriangleVertexIndices.Length; faceIndex += 3)
            {
                Vector3 a = baseVertices[IcosahedronTriangleVertexIndices[faceIndex]];
                Vector3 b = baseVertices[IcosahedronTriangleVertexIndices[faceIndex + 1]];
                Vector3 c = baseVertices[IcosahedronTriangleVertexIndices[faceIndex + 2]];

                BuildFace(a, b, c, geodesicFrequency, radius, generateVertexColors, vertices, normals, colors, indices);
            }

            mesh.Clear();
            mesh.name = "PlanetRecipePayloadPreview_Icosphere_Runtime";
            mesh.indexFormat = vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            if (generateVertexColors)
            {
                mesh.SetColors(colors);
            }

            mesh.SetTriangles(indices, 0, true);
            mesh.RecalculateBounds();

            return new PlanetSpherePayloadBuildResult(vertexCount, triangleCount, indexCount, mesh.bounds);
        }

        private static void BuildFace(
            Vector3 a,
            Vector3 b,
            Vector3 c,
            int geodesicFrequency,
            float radius,
            bool generateVertexColors,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Color32> colors,
            List<int> indices)
        {
            int vertexStart = vertices.Count;

            for (int row = 0; row <= geodesicFrequency; row++)
            {
                for (int column = 0; column <= geodesicFrequency - row; column++)
                {
                    float weightB = row / (float)geodesicFrequency;
                    float weightC = column / (float)geodesicFrequency;
                    float weightA = 1f - weightB - weightC;
                    Vector3 direction = (a * weightA + b * weightB + c * weightC).normalized;

                    vertices.Add(direction * radius);
                    normals.Add(direction);

                    if (generateVertexColors)
                    {
                        colors.Add(EvaluateDebugColor(direction));
                    }
                }
            }

            for (int row = 0; row < geodesicFrequency; row++)
            {
                int rowLength = geodesicFrequency - row + 1;

                for (int column = 0; column < rowLength - 1; column++)
                {
                    int i0 = vertexStart + TriangularIndex(row, column, geodesicFrequency);
                    int i1 = vertexStart + TriangularIndex(row + 1, column, geodesicFrequency);
                    int i2 = vertexStart + TriangularIndex(row, column + 1, geodesicFrequency);

                    AddOutwardTriangle(vertices, indices, i0, i1, i2);

                    if (column < rowLength - 2)
                    {
                        int i3 = vertexStart + TriangularIndex(row + 1, column + 1, geodesicFrequency);
                        AddOutwardTriangle(vertices, indices, i2, i1, i3);
                    }
                }
            }
        }

        private static int TriangularIndex(int row, int column, int geodesicFrequency)
        {
            return row * (geodesicFrequency + 1) - row * (row - 1) / 2 + column;
        }

        private static void AddOutwardTriangle(List<Vector3> vertices, List<int> indices, int i0, int i1, int i2)
        {
            Vector3 a = vertices[i0];
            Vector3 b = vertices[i1];
            Vector3 c = vertices[i2];
            Vector3 normal = Vector3.Cross(b - a, c - a);

            if (Vector3.Dot(normal, a) >= 0f)
            {
                indices.Add(i0);
                indices.Add(i1);
                indices.Add(i2);
                return;
            }

            indices.Add(i0);
            indices.Add(i2);
            indices.Add(i1);
        }

        private static Vector3[] CreateIcosahedronVertices()
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;

            return new[]
            {
                new Vector3(-1f, t, 0f).normalized,
                new Vector3(1f, t, 0f).normalized,
                new Vector3(-1f, -t, 0f).normalized,
                new Vector3(1f, -t, 0f).normalized,
                new Vector3(0f, -1f, t).normalized,
                new Vector3(0f, 1f, t).normalized,
                new Vector3(0f, -1f, -t).normalized,
                new Vector3(0f, 1f, -t).normalized,
                new Vector3(t, 0f, -1f).normalized,
                new Vector3(t, 0f, 1f).normalized,
                new Vector3(-t, 0f, -1f).normalized,
                new Vector3(-t, 0f, 1f).normalized
            };
        }

        private static Color32 EvaluateDebugColor(Vector3 direction)
        {
            Color deep = new Color(0.18f, 0.25f, 0.68f, 1f);
            Color low = new Color(0.86f, 0.70f, 0.40f, 1f);
            Color mid = new Color(0.21f, 0.68f, 0.34f, 1f);
            Color high = new Color(0.93f, 0.96f, 0.88f, 1f);

            float height01 = Mathf.Clamp01(direction.y * 0.5f + 0.5f);
            Color color = height01 < 0.45f
                ? Color.Lerp(deep, low, height01 / 0.45f)
                : height01 < 0.75f
                    ? Color.Lerp(low, mid, (height01 - 0.45f) / 0.3f)
                    : Color.Lerp(mid, high, (height01 - 0.75f) / 0.25f);

            float longitude = Mathf.Atan2(direction.z, direction.x);
            float band = 0.92f + Mathf.Sin(longitude * 18f) * 0.08f;
            color.r *= band;
            color.g *= band;
            color.b *= band;
            color.a = 1f;
            return color;
        }

        private static void ValidateGeodesicFrequency(int geodesicFrequency)
        {
            if (geodesicFrequency < MinGeodesicFrequency || geodesicFrequency > MaxGeodesicFrequency)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(geodesicFrequency),
                    geodesicFrequency,
                    "geodesicFrequency must be between " + MinGeodesicFrequency + " and " + MaxGeodesicFrequency + ".");
            }
        }
    }
}
