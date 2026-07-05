using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarchingCubesPlanet.TrianglePools
{
    public sealed class PlanetTriangleGpuBackend
    {
        private const string BackendObjectName = "PlanetTriangleGpuBackend_";
        private const string SurfaceGpuShaderName = "MarchingCubesPlanet/Planet/SurfaceGpu";
        private const string WaterGpuShaderName = "MarchingCubesPlanet/Planet/WaterGpu";
        private const string VertexBufferPropertyName = "_PlanetTriangleVertices";
        private const string SurfaceAtlasTexturePropertyName = "_PlanetSurfaceAtlas";
        private const string UseSurfaceAtlasPropertyName = "_UsePlanetSurfaceAtlas";
        private const string BaseMapTexturePropertyName = "_BaseMap";
        private const string MainTexturePropertyName = "_MainTex";
        private const string BaseColorPropertyName = "_BaseColor";
        private const string ColorPropertyName = "_Color";
        private const string SpecColorPropertyName = "_SpecColor";
        private const string SmoothnessPropertyName = "_Smoothness";
        private const string MetallicPropertyName = "_Metallic";

        private struct Publication
        {
            public uint meshId;
            public int vertexStart;
            public int vertexCount;
            public int vertexCapacity;
            public Bounds bounds;
            public bool occupied;
        }

        private struct FreeRange
        {
            public int start;
            public int count;
        }

        private readonly uint artistId;
        private readonly List<Publication> publications = new List<Publication>(256);
        private readonly List<FreeRange> freeRanges = new List<FreeRange>(256);
        private GraphicsBuffer vertexBuffer;
        private GraphicsBuffer waterVertexBuffer;
        private PlanetTriangleGpuVertex[] uploadScratch;
        private PlanetTriangleGpuVertex[] clearScratch;
        private Material material;
        private Material waterMaterial;
        private Material sourceMaterial;
        private Material waterSourceMaterial;
        private GameObject renderObject;
        private PlanetTriangleGpuRenderer renderer;
        private Bounds worldBounds;
        private Bounds waterWorldBounds;
        private int vertexCapacity;
        private int waterVertexCapacity;
        private int highWatermarkVertexCount;
        private int activeVertexCount;
        private int waterVertexCount;
        private bool hasBounds;

        public PlanetTriangleGpuBackend(uint artistId)
        {
            this.artistId = artistId;
        }

        public int VertexCapacity => vertexCapacity;
        public int ActiveVertexCount => activeVertexCount;
        public int HighWatermarkVertexCount => highWatermarkVertexCount;
        public int LastPublishedVertexCount { get; private set; }
        public long EstimatedGpuBytes => vertexCapacity * (long)PlanetTriangleGpuVertex.Stride;
        public long EstimatedWaterGpuBytes => waterVertexCapacity * (long)PlanetTriangleGpuVertex.Stride;
        public bool HasVisibleData => vertexBuffer != null && highWatermarkVertexCount > 0 && activeVertexCount > 0;
        public bool HasVisibleWaterData => waterVertexBuffer != null && waterVertexCount > 0;

        public void EnsureInitialized(int totalTriangleBudget)
        {
            int safeTriangleBudget = Mathf.Max(1, totalTriangleBudget);
            int requestedVertexCapacity = safeTriangleBudget * 3;
            if (vertexBuffer != null && vertexCapacity == requestedVertexCapacity)
            {
                EnsureRenderer();
                return;
            }

            Release();
            vertexCapacity = requestedVertexCapacity;
            vertexBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                vertexCapacity,
                PlanetTriangleGpuVertex.Stride)
            {
                name = "PlanetTriangleGpuVertexBuffer_" + artistId
            };

            EnsureRenderer();
        }

        public void SetMaterial(Material value)
        {
            if (value == null)
            {
                return;
            }

            if (material != null && sourceMaterial == value)
            {
                return;
            }

            sourceMaterial = value;
            Shader shader = Shader.Find(SurfaceGpuShaderName);
            if (shader == null)
            {
                Debug.LogError("Missing GPU surface shader: " + SurfaceGpuShaderName);
                return;
            }

            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "PlanetTriangleGpu_Surface_Runtime"
                };
            }
            else
            {
                material.shader = shader;
            }

            material.CopyPropertiesFromMaterial(value);
            material.shader = shader;

            if (value.HasProperty(BaseMapTexturePropertyName))
            {
                material.SetTexture(BaseMapTexturePropertyName, value.GetTexture(BaseMapTexturePropertyName));
                material.SetTextureScale(BaseMapTexturePropertyName, value.GetTextureScale(BaseMapTexturePropertyName));
                material.SetTextureOffset(BaseMapTexturePropertyName, value.GetTextureOffset(BaseMapTexturePropertyName));
            }
            else if (value.HasProperty(MainTexturePropertyName))
            {
                material.SetTexture(BaseMapTexturePropertyName, value.GetTexture(MainTexturePropertyName));
                material.SetTextureScale(BaseMapTexturePropertyName, value.GetTextureScale(MainTexturePropertyName));
                material.SetTextureOffset(BaseMapTexturePropertyName, value.GetTextureOffset(MainTexturePropertyName));
            }

            if (value.HasProperty(SurfaceAtlasTexturePropertyName))
            {
                material.SetTexture(SurfaceAtlasTexturePropertyName, value.GetTexture(SurfaceAtlasTexturePropertyName));
            }

            if (value.HasProperty(UseSurfaceAtlasPropertyName))
            {
                material.SetFloat(UseSurfaceAtlasPropertyName, value.GetFloat(UseSurfaceAtlasPropertyName));
            }
            else
            {
                material.SetFloat(UseSurfaceAtlasPropertyName, 0f);
            }
        }

        public bool Publish(uint meshId, IList<PlanetTriangleGpuVertex> vertices, int vertexCount, Bounds bounds, Material source)
        {
            LastPublishedVertexCount = 0;
            if (vertices == null)
            {
                return false;
            }

            vertexCount = Mathf.Max(0, Mathf.Min(vertexCount, vertices.Count));
            vertexCount -= vertexCount % 3;
            if (vertexCount <= 0)
            {
                ReleasePublication(meshId);
                return true;
            }

            EnsureInitialized(PlanetTrianglePoolRegistry.GetOrCreate(artistId).TotalTriangleBudget);
            SetMaterial(source);
            if (vertexBuffer == null)
            {
                return false;
            }

            ReleasePublication(meshId, false);
            int remainingVertexCount = vertexCount;
            int sourceVertexStart = 0;
            while (remainingVertexCount > 0 &&
                   TryAllocateVertexRange(remainingVertexCount, out int vertexStart, out int allocatedVertexCount))
            {
                WriteVertices(vertices, sourceVertexStart, allocatedVertexCount, vertexStart);
                AddPublication(meshId, vertexStart, allocatedVertexCount, bounds);
                activeVertexCount += allocatedVertexCount;
                LastPublishedVertexCount += allocatedVertexCount;
                sourceVertexStart += allocatedVertexCount;
                remainingVertexCount -= allocatedVertexCount;
            }

            if (LastPublishedVertexCount <= 0)
            {
                Debug.LogWarning(
                    "[09 GPU] Not enough GPU triangle buffer space. artistId=" + artistId +
                    " requestedVertices=" + vertexCount +
                    " capacity=" + vertexCapacity +
                    " highWatermark=" + highWatermarkVertexCount);
                RebuildBounds();
                UpdateRenderer();
                return false;
            }

            RebuildBounds();
            UpdateRenderer();
            return true;
        }

        public bool PublishWater(IList<PlanetTriangleGpuVertex> vertices, int vertexCount, Bounds bounds, Material source)
        {
            if (vertices == null)
            {
                return false;
            }

            vertexCount = Mathf.Max(0, Mathf.Min(vertexCount, vertices.Count));
            vertexCount -= vertexCount % 3;
            if (vertexCount <= 0)
            {
                ReleaseWaterPublication();
                return true;
            }

            EnsureRenderer();
            EnsureWaterCapacity(vertexCount);
            SetWaterMaterial(source);
            if (waterVertexBuffer == null || waterMaterial == null)
            {
                return false;
            }

            WriteWaterVertices(vertices, vertexCount);
            waterVertexCount = vertexCount;
            waterWorldBounds = bounds;
            UpdateRenderer();
            return true;
        }

        public int ReleasePublication(uint meshId)
        {
            return ReleasePublication(meshId, true);
        }

        public void ReleaseWaterPublication()
        {
            if (waterVertexBuffer != null && waterVertexCount > 0)
            {
                EnsureClearScratch(waterVertexCount);
                waterVertexBuffer.SetData(clearScratch, 0, 0, waterVertexCount);
            }

            waterVertexCount = 0;
            waterWorldBounds = default;
            UpdateRenderer();
        }

        private int ReleasePublication(uint meshId, bool updateRenderer)
        {
            int released = 0;
            for (int i = 0; i < publications.Count; i++)
            {
                Publication publication = publications[i];
                if (!publication.occupied || publication.meshId != meshId)
                {
                    continue;
                }

                ClearRange(publication.vertexStart, publication.vertexCapacity);
                AddFreeRange(publication.vertexStart, publication.vertexCapacity);
                activeVertexCount = Mathf.Max(0, activeVertexCount - publication.vertexCount);
                publication.occupied = false;
                publications[i] = publication;
                released += publication.vertexCount / 3;
            }

            if (updateRenderer)
            {
                RebuildBounds();
                UpdateRenderer();
            }

            return released;
        }

        public void ReleaseAllPublications()
        {
            if (vertexBuffer != null && highWatermarkVertexCount > 0)
            {
                ClearRange(0, highWatermarkVertexCount);
            }

            publications.Clear();
            freeRanges.Clear();
            activeVertexCount = 0;
            highWatermarkVertexCount = 0;
            hasBounds = false;
            worldBounds = default;
            ReleaseWaterPublication();
            UpdateRenderer();
        }

        public void Release()
        {
            ReleaseAllPublications();
            if (vertexBuffer != null)
            {
                vertexBuffer.Release();
                vertexBuffer = null;
            }

            if (material != null)
            {
                UnityEngine.Object.Destroy(material);
                material = null;
            }

            if (waterVertexBuffer != null)
            {
                waterVertexBuffer.Release();
                waterVertexBuffer = null;
            }

            if (waterMaterial != null)
            {
                UnityEngine.Object.Destroy(waterMaterial);
                waterMaterial = null;
            }

            if (renderObject != null)
            {
                UnityEngine.Object.Destroy(renderObject);
                renderObject = null;
                renderer = null;
            }

            vertexCapacity = 0;
            waterVertexCapacity = 0;
            waterVertexCount = 0;
            sourceMaterial = null;
            waterSourceMaterial = null;
        }

        private void EnsureRenderer()
        {
            if (renderObject != null && renderer != null)
            {
                return;
            }

            renderObject = new GameObject(BackendObjectName + artistId)
            {
                hideFlags = HideFlags.DontSave
            };
            renderer = renderObject.AddComponent<PlanetTriangleGpuRenderer>();
            UpdateRenderer();
        }

        private void UpdateRenderer()
        {
            if (renderer == null)
            {
                return;
            }

            renderer.Configure(
                vertexBuffer,
                material,
                highWatermarkVertexCount,
                hasBounds ? worldBounds : new Bounds(Vector3.zero, Vector3.one),
                VertexBufferPropertyName);
            renderer.ConfigureWater(
                waterVertexBuffer,
                waterMaterial,
                waterVertexCount,
                waterVertexCount > 0 ? waterWorldBounds : new Bounds(Vector3.zero, Vector3.one),
                VertexBufferPropertyName);
        }

        private void EnsureWaterCapacity(int requestedVertexCapacity)
        {
            requestedVertexCapacity = Mathf.Max(3, requestedVertexCapacity);
            requestedVertexCapacity -= requestedVertexCapacity % 3;
            if (waterVertexBuffer != null && waterVertexCapacity >= requestedVertexCapacity)
            {
                return;
            }

            if (waterVertexBuffer != null)
            {
                waterVertexBuffer.Release();
                waterVertexBuffer = null;
            }

            waterVertexCapacity = requestedVertexCapacity;
            waterVertexBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                waterVertexCapacity,
                PlanetTriangleGpuVertex.Stride)
            {
                name = "PlanetTriangleGpuWaterVertexBuffer_" + artistId
            };
        }

        private void SetWaterMaterial(Material value)
        {
            if (value == null)
            {
                return;
            }

            waterSourceMaterial = value;
            Shader shader = Shader.Find(WaterGpuShaderName);
            if (shader == null)
            {
                Debug.LogError("Missing GPU water shader: " + WaterGpuShaderName);
                return;
            }

            if (waterMaterial == null)
            {
                waterMaterial = new Material(shader)
                {
                    name = "PlanetTriangleGpu_Water_Runtime"
                };
            }
            else
            {
                waterMaterial.shader = shader;
            }

            CopyWaterMaterialProperties(value, waterMaterial);
            waterMaterial.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void CopyWaterMaterialProperties(Material source, Material target)
        {
            if (source == null || target == null)
            {
                return;
            }

            Color baseColor = source.HasProperty(BaseColorPropertyName)
                ? source.GetColor(BaseColorPropertyName)
                : source.HasProperty(ColorPropertyName)
                    ? source.GetColor(ColorPropertyName)
                    : new Color(0.08f, 0.42f, 0.72f, 0.78f);
            Color specColor = source.HasProperty(SpecColorPropertyName)
                ? source.GetColor(SpecColorPropertyName)
                : new Color(0.18f, 0.26f, 0.30f, 1f);
            float smoothness = source.HasProperty(SmoothnessPropertyName)
                ? source.GetFloat(SmoothnessPropertyName)
                : 0.72f;
            float metallic = source.HasProperty(MetallicPropertyName)
                ? source.GetFloat(MetallicPropertyName)
                : 0f;

            target.SetColor(BaseColorPropertyName, baseColor);
            target.SetColor(SpecColorPropertyName, specColor);
            target.SetFloat(SmoothnessPropertyName, smoothness);
            target.SetFloat(MetallicPropertyName, metallic);
        }

        private bool TryAllocateVertexRange(int requestedVertexCount, out int vertexStart, out int allocatedVertexCount)
        {
            for (int i = 0; i < freeRanges.Count; i++)
            {
                FreeRange range = freeRanges[i];
                allocatedVertexCount = Mathf.Min(requestedVertexCount, range.count);
                allocatedVertexCount -= allocatedVertexCount % 3;
                if (allocatedVertexCount <= 0)
                {
                    continue;
                }

                vertexStart = range.start;
                range.start += allocatedVertexCount;
                range.count -= allocatedVertexCount;
                if (range.count <= 0)
                {
                    freeRanges.RemoveAt(i);
                }
                else
                {
                    freeRanges[i] = range;
                }

                return true;
            }

            allocatedVertexCount = Mathf.Min(requestedVertexCount, vertexCapacity - highWatermarkVertexCount);
            allocatedVertexCount -= allocatedVertexCount % 3;
            if (allocatedVertexCount <= 0)
            {
                vertexStart = 0;
                return false;
            }

            vertexStart = highWatermarkVertexCount;
            highWatermarkVertexCount += allocatedVertexCount;
            return true;
        }

        private void AddPublication(uint meshId, int vertexStart, int vertexCount, Bounds bounds)
        {
            for (int i = 0; i < publications.Count; i++)
            {
                if (publications[i].occupied)
                {
                    continue;
                }

                publications[i] = new Publication
                {
                    meshId = meshId,
                    vertexStart = vertexStart,
                    vertexCount = vertexCount,
                    vertexCapacity = vertexCount,
                    bounds = bounds,
                    occupied = true
                };
                return;
            }

            publications.Add(new Publication
            {
                meshId = meshId,
                vertexStart = vertexStart,
                vertexCount = vertexCount,
                vertexCapacity = vertexCount,
                bounds = bounds,
                occupied = true
            });
        }

        private void WriteVertices(
            IList<PlanetTriangleGpuVertex> vertices,
            int sourceVertexStart,
            int vertexCount,
            int vertexStart)
        {
            EnsureUploadScratch(vertexCount);
            for (int i = 0; i < vertexCount; i++)
            {
                uploadScratch[i] = vertices[sourceVertexStart + i];
            }

            vertexBuffer.SetData(uploadScratch, 0, vertexStart, vertexCount);
        }

        private void WriteWaterVertices(IList<PlanetTriangleGpuVertex> vertices, int vertexCount)
        {
            EnsureUploadScratch(vertexCount);
            for (int i = 0; i < vertexCount; i++)
            {
                uploadScratch[i] = vertices[i];
            }

            waterVertexBuffer.SetData(uploadScratch, 0, 0, vertexCount);
        }

        private void AddFreeRange(int start, int count)
        {
            if (count <= 0)
            {
                return;
            }

            freeRanges.Add(new FreeRange { start = start, count = count });
            freeRanges.Sort((a, b) => a.start.CompareTo(b.start));
            for (int i = 0; i < freeRanges.Count - 1;)
            {
                FreeRange current = freeRanges[i];
                FreeRange next = freeRanges[i + 1];
                if (current.start + current.count < next.start)
                {
                    i++;
                    continue;
                }

                current.count = Mathf.Max(current.start + current.count, next.start + next.count) - current.start;
                freeRanges[i] = current;
                freeRanges.RemoveAt(i + 1);
            }
        }

        private void ClearRange(int start, int count)
        {
            if (vertexBuffer == null || count <= 0)
            {
                return;
            }

            EnsureClearScratch(count);
            vertexBuffer.SetData(clearScratch, 0, start, count);
        }

        private void EnsureUploadScratch(int count)
        {
            if (uploadScratch == null || uploadScratch.Length < count)
            {
                uploadScratch = new PlanetTriangleGpuVertex[count];
            }
        }

        private void EnsureClearScratch(int count)
        {
            if (clearScratch == null || clearScratch.Length < count)
            {
                clearScratch = new PlanetTriangleGpuVertex[count];
            }
        }

        private void IncludeBounds(Bounds bounds)
        {
            if (!hasBounds)
            {
                worldBounds = bounds;
                hasBounds = true;
                return;
            }

            worldBounds.Encapsulate(bounds);
        }

        private void RebuildBounds()
        {
            hasBounds = false;
            worldBounds = default;
            for (int i = 0; i < publications.Count; i++)
            {
                if (publications[i].occupied)
                {
                    IncludeBounds(publications[i].bounds);
                }
            }
        }
    }
}
