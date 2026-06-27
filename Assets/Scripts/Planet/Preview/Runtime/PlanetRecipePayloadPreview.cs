using System;
using MarchingCubesPlanet.Coordinates;
using UnityEngine;

namespace MarchingCubesPlanet.Preview
{
    [DisallowMultipleComponent]
    public sealed class PlanetRecipePayloadPreview : MonoBehaviour
    {
        private const string VertexColorShaderName = "MarchingCubesPlanet/Debug/Vertex Color";
        private const string UrpUnlitShaderName = "Universal Render Pipeline/Unlit";

        [Header("Recipe")]
        [SerializeField] private PlanetRecipe recipe = PlanetRecipe.Default();
        [SerializeField] private PlanetPlacement placement = PlanetPlacement.Default();

        [Header("Payload")]
        [SerializeField] private int faceResolution = 103;
        [SerializeField] private bool generateVertexColors = true;
        [SerializeField] private Material materialOverride;

        [Header("Runtime State")]
        [SerializeField, HideInInspector] private int derivedTriangleCount;
        [SerializeField, HideInInspector] private int derivedVertexCount;
        [SerializeField, HideInInspector] private int derivedIndexCount;
        [SerializeField, HideInInspector] private float derivedWorldRadius;
        [SerializeField, HideInInspector] private bool hasLiveMesh;
        [SerializeField, TextArea] private string lastDiagnostic;

        private Mesh runtimeMesh;
        private Material runtimeMaterial;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;

        public PlanetRecipe Recipe => recipe;
        public PlanetPlacement Placement => placement;
        public int FaceResolution => faceResolution;
        public int DerivedTriangleCount => derivedTriangleCount;
        public int DerivedVertexCount => derivedVertexCount;
        public int DerivedIndexCount => derivedIndexCount;
        public float DerivedWorldRadius => derivedWorldRadius;
        public Vector3 TransformPlanetWorldCenter => transform.position;
        public bool HasLiveMesh => hasLiveMesh;
        public string LastDiagnostic => lastDiagnostic;

        private void OnValidate()
        {
            faceResolution = Mathf.Max(1, faceResolution);
            RefreshDerivedValues();
        }

        private void Start()
        {
            SyncPlacementFromTransform();
            RefreshDerivedValues();
        }

        private void OnDestroy()
        {
            Release();
        }

        public void ApplyPayload126k()
        {
            SetFaceResolution(103);
        }

        public void ApplyPayload250k()
        {
            SetFaceResolution(144);
        }

        public void ApplyPayload500k()
        {
            SetFaceResolution(205);
        }

        public void ApplyPayload1M()
        {
            SetFaceResolution(289);
        }

        public void ResetDemoRecipe()
        {
            recipe = PlanetRecipe.Default();
            SyncPlacementFromTransform();
            SetFaceResolution(103);
            lastDiagnostic = "Demo recipe reset. PlanetPlacement was synced from Transform.position.";
        }

        public void SetFaceResolution(int value)
        {
            faceResolution = Mathf.Max(1, value);
            RefreshDerivedValues();
        }

        public void Generate()
        {
            SyncPlacementFromTransform();
            RefreshDerivedValues();

            if (!PlanetRecipeValidator.Validate(in recipe, out string recipeMessage))
            {
                lastDiagnostic = "Generate blocked: " + recipeMessage;
                return;
            }

            try
            {
                Release();
                EnsureRendererComponents();

                runtimeMesh = new Mesh();
                PlanetSpherePayloadBuildResult result = PlanetSpherePayloadMeshBuilder.Build(
                    runtimeMesh,
                    in recipe,
                    faceResolution,
                    generateVertexColors);

                meshFilter.sharedMesh = runtimeMesh;
                meshRenderer.sharedMaterial = ResolveMaterial();

                transform.localScale = Vector3.one;

                derivedVertexCount = result.VertexCount;
                derivedTriangleCount = result.TriangleCount;
                derivedIndexCount = result.IndexCount;
                hasLiveMesh = true;
                lastDiagnostic = "Generated payload sphere: faceResolution=" + faceResolution +
                                 ", triangles=" + derivedTriangleCount +
                                 ", vertices=" + derivedVertexCount +
                                 ", WorldRadius=" + derivedWorldRadius +
                                 ", center=" + placement.PlanetWorldCenter + ".";
            }
            catch (Exception exception)
            {
                Release();
                lastDiagnostic = "Generate failed: " + exception.Message;
                throw;
            }
        }

        public void Release()
        {
            CacheRendererComponents();

            if (meshFilter != null && meshFilter.sharedMesh == runtimeMesh)
            {
                meshFilter.sharedMesh = null;
            }

            DestroyRuntimeObject(runtimeMesh);
            runtimeMesh = null;

            if (runtimeMaterial != null)
            {
                if (meshRenderer != null && meshRenderer.sharedMaterial == runtimeMaterial)
                {
                    meshRenderer.sharedMaterial = null;
                }

                DestroyRuntimeObject(runtimeMaterial);
                runtimeMaterial = null;
            }

            hasLiveMesh = false;
            RefreshDerivedValues();
            lastDiagnostic = "Payload preview released.";
        }

        private void RefreshDerivedValues()
        {
            SyncPlacementFromTransform();
            derivedWorldRadius = recipe.WorldRadius;
            derivedTriangleCount = PlanetSpherePayloadMeshBuilder.CalculateTriangleCount(faceResolution);
            derivedVertexCount = PlanetSpherePayloadMeshBuilder.CalculateVertexCount(faceResolution);
            derivedIndexCount = PlanetSpherePayloadMeshBuilder.CalculateIndexCount(faceResolution);
        }

        private void SyncPlacementFromTransform()
        {
            placement.PlanetWorldCenter = transform.position;
        }

        private Material ResolveMaterial()
        {
            if (materialOverride != null)
            {
                return materialOverride;
            }

            Shader shader = Shader.Find(VertexColorShaderName);
            if (shader == null)
            {
                shader = Shader.Find(UrpUnlitShaderName);
            }

            if (shader == null)
            {
                throw new InvalidOperationException("No debug material shader was found for PlanetRecipePayloadPreview.");
            }

            runtimeMaterial = new Material(shader)
            {
                name = "PlanetRecipePayloadPreview_Material_Runtime"
            };

            if (shader.name == UrpUnlitShaderName)
            {
                runtimeMaterial.color = Color.white;
            }

            return runtimeMaterial;
        }

        private void EnsureRendererComponents()
        {
            CacheRendererComponents();

            if (meshFilter == null)
            {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            if (meshRenderer == null)
            {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }
        }

        private void CacheRendererComponents()
        {
            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
            }

            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
            }
        }

        private static void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
