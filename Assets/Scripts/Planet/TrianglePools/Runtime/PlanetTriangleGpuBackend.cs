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
        private const string VertexBufferPropertyName = "_PlanetTriangleVertices";
        private const string SurfaceAtlasTexturePropertyName = "_PlanetSurfaceAtlas";
        private const string UseSurfaceAtlasPropertyName = "_UsePlanetSurfaceAtlas";
        private const string BaseMapTexturePropertyName = "_BaseMap";
        private const string MainTexturePropertyName = "_MainTex";

        private struct Publication
        {
            public uint meshId;
            public int vertexStart;
            public int vertexCount;
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
        private PlanetTriangleGpuVertex[] uploadScratch;
        private PlanetTriangleGpuVertex[] clearScratch;
        private Material material;
        private Material sourceMaterial;
        private GameObject renderObject;
        private PlanetTriangleGpuRenderer renderer;
        private Bounds worldBounds;
        private int vertexCapacity;
        private int highWatermarkVertexCount;
        private int activeVertexCount;
        private bool hasBounds;

        public PlanetTriangleGpuBackend(uint artistId)
        {
            this.artistId = artistId;
        }

        public int VertexCapacity => vertexCapacity;
        public int ActiveVertexCount => activeVertexCount;
        public int HighWatermarkVertexCount => highWatermarkVertexCount;
        public long EstimatedGpuBytes => vertexCapacity * (long)PlanetTriangleGpuVertex.Stride;
        public bool HasVisibleData => vertexBuffer != null && highWatermarkVertexCount > 0 && activeVertexCount > 0;

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
            if (vertices == null)
            {
                return false;
            }

            vertexCount = Mathf.Max(0, Mathf.Min(vertexCount, vertices.Count));
            vertexCount -= vertexCount % 3;
            ReleasePublication(meshId);
            if (vertexCount <= 0)
            {
                return true;
            }

            EnsureInitialized(PlanetTrianglePoolRegistry.GetOrCreate(artistId).TotalTriangleBudget);
            SetMaterial(source);
            if (vertexBuffer == null)
            {
                return false;
            }

            if (!TryAllocateVertexRange(vertexCount, out int vertexStart))
            {
                Debug.LogWarning(
                    "[09 GPU] Not enough GPU triangle buffer space. artistId=" + artistId +
                    " requestedVertices=" + vertexCount +
                    " capacity=" + vertexCapacity +
                    " highWatermark=" + highWatermarkVertexCount);
                return false;
            }

            EnsureUploadScratch(vertexCount);
            for (int i = 0; i < vertexCount; i++)
            {
                uploadScratch[i] = vertices[i];
            }

            vertexBuffer.SetData(uploadScratch, 0, vertexStart, vertexCount);
            AddPublication(meshId, vertexStart, vertexCount, bounds);
            activeVertexCount += vertexCount;
            highWatermarkVertexCount = Mathf.Max(highWatermarkVertexCount, vertexStart + vertexCount);
            IncludeBounds(bounds);
            UpdateRenderer();
            return true;
        }

        public int ReleasePublication(uint meshId)
        {
            int released = 0;
            for (int i = 0; i < publications.Count; i++)
            {
                Publication publication = publications[i];
                if (!publication.occupied || publication.meshId != meshId)
                {
                    continue;
                }

                ClearRange(publication.vertexStart, publication.vertexCount);
                AddFreeRange(publication.vertexStart, publication.vertexCount);
                activeVertexCount = Mathf.Max(0, activeVertexCount - publication.vertexCount);
                publication.occupied = false;
                publications[i] = publication;
                released += publication.vertexCount / 3;
            }

            RebuildBounds();
            UpdateRenderer();
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

            if (renderObject != null)
            {
                UnityEngine.Object.Destroy(renderObject);
                renderObject = null;
                renderer = null;
            }

            vertexCapacity = 0;
            sourceMaterial = null;
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
        }

        private bool TryAllocateVertexRange(int vertexCount, out int vertexStart)
        {
            for (int i = 0; i < freeRanges.Count; i++)
            {
                FreeRange range = freeRanges[i];
                if (range.count < vertexCount)
                {
                    continue;
                }

                vertexStart = range.start;
                range.start += vertexCount;
                range.count -= vertexCount;
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

            if (highWatermarkVertexCount + vertexCount > vertexCapacity)
            {
                vertexStart = 0;
                return false;
            }

            vertexStart = highWatermarkVertexCount;
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
                bounds = bounds,
                occupied = true
            });
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
