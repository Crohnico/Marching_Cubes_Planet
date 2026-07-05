using UnityEngine;
using UnityEngine.Rendering;

namespace MarchingCubesPlanet.TrianglePools
{
    public sealed class PlanetTriangleGpuRenderer : MonoBehaviour
    {
        private MaterialPropertyBlock properties;
        private GraphicsBuffer vertexBuffer;
        private GraphicsBuffer waterVertexBuffer;
        private Material material;
        private Material waterMaterial;
        private Bounds worldBounds;
        private Bounds waterWorldBounds;
        private string vertexBufferPropertyName;
        private string waterVertexBufferPropertyName;
        private int vertexCount;
        private int waterVertexCount;

        public void Configure(
            GraphicsBuffer buffer,
            Material renderMaterial,
            int renderVertexCount,
            Bounds bounds,
            string bufferPropertyName)
        {
            vertexBuffer = buffer;
            material = renderMaterial;
            vertexCount = Mathf.Max(0, renderVertexCount);
            worldBounds = bounds;
            vertexBufferPropertyName = bufferPropertyName;
        }

        public void ConfigureWater(
            GraphicsBuffer buffer,
            Material renderMaterial,
            int renderVertexCount,
            Bounds bounds,
            string bufferPropertyName)
        {
            waterVertexBuffer = buffer;
            waterMaterial = renderMaterial;
            waterVertexCount = Mathf.Max(0, renderVertexCount);
            waterWorldBounds = bounds;
            waterVertexBufferPropertyName = bufferPropertyName;
        }

        private void Update()
        {
            if ((vertexBuffer == null || material == null || vertexCount <= 0) &&
                (waterVertexBuffer == null || waterMaterial == null || waterVertexCount <= 0))
            {
                return;
            }

            if (properties == null)
            {
                properties = new MaterialPropertyBlock();
            }

            if (vertexBuffer != null && material != null && vertexCount > 0)
            {
                properties.Clear();
                properties.SetBuffer(vertexBufferPropertyName, vertexBuffer);
                RenderParams renderParams = new RenderParams(material)
                {
                    worldBounds = worldBounds,
                    matProps = properties,
                    shadowCastingMode = ShadowCastingMode.On,
                    receiveShadows = true,
                    layer = gameObject.layer
                };
                Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, vertexCount);
            }

            if (waterVertexBuffer != null && waterMaterial != null && waterVertexCount > 0)
            {
                properties.Clear();
                properties.SetBuffer(waterVertexBufferPropertyName, waterVertexBuffer);
                RenderParams waterRenderParams = new RenderParams(waterMaterial)
                {
                    worldBounds = waterWorldBounds,
                    matProps = properties,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = true,
                    layer = gameObject.layer
                };
                Graphics.RenderPrimitives(waterRenderParams, MeshTopology.Triangles, waterVertexCount);
            }
        }
    }
}
