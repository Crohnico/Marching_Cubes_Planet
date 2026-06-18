using MarchingCubesPlanet.MarchingCubes;
using MarchingCubesPlanet.Noise;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MarchingCubesPlanet.Generators
{
    [DisallowMultipleComponent]
    public sealed class MarchingCubesSphereGenerator : MonoBehaviour
    {
        private const string GeneratedMeshName = "Generated Marching Cubes Sphere";
        private const string GeneratedOceanMeshName = "Generated Ocean Sphere";
        private const string GeneratedRootName = "Generated Cubes";
        private const float IsoLevel = 0f;

        [Header("Sphere")]
        [SerializeField, Min(0.01f)] private float radius = 2f;
        [SerializeField, Min(2)] private int cubesPerAxis = 4;

        [Header("Noise Profile")]
        [SerializeField] private int noiseSeed = 12345;
        [FormerlySerializedAs("perlinProfile")]
        [SerializeField] private NoiseProfile noiseProfile;

        [Header("Continents")]
        [SerializeField] private bool useContinents = true;
        [SerializeField, Min(1)] private int voronoiCellCount = 18;
        [SerializeField, Min(0)] private int landCellCount = 7;
        [SerializeField, Range(0f, 1f)] private float landElevation = 0.06f;
        [SerializeField, Range(0f, 1f)] private float oceanDepth = 0.16f;
        [SerializeField, Range(0f, 1f)] private float minimumOceanDepth = 0.03f;
        [SerializeField, Range(0f, 1f)] private float continentEdgeBlend = 0.16f;
        [SerializeField] private AnimationCurve continentBlendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Rendering")]
        [FormerlySerializedAs("material")]
        [SerializeField] private Material terrainMaterial;
        [SerializeField, Range(0.05f, 0.95f)] private float seaLevelAtlasV = 0.5f;
        [SerializeField] private bool generateOcean = true;
        [SerializeField] private Material oceanMaterial;

        [Header("Debug")]
        [SerializeField] private bool drawGridGizmos = true;
        [SerializeField] private bool drawSphereGizmo = true;
        [SerializeField, TextArea(3, 8)] private string lastGenerationReport;

        [SerializeField, HideInInspector] private Transform generatedRoot;

        public int CubesPerAxis => cubesPerAxis;
        public float CubeSize => (GridRadius * 2f) / cubesPerAxis;
        public float Radius => radius;
        public bool HasNoiseProfile => noiseProfile != null;
        public bool UsesSurfaceNoise => UsesProfileNoise;
        public bool UsesVoronoiContinents => UsesContinents;
        public string LastGenerationReport => lastGenerationReport;

        private bool UsesProfileNoise => noiseProfile != null && noiseProfile.HasRadiusNoise;
        private bool UsesContinents => useContinents && voronoiCellCount > 0 && (landElevation > 0f || oceanDepth > 0f);
        private float MaxProfileRadiusOffset => UsesProfileNoise ? noiseProfile.GetMaxRadiusOffset(radius) : 0f;
        private float MaxContinentRadiusOffset => UsesContinents && landCellCount > 0 ? radius * Mathf.Max(0f, landElevation) : 0f;
        private float MinHeightAtlasOffset => -radius * Mathf.Max(oceanDepth, minimumOceanDepth) - MaxProfileRadiusOffset;
        private float MaxHeightAtlasOffset => MaxContinentRadiusOffset + MaxProfileRadiusOffset;
        private float GridRadius => radius + MaxProfileRadiusOffset + MaxContinentRadiusOffset;
        private PerlinNoise3D activeNoise;
        private SphericalVoronoiContinents activeContinents;
        private float minRadiusOffset;
        private float maxRadiusOffset;
        private float minFieldValue;
        private float maxFieldValue;
        private int generatedCubeCount;
        private int generatedMeshCount;
        private int generatedTriangleCount;
        private int generatedOceanMeshCount;
        private int generatedOceanTriangleCount;

        private void OnValidate()
        {
            radius = Mathf.Max(0.01f, radius);
            cubesPerAxis = Mathf.Max(2, cubesPerAxis);
            voronoiCellCount = Mathf.Max(1, voronoiCellCount);
            landCellCount = Mathf.Clamp(landCellCount, 0, voronoiCellCount);
            landElevation = Mathf.Max(0f, landElevation);
            oceanDepth = Mathf.Max(0f, oceanDepth);
            minimumOceanDepth = Mathf.Max(0f, minimumOceanDepth);
            continentEdgeBlend = Mathf.Max(0f, continentEdgeBlend);
            if (continentBlendCurve == null)
            {
                continentBlendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            }
        }

        [ContextMenu("Generate Sphere")]
        public void GenerateSphere()
        {
            ClearGeneratedMesh();
            activeNoise = UsesProfileNoise ? new PerlinNoise3D(noiseSeed) : null;
            activeContinents = UsesContinents ? new SphericalVoronoiContinents(noiseSeed, voronoiCellCount, landCellCount) : null;
            ResetGenerationStats();
            Transform root = EnsureGeneratedRoot();

            for (int z = 0; z < cubesPerAxis; z++)
            {
                for (int y = 0; y < cubesPerAxis; y++)
                {
                    for (int x = 0; x < cubesPerAxis; x++)
                    {
                        CreateCubeObject(root, x, y, z);
                    }
                }
            }

            WriteGenerationReport();
            Debug.Log(lastGenerationReport, this);
        }

        [ContextMenu("Clear Generated Mesh")]
        public void ClearGeneratedMesh()
        {
            if (TryGetComponent(out MeshFilter ownMeshFilter))
            {
                DestroyGeneratedMesh(ownMeshFilter.sharedMesh);
                ownMeshFilter.sharedMesh = null;
            }

            Transform root = generatedRoot != null ? generatedRoot : transform.Find(GeneratedRootName);
            if (root == null)
            {
                generatedRoot = null;
                return;
            }

#if UNITY_EDITOR
            ClearGeneratedSelection(root);
#endif
            DestroyGeneratedMeshesIn(root);

            if (Application.isPlaying)
            {
                Destroy(root.gameObject);
            }
            else
            {
                DestroyImmediate(root.gameObject);
            }

            generatedRoot = null;
        }

        private void CreateCubeObject(Transform root, int x, int y, int z)
        {
            Vector3 center = GridCubeCenterToLocalPosition(x, y, z);
            GameObject cubeObject = new GameObject(BuildCubeName(x, y, z));
            cubeObject.transform.SetParent(root, false);
            cubeObject.transform.localPosition = center;
            generatedCubeCount++;

            CreateTerrainMesh(cubeObject.transform, center, x, y, z);
            CreateOceanMesh(cubeObject.transform, center, x, y, z);
        }

        private void CreateTerrainMesh(Transform cubeTransform, Vector3 center, int x, int y, int z)
        {
            MarchingCubesMeshData meshData = new MarchingCubesMeshData();
            MarchingCube cube = BuildTerrainCube(center);
            MarchingCubesPolygonizer.Polygonize(cube, IsoLevel, meshData);

            if (meshData.Triangles.Count == 0)
            {
                return;
            }

            meshData.ApplyUVs(vertex => HeightToAtlasUV(center + vertex));

            generatedMeshCount++;
            generatedTriangleCount += meshData.Triangles.Count / 3;

            GameObject meshObject = new GameObject("Terrain");
            meshObject.transform.SetParent(cubeTransform, false);

            MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = meshObject.AddComponent<MeshRenderer>();

            meshFilter.sharedMesh = meshData.ToMesh($"{GeneratedMeshName} {BuildCoordinateName(x, y, z)}");
            if (terrainMaterial != null)
            {
                meshRenderer.sharedMaterial = terrainMaterial;
            }
        }

        private void CreateOceanMesh(Transform cubeTransform, Vector3 center, int x, int y, int z)
        {
            if (!generateOcean)
            {
                return;
            }

            MarchingCubesMeshData meshData = new MarchingCubesMeshData();
            MarchingCube cube = BuildOceanCube(center);
            MarchingCubesPolygonizer.Polygonize(cube, IsoLevel, meshData);

            if (meshData.Triangles.Count == 0)
            {
                return;
            }

            generatedOceanMeshCount++;
            generatedOceanTriangleCount += meshData.Triangles.Count / 3;

            GameObject meshObject = new GameObject("Ocean");
            meshObject.transform.SetParent(cubeTransform, false);

            MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = meshObject.AddComponent<MeshRenderer>();

            meshFilter.sharedMesh = meshData.ToMesh($"{GeneratedOceanMeshName} {BuildCoordinateName(x, y, z)}");
            if (oceanMaterial != null)
            {
                meshRenderer.sharedMaterial = oceanMaterial;
            }
        }

        private MarchingCube BuildTerrainCube(Vector3 cubeCenter)
        {
            return BuildCube(cubeCenter, SampleSphereField);
        }

        private MarchingCube BuildOceanCube(Vector3 cubeCenter)
        {
            return BuildCube(cubeCenter, SampleOceanField);
        }

        private MarchingCube BuildCube(Vector3 cubeCenter, System.Func<Vector3, float> sampleField)
        {
            float halfSize = CubeSize * 0.5f;
            return new MarchingCube(
                BuildCorner(cubeCenter, new Vector3(-halfSize, -halfSize, -halfSize), sampleField),
                BuildCorner(cubeCenter, new Vector3(halfSize, -halfSize, -halfSize), sampleField),
                BuildCorner(cubeCenter, new Vector3(halfSize, halfSize, -halfSize), sampleField),
                BuildCorner(cubeCenter, new Vector3(-halfSize, halfSize, -halfSize), sampleField),
                BuildCorner(cubeCenter, new Vector3(-halfSize, -halfSize, halfSize), sampleField),
                BuildCorner(cubeCenter, new Vector3(halfSize, -halfSize, halfSize), sampleField),
                BuildCorner(cubeCenter, new Vector3(halfSize, halfSize, halfSize), sampleField),
                BuildCorner(cubeCenter, new Vector3(-halfSize, halfSize, halfSize), sampleField));
        }

        private MarchingCubeCorner BuildCorner(Vector3 cubeCenter, Vector3 meshLocalPosition, System.Func<Vector3, float> sampleField)
        {
            Vector3 sphereLocalPosition = cubeCenter + meshLocalPosition;
            return new MarchingCubeCorner(meshLocalPosition, sampleField.Invoke(sphereLocalPosition));
        }

        private Vector3 GridCubeCenterToLocalPosition(int x, int y, int z)
        {
            float cubeSize = CubeSize;
            float gridSize = GridRadius * 2f;
            Vector3 min = Vector3.one * (-gridSize * 0.5f);
            return min + new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * cubeSize;
        }

        private float SampleSphereField(Vector3 localPosition)
        {
            float radiusOffset = GetRadiusOffset(localPosition);
            float value = localPosition.magnitude - (radius + radiusOffset);
            minFieldValue = Mathf.Min(minFieldValue, value);
            maxFieldValue = Mathf.Max(maxFieldValue, value);
            return value;
        }

        private float SampleOceanField(Vector3 localPosition)
        {
            return localPosition.magnitude - radius;
        }

        private Vector2 HeightToAtlasUV(Vector3 sphereLocalPosition)
        {
            float heightOffset = sphereLocalPosition.magnitude - radius;
            if (heightOffset <= 0f)
            {
                float depthRange = Mathf.Max(0.0001f, -MinHeightAtlasOffset);
                float underwaterHeight = Mathf.Clamp01((heightOffset - MinHeightAtlasOffset) / depthRange);
                return new Vector2(0.5f, Mathf.Lerp(0f, seaLevelAtlasV, underwaterHeight));
            }

            float landRange = Mathf.Max(0.0001f, MaxHeightAtlasOffset);
            float landHeight = Mathf.Clamp01(heightOffset / landRange);
            return new Vector2(0.5f, Mathf.Lerp(seaLevelAtlasV, 1f, landHeight));
        }

        private float GetRadiusOffset(Vector3 localPosition)
        {
            float offset = 0f;
            bool isOceanCell = false;

            if (activeContinents != null)
            {
                ContinentSample continentSample = activeContinents.Sample(
                    localPosition,
                    radius,
                    landElevation,
                    oceanDepth,
                    continentEdgeBlend,
                    continentBlendCurve);

                offset += continentSample.RadiusOffset;
                isOceanCell = continentSample.IsOceanCell;
            }

            if (UsesProfileNoise)
            {
                offset += noiseProfile.SampleRadiusOffset(activeNoise, localPosition, radius);
            }

            if (isOceanCell && minimumOceanDepth > 0f)
            {
                offset = Mathf.Min(offset, -radius * minimumOceanDepth);
            }

            minRadiusOffset = Mathf.Min(minRadiusOffset, offset);
            maxRadiusOffset = Mathf.Max(maxRadiusOffset, offset);
            return offset;
        }

        private void ResetGenerationStats()
        {
            minRadiusOffset = float.PositiveInfinity;
            maxRadiusOffset = float.NegativeInfinity;
            minFieldValue = float.PositiveInfinity;
            maxFieldValue = float.NegativeInfinity;
            generatedCubeCount = 0;
            generatedMeshCount = 0;
            generatedTriangleCount = 0;
            generatedOceanMeshCount = 0;
            generatedOceanTriangleCount = 0;
        }

        private void WriteGenerationReport()
        {
            if (float.IsInfinity(minRadiusOffset))
            {
                minRadiusOffset = 0f;
                maxRadiusOffset = 0f;
            }

            if (float.IsInfinity(minFieldValue))
            {
                minFieldValue = 0f;
                maxFieldValue = 0f;
            }

            string profileName = noiseProfile != null ? noiseProfile.name : "None";
            lastGenerationReport =
                $"Profile: {profileName}\n" +
                $"Surface Noise Enabled: {UsesProfileNoise}\n" +
                $"Continents Enabled: {UsesContinents}\n" +
                $"Voronoi Cells/Land Cells: {voronoiCellCount} / {landCellCount}\n" +
                $"Seed: {noiseSeed}\n" +
                $"Max Profile Offset: {MaxProfileRadiusOffset:0.0000}\n" +
                $"Max Continent Offset: {MaxContinentRadiusOffset:0.0000}\n" +
                $"Radius Offset Min/Max: {minRadiusOffset:0.0000} / {maxRadiusOffset:0.0000}\n" +
                $"Field Min/Max: {minFieldValue:0.0000} / {maxFieldValue:0.0000}\n" +
                $"Terrain Cubes/Meshes/Triangles: {generatedCubeCount} / {generatedMeshCount} / {generatedTriangleCount}\n" +
                $"Ocean Meshes/Triangles: {generatedOceanMeshCount} / {generatedOceanTriangleCount}";
        }

        private Transform EnsureGeneratedRoot()
        {
            if (generatedRoot != null)
            {
                return generatedRoot;
            }

            Transform existing = transform.Find(GeneratedRootName);
            if (existing != null)
            {
                generatedRoot = existing;
                return generatedRoot;
            }

            GameObject rootObject = new GameObject(GeneratedRootName);
            rootObject.transform.SetParent(transform, false);
            generatedRoot = rootObject.transform;
            return generatedRoot;
        }

        private void DestroyGeneratedMeshesIn(Transform root)
        {
            MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < meshFilters.Length; i++)
            {
                DestroyGeneratedMesh(meshFilters[i].sharedMesh);
                meshFilters[i].sharedMesh = null;
            }
        }

        private string BuildCubeName(int x, int y, int z)
        {
            return $"Cube_{BuildCoordinateName(x, y, z)}";
        }

        private string BuildCoordinateName(int x, int y, int z)
        {
            return $"{FormatCoordinate(GetCenteredCoordinate(x))}_{FormatCoordinate(GetCenteredCoordinate(y))}_{FormatCoordinate(GetCenteredCoordinate(z))}";
        }

        private float GetCenteredCoordinate(int index)
        {
            return index - (cubesPerAxis - 1) * 0.5f;
        }

        private static string FormatCoordinate(float value)
        {
            return Mathf.Approximately(value, Mathf.Round(value))
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.0");
        }

        private static void DestroyGeneratedMesh(Mesh mesh)
        {
            if (mesh == null || (!mesh.name.StartsWith(GeneratedMeshName) && !mesh.name.StartsWith(GeneratedOceanMeshName)))
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(mesh);
                return;
            }

            DestroyImmediate(mesh);
        }

