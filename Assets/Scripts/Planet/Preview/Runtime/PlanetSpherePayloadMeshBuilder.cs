using System;
using System.Collections.Generic;
using MarchingCubesPlanet.Coordinates;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarchingCubesPlanet.Preview
{
    public static class PlanetSpherePayloadMeshBuilder
    {
        private static readonly Vector3[] FaceNormals =
        {
            Vector3.right,
            Vector3.left,
            Vector3.up,
            Vector3.down,
            Vector3.forward,
            Vector3.back
        };

        public static int CalculateTriangleCount(int faceResolution)
        {
            ValidateFaceResolution(faceResolution);
            return checked(12 * faceResolution * faceResolution);
        }

        public static int CalculateIndexCount(int faceResolution)
        {
            return checked(CalculateTriangleCount(faceResolution) * 3);
        }

        public static int CalculateVertexCount(int faceResolution)
        {
            ValidateFaceResolution(faceResolution);
            int sideVertexCount = checked(faceResolution + 1);
            return checked(6 * sideVertexCount * sideVertexCount);
        }

        public static PlanetSpherePayloadBuildResult Build(
            Mesh mesh,
            in PlanetRecipe recipe,
            int faceResolution,
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

            int vertexCount = CalculateVertexCount(faceResolution);
            int triangleCount = CalculateTriangleCount(faceResolution);
            int indexCount = checked(triangleCount * 3);
            float radius = recipe.WorldRadius;

            List<Vector3> vertices = new List<Vector3>(vertexCount);
            List<Vector3> normals = new List<Vector3>(vertexCount);
            List<Color32> colors = generateVertexColors ? new List<Color32>(vertexCount) : null;
            List<int> indices = new List<int>(indexCount);

            for (int faceIndex = 0; faceIndex < FaceNormals.Length; faceIndex++)
            {
                BuildFace(FaceNormals[faceIndex], faceResolution, radius, generateVertexColors, vertices, normals, colors, indices);
            }

            mesh.Clear();
            mesh.name = "PlanetRecipePayloadPreview_Mesh_Runtime";
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
            Vector3 faceNormal,
            int faceResolution,
            float radius,
            bool generateVertexColors,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Color32> colors,
            List<int> indices)
        {
            Vector3 axisA = new Vector3(faceNormal.y, faceNormal.z, faceNormal.x);
            Vector3 axisB = Vector3.Cross(faceNormal, axisA);
            int rowVertexCount = faceResolution + 1;
            int vertexStart = vertices.Count;

            for (int y = 0; y <= faceResolution; y++)
            {
                float percentY = y / (float)faceResolution;
                float coordinateY = percentY * 2f - 1f;

                for (int x = 0; x <= faceResolution; x++)
                {
                    float percentX = x / (float)faceResolution;
                    float coordinateX = percentX * 2f - 1f;
                    Vector3 pointOnCube = faceNormal + axisA * coordinateX + axisB * coordinateY;
                    Vector3 direction = pointOnCube.normalized;

                    vertices.Add(direction * radius);
                    normals.Add(direction);

                    if (generateVertexColors)
                    {
                        colors.Add(EvaluateDebugColor(direction));
                    }
                }
            }

            for (int y = 0; y < faceResolution; y++)
            {
                for (int x = 0; x < faceResolution; x++)
                {
                    int i0 = vertexStart + x + y * rowVertexCount;
                    int i1 = i0 + 1;
                    int i2 = i0 + rowVertexCount;
                    int i3 = i2 + 1;

                    indices.Add(i0);
                    indices.Add(i1);
                    indices.Add(i2);

                    indices.Add(i1);
                    indices.Add(i3);
                    indices.Add(i2);
                }
            }
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

        private static void ValidateFaceResolution(int faceResolution)
        {
            if (faceResolution <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(faceResolution), faceResolution, "faceResolution must be greater than zero.");
            }
        }
    }
}
