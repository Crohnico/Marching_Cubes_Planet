using MarchingCubesPlanet.MarchingCubes;
using MarchingCubesPlanet.Noise;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MarchingCubesPlanet.Generators
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class MarchingCubesSphereGenerator : MonoBehaviour
    {
        public enum PlanetLod
        {
            LOD5 = 5,
            LOD6 = 6,
            LOD7 = 7,
            LOD8 = 8,
            LOD9 = 9,
            LOD10 = 10
        }

        private const string GeneratedMeshName = "Generated Marching Cubes Sphere";
        private const string GeneratedOceanMeshName = "Generated Ocean Sphere";
        private const string GeneratedRootName = "Generated Cubes";
        private const string DestroyingGeneratedRootName = "Generated Cubes (Destroying)";
        private const float IsoLevel = 0f;
        private const HideFlags GeneratedObjectHideFlags = HideFlags.NotEditable | HideFlags.DontSaveInEditor;
        private const HideFlags GeneratedMeshHideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

        [Header("Sphere")]
        [SerializeField, Min(0.01f)] private float radius = 2f;
        [SerializeField, Min(2)] private int cubesPerAxis = 4;
        [SerializeField] private bool generateOnAwake = true;

        [Header("LOD")]
        [SerializeField] private PlanetLod globalLod = PlanetLod.LOD5;

        [Header("Dynamic LOD Test")]
        [SerializeField] private bool useMainCameraChunkLodTest = true;
        [SerializeField] private bool autoRegenerateMainCameraChunkLodTest = true;
        [SerializeField, Min(0f)] private float mainCameraLod5Distance = 1500f;
        [SerializeField, Min(0.01f)] private float mainCameraLodRefreshInterval = 0.25f;
        [SerializeField, Min(0f)] private float mainCameraLodMoveThreshold = 25f;

        [Header("Noise Profile")]
        [SerializeField] private int noiseSeed = 12345;
        [FormerlySerializedAs("perlinProfile")]
        [SerializeField] private NoiseProfile noiseProfile;

        [Header("Continents")]
        [SerializeField] private bool useContinents = true;
        [SerializeField, Min(1)] private int voronoiCellCount = 18;
        [SerializeField, Min(0)] private int landCellCount = 7;
        [SerializeField, Range(0f, 1f)] private float landMinElevation = 0.015f;
        [FormerlySerializedAs("landElevation")]
        [SerializeField, Range(0f, 1f)] private float landMaxElevation = 0.09f;
        [SerializeField] private AnimationCurve landElevationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
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
        [SerializeField] private ShadowCastingMode generatedShadowCastingMode = ShadowCastingMode.Off;
        [SerializeField] private bool generatedReceiveShadows;

        [Header("Debug")]
        [SerializeField] private bool drawGridGizmos = true;
        [SerializeField] private bool drawSphereGizmo = true;
        [SerializeField] private bool logGenerationReport;
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
        private bool UsesContinents => useContinents && voronoiCellCount > 0 && (landMaxElevation > 0f || oceanDepth > 0f);
        private float MaxProfileRadiusOffset => UsesProfileNoise ? noiseProfile.GetMaxRadiusOffset(radius) : 0f;
        private float MaxContinentRadiusOffset => UsesContinents && landCellCount > 0 ? radius * Mathf.Max(0f, landMaxElevation) : 0f;
        private float MinHeightAtlasOffset => -radius * Mathf.Max(oceanDepth, minimumOceanDepth) - MaxProfileRadiusOffset;
        private float MaxHeightAtlasOffset => MaxContinentRadiusOffset + MaxProfileRadiusOffset;
        private float GridRadius => radius + MaxProfileRadiusOffset + MaxContinentRadiusOffset;
        private PerlinNoise3D activeNoise;
        private SphericalVoronoiContinents activeContinents;
        private float minRadiusOffset;
        private float maxRadiusOffset;
        private float minFieldValue;
        private float maxFieldValue;
        private int generatedChunkCount;
        private int generatedCubeCount;
        private int generatedMeshCount;
        private int generatedTriangleCount;
        private int generatedTerrainTriangleReduction;
        private int generatedOceanMeshCount;
        private int generatedOceanTriangleCount;
        private int generatedOceanTriangleReduction;
        private int generatedCameraLod5ChunkCount;
        private int generatedCameraLod6ChunkCount;
        private string generatedLodMode;
        private bool isGenerating;
        private bool hasCameraLodSnapshot;
        private Vector3 lastCameraLodPosition;
        private float lastCameraLod5Distance;
        private float nextCameraLodRefreshTime;

        private void Awake()
        {
            if (generateOnAwake && Application.isPlaying)
            {
                GenerateSphere();
            }
        }

        private void Update()
        {
            if (!ShouldAutoRegenerateCameraLodTest())
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (now < nextCameraLodRefreshTime)
            {
                return;
            }

            nextCameraLodRefreshTime = now + mainCameraLodRefreshInterval;
            Camera lodCamera = Camera.main;
            if (lodCamera == null)
            {
                hasCameraLodSnapshot = false;
                return;
            }

            if (!HasCameraLodTestChanged(lodCamera))
            {
                return;
            }

            RememberCameraLodTestSnapshot(lodCamera);
            GenerateSphere();
        }

        private bool ShouldAutoRegenerateCameraLodTest()
        {
            return useMainCameraChunkLodTest
                && autoRegenerateMainCameraChunkLodTest
                && !isGenerating
                && isActiveAndEnabled;
        }

        private bool HasCameraLodTestChanged(Camera lodCamera)
        {
            if (lodCamera == null)
            {
                return false;
            }

            if (!hasCameraLodSnapshot)
            {
                return true;
            }

            float moveThreshold = Mathf.Max(0f, mainCameraLodMoveThreshold);
            float sqrMoveThreshold = moveThreshold * moveThreshold;
            bool cameraMoved = (lodCamera.transform.position - lastCameraLodPosition).sqrMagnitude > sqrMoveThreshold;
            bool distanceChanged = !Mathf.Approximately(lastCameraLod5Distance, mainCameraLod5Distance);
            return cameraMoved || distanceChanged;
        }

        private void RememberCameraLodTestSnapshot(Camera lodCamera)
        {
            if (!useMainCameraChunkLodTest || lodCamera == null)
            {
                hasCameraLodSnapshot = false;
                return;
            }

            lastCameraLodPosition = lodCamera.transform.position;
            lastCameraLod5Distance = mainCameraLod5Distance;
            hasCameraLodSnapshot = true;
        }

        private void OnValidate()
        {
            radius = Mathf.Max(0.01f, radius);
            cubesPerAxis = Mathf.Max(2, cubesPerAxis);
            mainCameraLod5Distance = Mathf.Max(0f, mainCameraLod5Distance);
            mainCameraLodRefreshInterval = Mathf.Max(0.01f, mainCameraLodRefreshInterval);
            mainCameraLodMoveThreshold = Mathf.Max(0f, mainCameraLodMoveThreshold);
            voronoiCellCount = Mathf.Max(1, voronoiCellCount);
            landCellCount = Mathf.Clamp(landCellCount, 0, voronoiCellCount);
            landMinElevation = Mathf.Max(0f, landMinElevation);
            landMaxElevation = Mathf.Max(landMinElevation, landMaxElevation);
            if (landElevationCurve == null)
            {
                landElevationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            }

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
            if (isGenerating)
            {
                return;
            }

            isGenerating = true;
            try
            {
                ClearGeneratedMesh();
                activeNoise = UsesProfileNoise ? new PerlinNoise3D(noiseSeed) : null;
                activeContinents = UsesContinents
                    ? new SphericalVoronoiContinents(
                        noiseSeed,
                        voronoiCellCount,
                        landCellCount,
                        landMinElevation,
                        landMaxElevation,
                        landElevationCurve)
                    : null;
                ResetGenerationStats();
                Transform root = EnsureGeneratedRoot();
                if (!useMainCameraChunkLodTest && UsesRadialProxy(globalLod))
                {
                    CreateRadialProxyMeshes(root);
                }
                else
                {
                    CreateMarchingCubesMeshes(root);
                }

                RememberCameraLodTestSnapshot(Camera.main);
                WriteGenerationReport();
                if (logGenerationReport)
                {
                    Debug.Log(lastGenerationReport, this);
                }
            }
            finally
            {
                isGenerating = false;
            }
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
                root.gameObject.name = DestroyingGeneratedRootName;
                Destroy(root.gameObject);
            }
            else
            {
                DestroyImmediate(root.gameObject);
            }

            generatedRoot = null;
        }

        private void CreateMarchingCubesMeshes(Transform root)
        {
            Camera lodCamera = useMainCameraChunkLodTest ? Camera.main : null;
            bool useCameraLodTest = useMainCameraChunkLodTest && lodCamera != null;
            PlanetLod fallbackLod = useMainCameraChunkLodTest ? PlanetLod.LOD6 : globalLod;
            generatedLodMode = useCameraLodTest
                ? $"MainCamera Chunk Test ({lodCamera.name})"
                : useMainCameraChunkLodTest
                    ? "MainCamera Chunk Test (camera missing, fallback LOD6)"
                    : "Marching Cubes";

            MarchingCubesMeshData terrainMeshData = new MarchingCubesMeshData();
            MarchingCubesMeshData oceanMeshData = new MarchingCubesMeshData();
            int lodChunkSpan = useMainCameraChunkLodTest ? GetLodChunkSpan(PlanetLod.LOD6) : GetLodChunkSpan(globalLod);

            for (int z = 0; z < cubesPerAxis; z += lodChunkSpan)
            {
                for (int y = 0; y < cubesPerAxis; y += lodChunkSpan)
                {
                    for (int x = 0; x < cubesPerAxis; x += lodChunkSpan)
                    {
                        AppendMarchingCubesChunk(
                            terrainMeshData,
                            oceanMeshData,
                            x,
                            y,
                            z,
                            lodChunkSpan,
                            lodCamera,
                            fallbackLod);
                    }
                }
            }

            if (terrainMeshData.TriangleCount > 0)
            {
                Mesh terrainMesh = terrainMeshData.ToMesh(GeneratedMeshName);
                MarkGeneratedMesh(terrainMesh);
                generatedMeshCount++;
                generatedTriangleCount = terrainMeshData.TriangleCount;
                CreateMeshObject(root, "Terrain", terrainMesh, terrainMaterial);
            }

            if (oceanMeshData.TriangleCount > 0)
            {
                Mesh oceanMesh = oceanMeshData.ToMesh(GeneratedOceanMeshName);
                MarkGeneratedMesh(oceanMesh);
                generatedOceanMeshCount++;
                generatedOceanTriangleCount = oceanMeshData.TriangleCount;
                CreateMeshObject(root, "Ocean", oceanMesh, oceanMaterial);
            }
        }

        private void AppendMarchingCubesChunk(
            MarchingCubesMeshData terrainMeshData,
            MarchingCubesMeshData oceanMeshData,
            int startX,
            int startY,
            int startZ,
            int cellSpan,
            Camera lodCamera,
            PlanetLod fallbackLod)
        {
            int endX = Mathf.Min(startX + cellSpan, cubesPerAxis);
            int endY = Mathf.Min(startY + cellSpan, cubesPerAxis);
            int endZ = Mathf.Min(startZ + cellSpan, cubesPerAxis);
            Vector3 min = GridCellBoundaryToLocalPosition(startX, startY, startZ);
            Vector3 max = GridCellBoundaryToLocalPosition(endX, endY, endZ);
            Vector3 chunkCenter = (min + max) * 0.5f;
            Vector3 halfSize = (max - min) * 0.5f;
            PlanetLod chunkLod = ResolveChunkLod(lodCamera, chunkCenter, fallbackLod);

            generatedChunkCount++;
            generatedCubeCount += (endX - startX) * (endY - startY) * (endZ - startZ);

            AppendTerrainChunk(terrainMeshData, chunkCenter, halfSize, startX, endX, startY, endY, startZ, endZ, chunkLod);
            AppendOceanChunk(oceanMeshData, chunkCenter, halfSize, startX, endX, startY, endY, startZ, endZ, chunkLod);
        }

        private PlanetLod ResolveChunkLod(Camera lodCamera, Vector3 chunkCenter, PlanetLod fallbackLod)
        {
            if (!useMainCameraChunkLodTest)
            {
                return fallbackLod;
            }

            if (lodCamera == null)
            {
                generatedCameraLod6ChunkCount++;
                return PlanetLod.LOD6;
            }

            Vector3 worldChunkCenter = transform.TransformPoint(chunkCenter);
            float distance = Vector3.Distance(lodCamera.transform.position, worldChunkCenter);
            if (distance <= mainCameraLod5Distance)
            {
                generatedCameraLod5ChunkCount++;
                return PlanetLod.LOD5;
            }

            generatedCameraLod6ChunkCount++;
            return PlanetLod.LOD6;
        }

        private void AppendTerrainChunk(
            MarchingCubesMeshData aggregateMeshData,
            Vector3 chunkCenter,
            Vector3 chunkHalfSize,
            int startX,
            int endX,
            int startY,
            int endY,
            int startZ,
            int endZ,
            PlanetLod chunkLod)
        {
            MarchingCubesMeshData meshData = new MarchingCubesMeshData();
            PolygonizeChunkCells(meshData, chunkCenter, startX, endX, startY, endY, startZ, endZ, SampleSphereField);
            if (meshData.TriangleCount == 0)
            {
                return;
            }

            meshData.ApplyUVs(vertex => HeightToAtlasUV(chunkCenter + vertex));
            generatedTerrainTriangleReduction += ApplyLodSimplification(meshData, chunkHalfSize, true, chunkLod);
            aggregateMeshData.Append(meshData, chunkCenter);
        }

        private void AppendOceanChunk(
            MarchingCubesMeshData aggregateMeshData,
            Vector3 chunkCenter,
            Vector3 chunkHalfSize,
            int startX,
            int endX,
            int startY,
            int endY,
            int startZ,
            int endZ,
            PlanetLod chunkLod)
        {
            if (!generateOcean)
            {
                return;
            }

            MarchingCubesMeshData meshData = new MarchingCubesMeshData();
            PolygonizeChunkCells(meshData, chunkCenter, startX, endX, startY, endY, startZ, endZ, SampleOceanField);
            if (meshData.TriangleCount == 0)
            {
                return;
            }

            generatedOceanTriangleReduction += ApplyLodSimplification(meshData, chunkHalfSize, false, chunkLod);
            aggregateMeshData.Append(meshData, chunkCenter);
        }

        private void CreateMeshObject(Transform parent, string objectName, Mesh mesh, Material meshMaterial)
        {
            GameObject meshObject = new GameObject(objectName);
            MarkGeneratedObject(meshObject);
            meshObject.transform.SetParent(parent, false);

            MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter.sharedMesh = mesh;
            meshRenderer.shadowCastingMode = generatedShadowCastingMode;
            meshRenderer.receiveShadows = generatedReceiveShadows;
            if (meshMaterial != null)
            {
                meshRenderer.sharedMaterial = meshMaterial;
            }
        }

        private void CreateRadialProxyMeshes(Transform root)
        {
            int resolution = GetRadialProxyResolution(globalLod);
            generatedLodMode = $"Radial Proxy {resolution}x{resolution}";
            generatedChunkCount = 1;

            MarchingCubesMeshData terrainMeshData = new MarchingCubesMeshData();
            BuildRadialProxyMeshData(terrainMeshData, resolution, ProjectToTerrainSurface);
            terrainMeshData.ApplyUVs(HeightToAtlasUV);

            if (terrainMeshData.TriangleCount > 0)
            {
                Mesh terrainMesh = terrainMeshData.ToMesh(GeneratedMeshName);
                MarkGeneratedMesh(terrainMesh);
                generatedMeshCount++;
                generatedTriangleCount = terrainMeshData.TriangleCount;
                CreateMeshObject(root, "Terrain", terrainMesh, terrainMaterial);
            }

            if (!generateOcean)
            {
                return;
            }

            MarchingCubesMeshData oceanMeshData = new MarchingCubesMeshData();
            BuildRadialProxyMeshData(oceanMeshData, resolution, direction => direction * radius);
            if (oceanMeshData.TriangleCount == 0)
            {
                return;
            }

            Mesh oceanMesh = oceanMeshData.ToMesh(GeneratedOceanMeshName);
            MarkGeneratedMesh(oceanMesh);
            generatedOceanMeshCount++;
            generatedOceanTriangleCount = oceanMeshData.TriangleCount;
            CreateMeshObject(root, "Ocean", oceanMesh, oceanMaterial);
        }

        private void BuildRadialProxyMeshData(
            MarchingCubesMeshData meshData,
            int resolution,
            System.Func<Vector3, Vector3> projectDirection)
        {
            int safeResolution = Mathf.Max(1, resolution);
            BuildRadialProxyFace(meshData, safeResolution, Vector3.right, Vector3.forward, Vector3.up, projectDirection);
            BuildRadialProxyFace(meshData, safeResolution, Vector3.left, Vector3.back, Vector3.up, projectDirection);
            BuildRadialProxyFace(meshData, safeResolution, Vector3.up, Vector3.right, Vector3.back, projectDirection);
            BuildRadialProxyFace(meshData, safeResolution, Vector3.down, Vector3.right, Vector3.forward, projectDirection);
            BuildRadialProxyFace(meshData, safeResolution, Vector3.forward, Vector3.left, Vector3.up, projectDirection);
            BuildRadialProxyFace(meshData, safeResolution, Vector3.back, Vector3.right, Vector3.up, projectDirection);
        }

        private void BuildRadialProxyFace(
            MarchingCubesMeshData meshData,
            int resolution,
            Vector3 normal,
            Vector3 axisA,
            Vector3 axisB,
            System.Func<Vector3, Vector3> projectDirection)
        {
            for (int y = 0; y < resolution; y++)
            {
                float v0 = -1f + (2f * y) / resolution;
                float v1 = -1f + (2f * (y + 1)) / resolution;

                for (int x = 0; x < resolution; x++)
                {
                    float u0 = -1f + (2f * x) / resolution;
                    float u1 = -1f + (2f * (x + 1)) / resolution;

                    Vector3 p00 = projectDirection.Invoke((normal + axisA * u0 + axisB * v0).normalized);
                    Vector3 p10 = projectDirection.Invoke((normal + axisA * u1 + axisB * v0).normalized);
                    Vector3 p11 = projectDirection.Invoke((normal + axisA * u1 + axisB * v1).normalized);
                    Vector3 p01 = projectDirection.Invoke((normal + axisA * u0 + axisB * v1).normalized);
                    AddOutwardQuad(meshData, p00, p10, p11, p01);
                }
            }
        }

        private Vector3 ProjectToTerrainSurface(Vector3 direction)
        {
            float radiusOffset = SampleRadialProxyRadiusOffset(direction);
            float surfaceRadius = radius + radiusOffset * GetRadialProxyHeightStrength(globalLod);

            return direction * Mathf.Max(0.001f, surfaceRadius);
        }

        private float SampleRadialProxyRadiusOffset(Vector3 direction)
        {
            float smoothing = GetRadialProxySmoothing(globalLod);
            if (smoothing <= 0.0001f)
            {
                return GetRadiusOffset(direction * radius);
            }

            BuildTangentFrame(direction, out Vector3 tangentA, out Vector3 tangentB);
            float centerWeight = 2f;
            float sum = GetRadiusOffset(direction * radius) * centerWeight;
            float weight = centerWeight;

            sum += GetRadiusOffset((direction + tangentA * smoothing).normalized * radius);
            sum += GetRadiusOffset((direction - tangentA * smoothing).normalized * radius);
            sum += GetRadiusOffset((direction + tangentB * smoothing).normalized * radius);
            sum += GetRadiusOffset((direction - tangentB * smoothing).normalized * radius);
            weight += 4f;

            return sum / weight;
        }

        private static void BuildTangentFrame(Vector3 direction, out Vector3 tangentA, out Vector3 tangentB)
        {
            Vector3 reference = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) < 0.9f ? Vector3.up : Vector3.right;
            tangentA = Vector3.Cross(reference, direction).normalized;
            tangentB = Vector3.Cross(direction, tangentA).normalized;
        }

        private static void AddOutwardQuad(MarchingCubesMeshData meshData, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            AddOutwardTriangle(meshData, a, b, c);
            AddOutwardTriangle(meshData, a, c, d);
        }

        private static void AddOutwardTriangle(MarchingCubesMeshData meshData, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 desiredNormal = a + b + c;
            Vector3 currentNormal = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(currentNormal, desiredNormal) < 0f)
            {
                meshData.AddTriangle(a, c, b);
                return;
            }

            meshData.AddTriangle(a, b, c);
        }

        private void PolygonizeChunkCells(
            MarchingCubesMeshData meshData,
            Vector3 chunkCenter,
            int startX,
            int endX,
            int startY,
            int endY,
            int startZ,
            int endZ,
            System.Func<Vector3, float> sampleField)
        {
            Vector3 halfSize = Vector3.one * (CubeSize * 0.5f);
            for (int z = startZ; z < endZ; z++)
            {
                for (int y = startY; y < endY; y++)
                {
                    for (int x = startX; x < endX; x++)
                    {
                        Vector3 cellCenter = GridCubeCenterToLocalPosition(x, y, z);
                        MarchingCube cube = BuildCube(chunkCenter, cellCenter - chunkCenter, halfSize, sampleField);
                        MarchingCubesPolygonizer.Polygonize(cube, IsoLevel, meshData);
                    }
                }
            }
        }

        private MarchingCube BuildCube(Vector3 chunkCenter, Vector3 cellLocalCenter, Vector3 halfSize, System.Func<Vector3, float> sampleField)
        {
            return new MarchingCube(
                BuildCorner(chunkCenter, cellLocalCenter, new Vector3(-halfSize.x, -halfSize.y, -halfSize.z), sampleField),
                BuildCorner(chunkCenter, cellLocalCenter, new Vector3(halfSize.x, -halfSize.y, -halfSize.z), sampleField),
                BuildCorner(chunkCenter, cellLocalCenter, new Vector3(halfSize.x, halfSize.y, -halfSize.z), sampleField),
                BuildCorner(chunkCenter, cellLocalCenter, new Vector3(-halfSize.x, halfSize.y, -halfSize.z), sampleField),
                BuildCorner(chunkCenter, cellLocalCenter, new Vector3(-halfSize.x, -halfSize.y, halfSize.z), sampleField),
                BuildCorner(chunkCenter, cellLocalCenter, new Vector3(halfSize.x, -halfSize.y, halfSize.z), sampleField),
                BuildCorner(chunkCenter, cellLocalCenter, new Vector3(halfSize.x, halfSize.y, halfSize.z), sampleField),
                BuildCorner(chunkCenter, cellLocalCenter, new Vector3(-halfSize.x, halfSize.y, halfSize.z), sampleField));
        }

        private MarchingCubeCorner BuildCorner(Vector3 chunkCenter, Vector3 cellLocalCenter, Vector3 cornerOffset, System.Func<Vector3, float> sampleField)
        {
            Vector3 meshLocalPosition = cellLocalCenter + cornerOffset;
            Vector3 sphereLocalPosition = chunkCenter + meshLocalPosition;
            return new MarchingCubeCorner(meshLocalPosition, sampleField.Invoke(sphereLocalPosition));
        }

        private Vector3 GridCubeCenterToLocalPosition(int x, int y, int z)
        {
            return GridCellBoundaryToLocalPosition(x, y, z) + Vector3.one * (CubeSize * 0.5f);
        }

        private Vector3 GridCellBoundaryToLocalPosition(int x, int y, int z)
        {
            float gridSize = GridRadius * 2f;
            Vector3 min = Vector3.one * (-gridSize * 0.5f);
            return min + new Vector3(x, y, z) * CubeSize;
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
            generatedChunkCount = 0;
            generatedCubeCount = 0;
            generatedMeshCount = 0;
            generatedTriangleCount = 0;
            generatedTerrainTriangleReduction = 0;
            generatedOceanMeshCount = 0;
            generatedOceanTriangleCount = 0;
            generatedOceanTriangleReduction = 0;
            generatedCameraLod5ChunkCount = 0;
            generatedCameraLod6ChunkCount = 0;
            generatedLodMode = "None";
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
            int lodChunkSpan = GetLodChunkSpan(globalLod);
            float simplificationRatio = GetLodTargetTriangleRatio(globalLod);
            string lodDetails = useMainCameraChunkLodTest
                ? $"MainCamera chunk test, LOD5 <= {mainCameraLod5Distance:0.0}, else LOD6"
                : UsesRadialProxy(globalLod)
                    ? $"proxy resolution {GetRadialProxyResolution(globalLod)}"
                    : $"chunk span {lodChunkSpan}, triangle target {simplificationRatio:0.00}";
            lastGenerationReport =
                $"Profile: {profileName}\n" +
                $"Global LOD: {globalLod} ({lodDetails})\n" +
                $"LOD Mode: {generatedLodMode}\n" +
                $"Surface Noise Enabled: {UsesProfileNoise}\n" +
                $"Continents Enabled: {UsesContinents}\n" +
                $"Voronoi Cells/Land Cells: {voronoiCellCount} / {landCellCount}\n" +
                $"Land Elevation Min/Max: {landMinElevation:0.0000} / {landMaxElevation:0.0000}\n" +
                $"Seed: {noiseSeed}\n" +
                $"Max Profile Offset: {MaxProfileRadiusOffset:0.0000}\n" +
                $"Max Continent Offset: {MaxContinentRadiusOffset:0.0000}\n" +
                $"Radius Offset Min/Max: {minRadiusOffset:0.0000} / {maxRadiusOffset:0.0000}\n" +
                $"Field Min/Max: {minFieldValue:0.0000} / {maxFieldValue:0.0000}\n" +
                $"Chunks/Sampled Cubes: {generatedChunkCount} / {generatedCubeCount}\n" +
                $"Camera Test LOD5/LOD6 Chunks: {generatedCameraLod5ChunkCount} / {generatedCameraLod6ChunkCount}\n" +
                $"Camera Test Auto Refresh/Move Threshold: {autoRegenerateMainCameraChunkLodTest} / {mainCameraLodMoveThreshold:0.0}\n" +
                $"Terrain Meshes/Triangles/Removed: {generatedMeshCount} / {generatedTriangleCount} / {generatedTerrainTriangleReduction}\n" +
                $"Ocean Meshes/Triangles/Removed: {generatedOceanMeshCount} / {generatedOceanTriangleCount} / {generatedOceanTriangleReduction}\n" +
                $"Renderer Shadows: {generatedShadowCastingMode}, Receive {generatedReceiveShadows}";
        }

        private int ApplyLodSimplification(MarchingCubesMeshData meshData, Vector3 chunkHalfSize, bool protectTerrainUvs, PlanetLod lod)
        {
            float targetRatio = GetLodTargetTriangleRatio(lod);
            if (targetRatio >= 0.999f || meshData.TriangleCount <= 2)
            {
                return 0;
            }

            float cubeSize = CubeSize;
            float boundaryTolerance = cubeSize * 0.001f;
            return meshData.SimplifyConservative(
                targetRatio,
                cubeSize * GetLodMaxEdgeLength(lod),
                cubeSize * GetLodMaxSurfaceError(lod),
                protectTerrainUvs ? GetLodMaxUvDelta(lod) : float.PositiveInfinity,
                protectTerrainUvs ? seaLevelAtlasV : float.NaN,
                GetLodMinNormalDot(lod),
                cubeSize * 0.0001f,
                vertex => IsOnChunkBoundary(vertex, chunkHalfSize, boundaryTolerance));
        }

        private static bool IsOnChunkBoundary(Vector3 vertex, Vector3 chunkHalfSize, float tolerance)
        {
            return Mathf.Abs(Mathf.Abs(vertex.x) - chunkHalfSize.x) <= tolerance
                || Mathf.Abs(Mathf.Abs(vertex.y) - chunkHalfSize.y) <= tolerance
                || Mathf.Abs(Mathf.Abs(vertex.z) - chunkHalfSize.z) <= tolerance;
        }

        private static int GetLodChunkSpan(PlanetLod lod)
        {
            switch (lod)
            {
                case PlanetLod.LOD6:
                    return 2;
                case PlanetLod.LOD7:
                    return 4;
                case PlanetLod.LOD8:
                    return 4;
                case PlanetLod.LOD9:
                case PlanetLod.LOD10:
                    return 8;
                case PlanetLod.LOD5:
                default:
                    return 1;
            }
        }

        private static float GetLodTargetTriangleRatio(PlanetLod lod)
        {
            switch (lod)
            {
                case PlanetLod.LOD6:
                    return 0.85f;
                case PlanetLod.LOD7:
                    return 0.65f;
                case PlanetLod.LOD8:
                    return 0.48f;
                case PlanetLod.LOD9:
                case PlanetLod.LOD10:
                    return 0.45f;
                case PlanetLod.LOD5:
                default:
                    return 1f;
            }
        }

        private static float GetLodMaxEdgeLength(PlanetLod lod)
        {
            switch (lod)
            {
                case PlanetLod.LOD6:
                    return 1.1f;
                case PlanetLod.LOD7:
                    return 1.6f;
                case PlanetLod.LOD8:
                    return 1.9f;
                case PlanetLod.LOD9:
                case PlanetLod.LOD10:
                    return 2.2f;
                case PlanetLod.LOD5:
                default:
                    return 0f;
            }
        }

        private static float GetLodMaxSurfaceError(PlanetLod lod)
        {
            switch (lod)
            {
                case PlanetLod.LOD6:
                    return 0.03f;
                case PlanetLod.LOD7:
                    return 0.06f;
                case PlanetLod.LOD8:
                    return 0.08f;
                case PlanetLod.LOD9:
                case PlanetLod.LOD10:
                    return 0.1f;
                case PlanetLod.LOD5:
                default:
                    return 0f;
            }
        }

        private static float GetLodMaxUvDelta(PlanetLod lod)
        {
            switch (lod)
            {
                case PlanetLod.LOD6:
                    return 0.035f;
                case PlanetLod.LOD7:
                    return 0.06f;
                case PlanetLod.LOD8:
                    return 0.08f;
                case PlanetLod.LOD9:
                case PlanetLod.LOD10:
                    return 0.09f;
                case PlanetLod.LOD5:
                default:
                    return 0f;
            }
        }

        private static float GetLodMinNormalDot(PlanetLod lod)
        {
            switch (lod)
            {
                case PlanetLod.LOD6:
                    return 0.94f;
                case PlanetLod.LOD7:
                    return 0.9f;
                case PlanetLod.LOD8:
                    return 0.88f;
                case PlanetLod.LOD9:
                case PlanetLod.LOD10:
                    return 0.85f;
                case PlanetLod.LOD5:
                default:
                    return 1f;
            }
        }

        private static bool UsesRadialProxy(PlanetLod lod)
        {
            return (int)lod >= (int)PlanetLod.LOD9;
        }

        private static int GetRadialProxyResolution(PlanetLod lod)
        {
            switch (lod)
            {
                case PlanetLod.LOD8:
                    return 8;
                case PlanetLod.LOD9:
                    return 6;
                case PlanetLod.LOD10:
                    return 4;
                case PlanetLod.LOD5:
                case PlanetLod.LOD6:
                case PlanetLod.LOD7:
                default:
                    return 6;
            }
        }

        private static float GetRadialProxyHeightStrength(PlanetLod lod)
        {
            switch (lod)
            {
                case PlanetLod.LOD9:
                    return 0.62f;
                case PlanetLod.LOD10:
                    return 0.45f;
                case PlanetLod.LOD8:
                default:
                    return 0.7f;
            }
        }

        private static float GetRadialProxySmoothing(PlanetLod lod)
        {
            switch (lod)
            {
                case PlanetLod.LOD9:
                    return 0.16f;
                case PlanetLod.LOD10:
                    return 0.26f;
                case PlanetLod.LOD8:
                default:
                    return 0.12f;
            }
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
                ApplyGeneratedHideFlagsRecursively(generatedRoot);
                return generatedRoot;
            }

            GameObject rootObject = new GameObject(GeneratedRootName);
            MarkGeneratedObject(rootObject);
            rootObject.transform.SetParent(transform, false);
            generatedRoot = rootObject.transform;
            return generatedRoot;
        }

        private static void ApplyGeneratedHideFlagsRecursively(Transform root)
        {
            if (root == null)
            {
                return;
            }

            MarkGeneratedObject(root.gameObject);
            for (int i = 0; i < root.childCount; i++)
            {
                ApplyGeneratedHideFlagsRecursively(root.GetChild(i));
            }
        }

        private static void MarkGeneratedObject(GameObject generatedObject)
        {
            if (generatedObject == null)
            {
                return;
            }

            generatedObject.hideFlags = GeneratedObjectHideFlags;
        }

        private static void MarkGeneratedMesh(Mesh generatedMesh)
        {
            if (generatedMesh == null)
            {
                return;
            }

            generatedMesh.hideFlags = GeneratedMeshHideFlags;
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

        private string BuildChunkName(int startX, int startY, int startZ, int endX, int endY, int endZ)
        {
            if (endX == startX + 1 && endY == startY + 1 && endZ == startZ + 1)
            {
                return BuildCubeName(startX, startY, startZ);
            }

            return $"Chunk_{BuildCoordinateName(startX, startY, startZ)}_to_{BuildCoordinateName(endX - 1, endY - 1, endZ - 1)}";
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
            if (root == null)
            {
                return;
            }

            Object[] selectedObjects = Selection.objects;
            for (int i = 0; i < selectedObjects.Length; i++)
            {
                if (IsGeneratedSelection(selectedObjects[i], root))
                {
                    Selection.objects = new Object[] { gameObject };
                    ActiveEditorTracker.sharedTracker.ForceRebuild();
                    return;
                }
            }
        }

        private static bool IsGeneratedSelection(Object selectedObject, Transform root)
        {
            try
            {
                if (selectedObject is GameObject selectedGameObject)
                {
                    return IsGeneratedTransform(selectedGameObject.transform, root);
                }

                if (selectedObject is Component selectedComponent)
                {
                    return IsGeneratedTransform(selectedComponent.transform, root);
                }
            }
            catch (MissingReferenceException)
            {
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
