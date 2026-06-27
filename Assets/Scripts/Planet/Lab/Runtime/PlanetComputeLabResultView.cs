using UnityEngine;

namespace MarchingCubesPlanet.Lab
{
    public sealed class PlanetComputeLabResultView : MonoBehaviour
    {
        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private MeshRenderer meshRenderer;

        public MeshFilter MeshFilter => meshFilter;
        public MeshRenderer MeshRenderer => meshRenderer;

        public void Configure(Mesh mesh, Material material, RenderTexture outputTexture)
        {
            if (meshFilter == null)
            {
                meshFilter = gameObject.GetComponent<MeshFilter>();
                if (meshFilter == null)
                {
                    meshFilter = gameObject.AddComponent<MeshFilter>();
                }
            }

            if (meshRenderer == null)
            {
                meshRenderer = gameObject.GetComponent<MeshRenderer>();
                if (meshRenderer == null)
                {
                    meshRenderer = gameObject.AddComponent<MeshRenderer>();
                }
            }

            meshFilter.sharedMesh = mesh;
            meshRenderer.sharedMaterial = material;

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", outputTexture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", outputTexture);
            }
        }
    }
}