#if UNITY_EDITOR
        private void ClearGeneratedSelection(Transform root)
        {
            if (Application.isPlaying || root == null)
            {
                return;
            }

            Object[] selectedObjects = Selection.objects;
            for (int i = 0; i < selectedObjects.Length; i++)
            {
                if (IsGeneratedSelection(selectedObjects[i], root))
                {
                    Selection.objects = new Object[] { gameObject };
                    return;
                }
            }
        }

        private static bool IsGeneratedSelection(Object selectedObject, Transform root)
        {
            if (selectedObject is GameObject selectedGameObject)
            {
                return IsGeneratedTransform(selectedGameObject.transform, root);
            }

            if (selectedObject is Component selectedComponent)
            {
                return IsGeneratedTransform(selectedComponent.transform, root);
            }

            return false;
        }

        private static bool IsGeneratedTransform(Transform selectedTransform, Transform root)
        {
            return selectedTransform != null && (selectedTransform == root || selectedTransform.IsChildOf(root));
        }
#endif

        private void OnDrawGizmosSelected()
        {
            if (drawGridGizmos)
            {
                DrawGridGizmo();
            }

            if (drawSphereGizmo)
            {
                Gizmos.color = new Color(0.2f, 0.65f, 1f, 0.35f);
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireSphere(Vector3.zero, radius);
                if (UsesProfileNoise || UsesContinents)
                {
                    Gizmos.color = new Color(1f, 0.72f, 0.18f, 0.25f);
                    Gizmos.DrawWireSphere(Vector3.zero, GridRadius);
                }

                Gizmos.matrix = Matrix4x4.identity;
            }
        }

        private void DrawGridGizmo()
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.18f);
            Gizmos.matrix = transform.localToWorldMatrix;

            float cubeSize = CubeSize;
            float gridSize = GridRadius * 2f;
            Vector3 min = Vector3.one * (-gridSize * 0.5f);

            for (int i = 0; i <= cubesPerAxis; i++)
            {
                float offset = i * cubeSize;

                for (int j = 0; j <= cubesPerAxis; j++)
                {
                    float lineOffset = j * cubeSize;
                    Gizmos.DrawLine(min + new Vector3(offset, lineOffset, 0f), min + new Vector3(offset, lineOffset, gridSize));
                    Gizmos.DrawLine(min + new Vector3(offset, 0f, lineOffset), min + new Vector3(offset, gridSize, lineOffset));
                    Gizmos.DrawLine(min + new Vector3(0f, offset, lineOffset), min + new Vector3(gridSize, offset, lineOffset));
                }
            }

            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
