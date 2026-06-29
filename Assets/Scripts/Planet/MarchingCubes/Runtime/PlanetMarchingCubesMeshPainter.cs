using System;
using MarchingCubesPlanet.Coordinates;
using MarchingCubesPlanet.Shape;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarchingCubesPlanet.MarchingCubes
{
    public sealed class PlanetMarchingCubesMeshPainter
    {
        private const string SurfaceShaderName = "MarchingCubesPlanet/Planet/Surface";
        private const string UrpUnlitShaderName = "Universal Render Pipeline/Unlit";
        private const string DefaultSurfaceMaterialResourceName = "PlanetWorld_Surface";
        private const string SurfaceAtlasTexturePropertyName = "_PlanetSurfaceAtlas";
        private const string UseSurfaceAtlasPropertyName = "_UsePlanetSurfaceAtlas";
        private const int SurfaceAtlasResolution = 256;
        private const float LegacySeaLevelAtlasV = 0.337f;

        private Mesh runtimeMesh;
        private Material runtimeMaterial;
        private Texture2D runtimeSurfaceAtlas;

        public Mesh RuntimeMesh => runtimeMesh;
        public Material RuntimeMaterial => runtimeMaterial;
        public Texture2D RuntimeSurfaceAtlas => runtimeSurfaceAtlas;
        public bool OwnsRuntimeMaterial => runtimeMaterial != null;
        public bool OwnsRuntimeSurfaceAtlas => runtimeSurfaceAtlas != null;
        public long RuntimeSurfaceAtlasEstimatedBytes => runtimeSurfaceAtlas != null
            ? (long)runtimeSurfaceAtlas.width * runtimeSurfaceAtlas.height * 4L
            : 0L;
        public int RuntimeSurfaceAtlasPixelCount => runtimeSurfaceAtlas != null
            ? runtimeSurfaceAtlas.width * runtimeSurfaceAtlas.height
            : 0;

        public PlanetMarchingCubesPaintResult Paint(
            MeshFilter meshFilter,
            MeshRenderer meshRenderer,
            Material materialOverride,
            PlanetMarchingCubesExtractionResult source,
            in PlanetRecipe recipe,
            in PlanetPlacement placement,
            PlanetMarchingCubesPaintSettings settings)
        {
            if (meshFilter == null)
            {
                throw new ArgumentNullException(nameof(meshFilter));
            }

            if (meshRenderer == null)
            {
                throw new ArgumentNullException(nameof(meshRenderer));
            }

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (!settings.Validate(out string message))
            {
                throw new ArgumentException(message, nameof(settings));
            }

            if (!recipe.IsValid(out string recipeMessage))
            {
                throw new ArgumentException(recipeMessage, nameof(recipe));
            }

            int sourceVertexCount = source.VertexCount - source.VertexCount % 3;
            int sourceTriangleCount = sourceVertexCount / 3;
            if (sourceTriangleCount > settings.meshTriangleCapacity)
            {
                throw new InvalidOperationException(
                    "The full 07 result does not fit in the 08 temporary Mesh capacity. Increase meshTriangleCapacity; 08 does not paint partial meshes.");
            }

            int paintedVertexCount = sourceVertexCount;
            int paintedTriangleCount = sourceTriangleCount;
            PlanetGpuShapeCell[] cells = new PlanetGpuShapeCell[recipe.VoronoiDivision];
            PlanetGpuShapeCellBuilder.Build(in recipe, cells);

            Release(meshFilter, meshRenderer);

            runtimeMesh = new Mesh
            {
                name = "PlanetMarchingCubesPaint_Mesh_Runtime",
                indexFormat = paintedVertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
            };

            if (paintedVertexCount > 0)
            {
                BuildMesh(
                    runtimeMesh,
                    meshFilter.transform,
                    source.Vertices,
                    paintedVertexCount,
                    cells,
                    in recipe,
                    in placement,
                    settings);
            }

            meshFilter.sharedMesh = runtimeMesh;
            meshRenderer.sharedMaterial = ResolveMaterial(materialOverride, settings.colorMode, cells);

            long estimatedBytes = PlanetMarchingCubesPaintResult.CalculateMeshEstimatedBytes(
                paintedVertexCount,
                paintedTriangleCount);
            return new PlanetMarchingCubesPaintResult(
                sourceTriangleCount,
                paintedTriangleCount,
                paintedVertexCount,
                estimatedBytes,
                settings.colorMode);
        }

        public void Release(MeshFilter meshFilter, MeshRenderer meshRenderer)
        {
            ReleaseMeshOnly(meshFilter);

            if (runtimeMaterial != null)
            {
                if (meshRenderer != null && meshRenderer.sharedMaterial == runtimeMaterial)
                {
                    meshRenderer.sharedMaterial = null;
                }

                DestroyRuntimeObject(runtimeMaterial);
                runtimeMaterial = null;
            }

            if (runtimeSurfaceAtlas != null)
            {
                DestroyRuntimeObject(runtimeSurfaceAtlas);
                runtimeSurfaceAtlas = null;
            }
        }

        private void ReleaseMeshOnly(MeshFilter meshFilter)
        {
            if (runtimeMesh == null)
            {
                return;
            }

            if (meshFilter != null && meshFilter.sharedMesh == runtimeMesh)
            {
                meshFilter.sharedMesh = null;
            }

            DestroyRuntimeObject(runtimeMesh);
            runtimeMesh = null;
        }

        private static void BuildMesh(
            Mesh mesh,
            Transform targetTransform,
            PlanetMarchingCubesVertex[] sourceVertices,
            int vertexCount,
            PlanetGpuShapeCell[] cells,
            in PlanetRecipe recipe,
            in PlanetPlacement placement,
            PlanetMarchingCubesPaintSettings settings)
        {
            Vector3[] positions = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            Color32[] colors = new Color32[vertexCount];
            int[] indices = new int[vertexCount];

            float minRadius = float.MaxValue;
            float maxRadius = float.MinValue;
            for (int i = 0; i < vertexCount; i++)
            {
                Vector4 packedPosition = sourceVertices[i].positionAndCase;
                Vector3 gridPosition = new Vector3(packedPosition.x, packedPosition.y, packedPosition.z);
                float radius = gridPosition.magnitude;
                if (radius < minRadius)
                {
                    minRadius = radius;
                }

                if (radius > maxRadius)
                {
                    maxRadius = radius;
                }
            }

            for (int i = 0; i < vertexCount; i += 3)
            {
                Vector3 a = ReadGridPosition(sourceVertices[i]);
                Vector3 b = ReadGridPosition(sourceVertices[i + 1]);
                Vector3 c = ReadGridPosition(sourceVertices[i + 2]);
                Vector3 triangleCenter = (a + b + c) * 0.33333334f;
                Vector2 triangleUv = EvaluateSurfaceAtlasUv(triangleCenter.magnitude, in recipe);

                for (int corner = 0; corner < 3; corner++)
                {
                    int vertexIndex = i + corner;
                    PlanetMarchingCubesVertex sourceVertex = sourceVertices[vertexIndex];
                    Vector4 packedPosition = sourceVertex.positionAndCase;
                    Vector4 packedNormal = sourceVertex.normalAndDiagnostic;
                    Vector3 gridPosition = new Vector3(packedPosition.x, packedPosition.y, packedPosition.z);
                    Vector3 gridNormal = new Vector3(packedNormal.x, packedNormal.y, packedNormal.z);
                    if (gridNormal.sqrMagnitude > 0.0001f)
                    {
                        gridNormal.Normalize();
                    }
                    else
                    {
                        gridNormal = Vector3.up;
                    }

                    Vector3 worldPosition = PlanetCoordinateConverter.GridToWorld(gridPosition, in recipe, in placement);
                    Vector3 worldNormal = placement.PlanetRotation * gridNormal;
                    positions[vertexIndex] = targetTransform != null ? targetTransform.InverseTransformPoint(worldPosition) : worldPosition;
                    normals[vertexIndex] = targetTransform != null ? targetTransform.InverseTransformDirection(worldNormal).normalized : worldNormal.normalized;
                    float height01 = triangleUv.y;
                    uvs[vertexIndex] = triangleUv;
                    colors[vertexIndex] = EvaluateColor(
                        settings,
                        gridPosition,
                        gridNormal,
                        Mathf.RoundToInt(packedPosition.w),
                        vertexIndex / 3,
                        minRadius,
                        maxRadius,
                        height01);
                    indices[vertexIndex] = vertexIndex;
                }
            }

            mesh.Clear();
            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.colors32 = colors;
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, true);
            mesh.RecalculateBounds();
        }

        private Material ResolveMaterial(Material materialOverride, PlanetMarchingCubesPaintColorMode colorMode, PlanetGpuShapeCell[] cells)
        {
            if (materialOverride != null)
            {
                ApplyMaterialProperties(materialOverride, colorMode, cells);
                return materialOverride;
            }

            Material defaultSurfaceMaterial = Resources.Load<Material>(DefaultSurfaceMaterialResourceName);
            if (defaultSurfaceMaterial != null)
            {
                runtimeMaterial = new Material(defaultSurfaceMaterial)
                {
                    name = "PlanetMarchingCubesPaint_SurfaceMaterial_Runtime"
                };
                ApplyMaterialProperties(runtimeMaterial, colorMode, cells);
                return runtimeMaterial;
            }

            Shader shader = Shader.Find(SurfaceShaderName);
            if (shader == null)
            {
                shader = Shader.Find(UrpUnlitShaderName);
            }

            if (shader == null)
            {
                throw new InvalidOperationException("No planet surface material shader was found for PlanetMarchingCubesMeshPainter.");
            }

            runtimeMaterial = new Material(shader)
            {
                name = "PlanetMarchingCubesPaint_SurfaceMaterial_Runtime"
            };

            if (shader.name == UrpUnlitShaderName)
            {
                runtimeMaterial.color = Color.white;
            }

            ApplyMaterialProperties(runtimeMaterial, colorMode, cells);
            return runtimeMaterial;
        }

        private void ApplyMaterialProperties(Material material, PlanetMarchingCubesPaintColorMode colorMode, PlanetGpuShapeCell[] cells)
        {
            if (material == null)
            {
                return;
            }

            bool useSurfaceAtlas = colorMode == PlanetMarchingCubesPaintColorMode.PlanetSurfaceAtlas;
            bool supportsRuntimeSurfaceAtlas = material.HasProperty(SurfaceAtlasTexturePropertyName);
            if (useSurfaceAtlas && supportsRuntimeSurfaceAtlas)
            {
                EnsureSurfaceAtlas(cells);
            }

            if (material.HasProperty(UseSurfaceAtlasPropertyName))
            {
                material.SetFloat(UseSurfaceAtlasPropertyName, useSurfaceAtlas ? 1f : 0f);
            }

            if (useSurfaceAtlas && supportsRuntimeSurfaceAtlas && runtimeSurfaceAtlas != null)
            {
                material.SetTexture(SurfaceAtlasTexturePropertyName, runtimeSurfaceAtlas);
            }
        }

        private static Color32 EvaluateColor(
            PlanetMarchingCubesPaintSettings settings,
            Vector3 position,
            Vector3 normal,
            int caseIndex,
            int triangleIndex,
            float minRadius,
            float maxRadius,
            float height01)
        {
            switch (settings.colorMode)
            {
                case PlanetMarchingCubesPaintColorMode.PlanetSurfaceAtlas:
                    return Color.white;
                case PlanetMarchingCubesPaintColorMode.FlatNormalColor:
                    return new Color(
                        normal.x * 0.5f + 0.5f,
                        normal.y * 0.5f + 0.5f,
                        normal.z * 0.5f + 0.5f,
                        1f);
                case PlanetMarchingCubesPaintColorMode.HeightColor:
                    return Color.Lerp(new Color(0.1f, 0.35f, 1f, 1f), new Color(0.95f, 1f, 0.35f, 1f), height01);
                case PlanetMarchingCubesPaintColorMode.CaseIndexPalette:
                    return Palette(caseIndex);
                case PlanetMarchingCubesPaintColorMode.TrianglePalette:
                    return Palette(triangleIndex);
                case PlanetMarchingCubesPaintColorMode.SolidColor:
                    return settings.solidColor;
                default:
                    return Color.white;
            }
        }

        private void EnsureSurfaceAtlas(PlanetGpuShapeCell[] cells)
        {
            if (runtimeSurfaceAtlas != null)
            {
                return;
            }

            int cellCount = cells == null ? 0 : cells.Length;
            int atlasWidth = Mathf.Max(1, cellCount);
            runtimeSurfaceAtlas = new Texture2D(atlasWidth, SurfaceAtlasResolution, TextureFormat.RGBA32, false, true)
            {
                name = "PlanetMarchingCubesPaint_SurfaceAtlas_Runtime",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color32[] pixels = new Color32[atlasWidth * SurfaceAtlasResolution];
            for (int x = 0; x < atlasWidth; x++)
            {
                for (int y = 0; y < SurfaceAtlasResolution; y++)
                {
                    float t = y / (float)(SurfaceAtlasResolution - 1);
                    pixels[y * atlasWidth + x] = EvaluateSurfaceCellGradient(t, x);
                }
            }

            runtimeSurfaceAtlas.SetPixels32(pixels);
            runtimeSurfaceAtlas.Apply(false, false);
        }

        private static Vector3 ReadGridPosition(PlanetMarchingCubesVertex vertex)
        {
            Vector4 packedPosition = vertex.positionAndCase;
            return new Vector3(packedPosition.x, packedPosition.y, packedPosition.z);
        }

        private static int FindNearestCellIndex(Vector3 gridPosition, PlanetGpuShapeCell[] cells)
        {
            if (cells == null || cells.Length == 0)
            {
                return 0;
            }

            Vector3 direction = gridPosition.sqrMagnitude > 0.0001f ? gridPosition.normalized : Vector3.up;
            int nearestIndex = 0;
            float nearestDot = -2f;
            for (int i = 0; i < cells.Length; i++)
            {
                float cellDot = Vector3.Dot(direction, cells[i].Direction);
                if (cellDot > nearestDot)
                {
                    nearestDot = cellDot;
                    nearestIndex = i;
                }
            }

            return nearestIndex;
        }

        private static Vector2 EvaluateSurfaceAtlasUv(float radius, in PlanetRecipe recipe)
        {
            float surfaceOffset = radius - recipe.GridRadius;
            float safeRadius = Mathf.Max(recipe.GridRadius, 0.0001f);
            float minHeightAtlasOffset =
                -safeRadius * Mathf.Max(recipe.OceanDepth, recipe.MinimumOceanDepth) -
                safeRadius * Mathf.Max(0f, recipe.SurfaceNoiseAmplitude);
            float maxHeightAtlasOffset =
                safeRadius * recipe.MaxLandElevation * recipe.MaxHeightModifier +
                safeRadius * Mathf.Max(0f, recipe.SurfaceNoiseAmplitude);

            if (surfaceOffset <= 0f)
            {
                float depthRange = Mathf.Max(0.0001f, -minHeightAtlasOffset);
                float underwaterHeight = Mathf.Clamp01((surfaceOffset - minHeightAtlasOffset) / depthRange);
                return new Vector2(0.5f, Mathf.Lerp(0f, LegacySeaLevelAtlasV, underwaterHeight));
            }

            float landRange = Mathf.Max(0.0001f, maxHeightAtlasOffset);
            float landHeight = Mathf.Clamp01(surfaceOffset / landRange);
            return new Vector2(0.5f, Mathf.Lerp(LegacySeaLevelAtlasV, 1f, landHeight));
        }

        private static float EvaluateHeight01(float radius, float minRadius, float maxRadius)
        {
            if (maxRadius - minRadius <= 0.0001f)
            {
                return 0.5f;
            }

            return Mathf.InverseLerp(minRadius, maxRadius, radius);
        }

        private static Color32 EvaluateSurfaceGradient(float height01)
        {
            height01 = Mathf.Clamp01(height01);

            Color underwater = new Color(0.82f, 0.36f, 0.54f, 1f);
            Color shallow = new Color(0.91f, 0.74f, 0.57f, 1f);
            Color sand = new Color(0.76f, 0.66f, 0.43f, 1f);
            Color darkGreen = new Color(0.13f, 0.31f, 0.18f, 1f);
            Color green = new Color(0.25f, 0.48f, 0.24f, 1f);
            Color brown = new Color(0.38f, 0.29f, 0.20f, 1f);
            Color grey = new Color(0.50f, 0.50f, 0.47f, 1f);
            Color snow = new Color(0.88f, 0.89f, 0.84f, 1f);

            if (height01 < 0.12f)
            {
                return Color.Lerp(underwater, shallow, height01 / 0.12f);
            }

            if (height01 < 0.20f)
            {
                return Color.Lerp(shallow, sand, (height01 - 0.12f) / 0.08f);
            }

            if (height01 < 0.38f)
            {
                return Color.Lerp(sand, darkGreen, (height01 - 0.20f) / 0.18f);
            }

            if (height01 < 0.58f)
            {
                return Color.Lerp(darkGreen, green, (height01 - 0.38f) / 0.20f);
            }

            if (height01 < 0.76f)
            {
                return Color.Lerp(green, brown, (height01 - 0.58f) / 0.18f);
            }

            if (height01 < 0.90f)
            {
                return Color.Lerp(brown, grey, (height01 - 0.76f) / 0.14f);
            }

            return Color.Lerp(grey, snow, (height01 - 0.90f) / 0.10f);
        }

        private static Color32 EvaluateSurfaceCellGradient(float height01, int cellIndex)
        {
            Color baseColor = EvaluatePlanetSurfacePalette(height01);

            float valueNoise = Hash01((uint)cellIndex, 0x6ac690c5u);
            float warmthNoise = Hash01((uint)cellIndex, 0x9e3779b9u) - 0.5f;
            float value = Mathf.Lerp(0.94f, 1.06f, valueNoise);
            Color cellColor = new Color(
                Mathf.Clamp01(baseColor.r * value + warmthNoise * 0.025f),
                Mathf.Clamp01(baseColor.g * value),
                Mathf.Clamp01(baseColor.b * value - warmthNoise * 0.020f),
                1f);

            return cellColor;
        }

        private static Color EvaluatePlanetSurfacePalette(float height01)
        {
            height01 = Mathf.Clamp01(height01);

            Color deepPink = new Color(0.78f, 0.36f, 0.55f, 1f);
            Color salmon = new Color(0.72f, 0.42f, 0.45f, 1f);
            Color darkRedBrown = new Color(0.28f, 0.17f, 0.15f, 1f);
            Color roseRed = new Color(0.62f, 0.32f, 0.38f, 1f);
            Color sand = new Color(0.78f, 0.68f, 0.44f, 1f);
            Color paleYellow = new Color(0.88f, 0.80f, 0.56f, 1f);
            Color brightGreen = new Color(0.42f, 0.62f, 0.29f, 1f);
            Color darkGreen = new Color(0.17f, 0.40f, 0.19f, 1f);
            Color brown = new Color(0.43f, 0.31f, 0.20f, 1f);
            Color darkGrey = new Color(0.32f, 0.32f, 0.30f, 1f);
            Color grey = new Color(0.55f, 0.55f, 0.51f, 1f);
            Color lightGrey = new Color(0.78f, 0.78f, 0.74f, 1f);
            Color snow = new Color(0.93f, 0.93f, 0.89f, 1f);

            if (height01 < 0.12f)
            {
                return Color.Lerp(deepPink, salmon, height01 / 0.12f);
            }

            if (height01 < 0.24f)
            {
                return Color.Lerp(salmon, darkRedBrown, (height01 - 0.12f) / 0.12f);
            }

            if (height01 < 0.36f)
            {
                return Color.Lerp(darkRedBrown, roseRed, (height01 - 0.24f) / 0.12f);
            }

            if (height01 < 0.5f)
            {
                return Color.Lerp(roseRed, sand, (height01 - 0.36f) / 0.14f);
            }

            if (height01 < 0.58f)
            {
                return Color.Lerp(sand, paleYellow, (height01 - 0.5f) / 0.08f);
            }

            if (height01 < 0.68f)
            {
                return Color.Lerp(paleYellow, brightGreen, (height01 - 0.58f) / 0.10f);
            }

            if (height01 < 0.78f)
            {
                return Color.Lerp(brightGreen, darkGreen, (height01 - 0.68f) / 0.10f);
            }

            if (height01 < 0.86f)
            {
                return Color.Lerp(darkGreen, brown, (height01 - 0.78f) / 0.08f);
            }

            if (height01 < 0.93f)
            {
                return Color.Lerp(brown, darkGrey, (height01 - 0.86f) / 0.07f);
            }

            if (height01 < 0.97f)
            {
                return Color.Lerp(darkGrey, grey, (height01 - 0.93f) / 0.04f);
            }

            if (height01 < 0.99f)
            {
                return Color.Lerp(grey, lightGrey, (height01 - 0.97f) / 0.02f);
            }

            return Color.Lerp(lightGrey, snow, (height01 - 0.99f) / 0.01f);
        }

        private static float Hash01(uint value, uint salt)
        {
            uint hash = value ^ salt;
            hash ^= hash >> 16;
            hash *= 0x7feb352dU;
            hash ^= hash >> 15;
            hash *= 0x846ca68bU;
            hash ^= hash >> 16;
            return (hash & 0x00ffffffU) / 16777215f;
        }

        private static Color32 Palette(int value)
        {
            uint hash = (uint)value;
            hash ^= hash >> 16;
            hash *= 0x7feb352dU;
            hash ^= hash >> 15;
            hash *= 0x846ca68bU;
            hash ^= hash >> 16;
            byte r = (byte)(80 + (hash & 127U));
            byte g = (byte)(80 + ((hash >> 8) & 127U));
            byte b = (byte)(80 + ((hash >> 16) & 127U));
            return new Color32(r, g, b, 255);
        }

        private static void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
