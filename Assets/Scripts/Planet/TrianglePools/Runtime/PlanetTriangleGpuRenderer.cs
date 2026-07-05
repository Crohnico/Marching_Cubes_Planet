using UnityEngine;
using UnityEngine.Rendering;

namespace MarchingCubesPlanet.TrianglePools
{
    public sealed class PlanetTriangleGpuRenderer : MonoBehaviour
    {
        private MaterialPropertyBlock properties;
        private GraphicsBuffer vertexBuffer;
        private Material material;
        private Bounds worldBounds;
        private string vertexBufferPropertyName;
        private int vertexCount;

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

        private void Update()
        {
            if (vertexBuffer == null || material == null || vertexCount <= 0)
            {
                return;
            }

            if (properties == null)
            {
                properties = new MaterialPropertyBlock();
            }

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
    }
}
