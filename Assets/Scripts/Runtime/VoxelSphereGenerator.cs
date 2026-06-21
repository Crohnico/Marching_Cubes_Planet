using System.Collections.Generic;
using MarchingCubesPlanet.VoxelEngine.Data;
using Unity.Mathematics;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    [DisallowMultipleComponent]
    public sealed class VoxelSphereGenerator : MonoBehaviour
    {
        [SerializeField] private VoxelChunkManager chunkManager;
        [SerializeField] private Transform centerOverride;
        [SerializeField] private Vector3 center = new Vector3(18f, 18f, 18f);
        [SerializeField, Min(0.01f)] private float radius = 14f;
        [SerializeField] private int seed = 12345;
        [SerializeField] private PlanetNoiseProfile planetNoiseProfile;
        [SerializeField] private float isoLevel;
        [SerializeField, Min(0f)] private float surfaceLayerDepth = 64f;
        [SerializeField, Min(0f)] private float transitionLayerDepth = 192f;
        [SerializeField] private bool declareOnlySurfaceChunks = true;
        [SerializeField, Min(0f)] private float chunkDeclarationPadding = 0f;
        [Header("Water")]
        [SerializeField] private bool generateWater = true;
        [SerializeField] private Material waterMaterial;
        [SerializeField, Range(1, 64)] private int waterSegmentCount = 64;
        [SerializeField, Min(4)] private int waterLatitudeSegments = 32;
        [SerializeField, Min(4)] private int waterLongitudeSegments = 64;
        [SerializeField, Min(0f)] private float waterRadiusPadding = 0.25f;

        private readonly HashSet<int3> declaredChunks = new HashSet<int3>();
        private readonly HashSet<int3> nextDeclaredChunks = new HashSet<int3>();
        private readonly List<GameObject> nearWaterSegments = new List<GameObject>();
        private GameObject waterRoot;
        private GameObject farWaterObject;
        private Mesh farWaterMesh;
        private bool waterMeshDirty = true;
        private Vector3 lastWaterCenter;
        private float lastWaterRadius = -1f;
        private Vector3 lastWaterHemisphereDirection;
        private bool hasLastWaterHemisphereDirection;

        public float Radius => radius;
        public Vector3 Center => centerOverride != null ? centerOverride.position : center;
        public float SeaSurfaceRadius => Mathf.Max(0.01f, radius + GetSeaSurfaceOffset());
        public float MaximumTerrainRadius => GetMaximumTerrainRadius();

        public float GetActionRadius(VoxelEngineConfig config)
        {
            float lodPadding = config != null ? config.CoarsestLodStartDistance : 0f;
            return GetMaximumTerrainRadius() + lodPadding;
        }

        public bool ContainsActionPoint(Vector3 point, VoxelEngineConfig config)
        {
            float actionRadius = GetActionRadius(config);
            return (point - Center).sqrMagnitude <= actionRadius * actionRadius;
        }

        private void Reset()
        {
            TryGetComponent(out chunkManager);
        }

        public void Configure(VoxelChunkManager nextChunkManager)
        {
            chunkManager = nextChunkManager;
        }

        public ScalarFieldSettings BuildScalarFieldSettings()
        {
            Vector3 resolvedCenter = Center;
            return new ScalarFieldSettings
            {
                debugSphereCenter = new float3(resolvedCenter.x, resolvedCenter.y, resolvedCenter.z),
                debugSphereRadius = radius,
                isoLevel = isoLevel,
                surfaceLayerDepth = surfaceLayerDepth,
                transitionLayerDepth = Mathf.Max(surfaceLayerDepth, transitionLayerDepth),
                planetNoise = planetNoiseProfile != null
                    ? planetNoiseProfile.BuildRuntimeSettings(seed)
                    : PlanetNoiseSettings.Default
            };
        }

        public void DeclareOccupiedChunks(VoxelChunkManager manager, int3 chunkSize)
        {
            if (manager == null)
            {
                return;
            }

            Vector3 resolvedCenter = Center;
            float maxRadius = GetMaximumTerrainRadius();
            float3 min = new float3(
                resolvedCenter.x - maxRadius,
                resolvedCenter.y - maxRadius,
                resolvedCenter.z - maxRadius);
            float3 max = new float3(
                resolvedCenter.x + maxRadius,
                resolvedCenter.y + maxRadius,
                resolvedCenter.z + maxRadius);

            int3 minChunk = VoxelChunkUtility.GetChunkCoords(min, chunkSize);
            int3 maxChunk = VoxelChunkUtility.GetChunkCoords(max, chunkSize);

            nextDeclaredChunks.Clear();
            for (int x = minChunk.x; x <= maxChunk.x; x++)
            {
                for (int y = minChunk.y; y <= maxChunk.y; y++)
                {
                    for (int z = minChunk.z; z <= maxChunk.z; z++)
                    {
                        int3 chunkCoord = new int3(x, y, z);
                        if (!declareOnlySurfaceChunks || DoesChunkIntersectSurface(chunkCoord, chunkSize, resolvedCenter))
                        {
                            nextDeclaredChunks.Add(chunkCoord);
                        }
                    }
                }
            }

            foreach (int3 chunkCoord in declaredChunks)
            {
                if (!nextDeclaredChunks.Contains(chunkCoord))
                {
                    manager.ReleaseChunk(chunkCoord);
                }
            }

            foreach (int3 chunkCoord in nextDeclaredChunks)
            {
                if (!declaredChunks.Contains(chunkCoord))
                {
                    manager.DeclareChunk(chunkCoord);
                }
            }

            declaredChunks.Clear();
            foreach (int3 chunkCoord in nextDeclaredChunks)
            {
                declaredChunks.Add(chunkCoord);
            }
        }

        private void ReleaseDeclaredChunks(VoxelChunkManager manager)
        {
            foreach (int3 chunkCoord in declaredChunks)
            {
                manager.ReleaseChunk(chunkCoord);
            }

            declaredChunks.Clear();
        }

        [ContextMenu("Generate")]
        public void Generate()
        {
            if (chunkManager == null)
            {
                Debug.LogWarning("VoxelSphereGenerator necesita un VoxelChunkManager asignado.", this);
                return;
            }

            chunkManager.Generate();
            UpdateWaterMeshes(true);
        }

        private void OnValidate()
        {
            radius = Mathf.Max(0.01f, radius);
            surfaceLayerDepth = Mathf.Max(0f, surfaceLayerDepth);
            transitionLayerDepth = Mathf.Max(surfaceLayerDepth, transitionLayerDepth);
            chunkDeclarationPadding = Mathf.Max(0f, chunkDeclarationPadding);
            waterLatitudeSegments = Mathf.Max(4, waterLatitudeSegments);
            waterLongitudeSegments = Mathf.Max(4, waterLongitudeSegments);
            waterSegmentCount = Mathf.Clamp(waterSegmentCount, 1, 64);
            waterRadiusPadding = Mathf.Max(0f, waterRadiusPadding);
            waterMeshDirty = true;
        }

        private bool DoesChunkIntersectSurface(int3 chunkCoord, int3 chunkSize, Vector3 sphereCenter)
        {
            int3 chunkOrigin = VoxelChunkUtility.GetChunkOrigin(chunkCoord, chunkSize);
            Vector3 min = new Vector3(chunkOrigin.x, chunkOrigin.y, chunkOrigin.z);
            Vector3 max = min + new Vector3(chunkSize.x, chunkSize.y, chunkSize.z);
            float minDistanceSquared = GetSquaredDistanceToAabb(sphereCenter, min, max);
            float maxDistanceSquared = GetMaxSquaredDistanceToAabb(sphereCenter, min, max);
            float minRadius = Mathf.Max(0f, GetMinimumTerrainRadius() - chunkDeclarationPadding);
            float maxRadius = GetMaximumTerrainRadius() + chunkDeclarationPadding;
            return minDistanceSquared <= maxRadius * maxRadius
                && maxDistanceSquared >= minRadius * minRadius;
        }

        private float GetMinimumTerrainRadius()
        {
            if (planetNoiseProfile == null)
            {
                return radius;
            }

            return Mathf.Max(0f, radius + planetNoiseProfile.GetMinimumSurfaceOffset(radius));
        }

        private float GetMaximumTerrainRadius()
        {
            if (planetNoiseProfile == null)
            {
                return radius;
            }

            return Mathf.Max(0.01f, radius + planetNoiseProfile.GetMaximumSurfaceOffset(radius));
        }

        private float GetSeaSurfaceOffset()
        {
            return planetNoiseProfile != null ? planetNoiseProfile.GetSeaSurfaceOffset(radius) : 0f;
        }

        private void OnEnable()
        {
            waterMeshDirty = true;
            UpdateWaterMeshes(true);
        }

        private void LateUpdate()
        {
            UpdateWaterMeshes(false);
        }

        private void UpdateWaterMeshes(bool forceRebuild)
        {
            if (!generateWater)
            {
                SetWaterActive(false, false);
                return;
            }

            EnsureWaterObjects();
            float waterRadius = SeaSurfaceRadius + waterRadiusPadding;
            Vector3 waterCenter = Center;
            Vector3 waterHemisphereDirection = GetWaterHemisphereDirection(waterCenter);
            bool geometryChanged = forceRebuild
                || waterMeshDirty
                || (waterCenter - lastWaterCenter).sqrMagnitude > 0.0001f
                || Mathf.Abs(waterRadius - lastWaterRadius) > 0.0001f
                || !hasLastWaterHemisphereDirection
                || Vector3.Dot(lastWaterHemisphereDirection, waterHemisphereDirection) < 0.998f;

            if (geometryChanged)
            {
                RebuildWaterMeshes(waterCenter, waterRadius, waterHemisphereDirection);
                lastWaterCenter = waterCenter;
                lastWaterRadius = waterRadius;
                lastWaterHemisphereDirection = waterHemisphereDirection;
                hasLastWaterHemisphereDirection = true;
                waterMeshDirty = false;
            }

            bool useNearWater = chunkManager != null && chunkManager.IsNearCombinedRenderingActive;
            SetWaterActive(!useNearWater, useNearWater);
        }

        private void EnsureWaterObjects()
        {
            if (waterRoot == null)
            {
                Transform existingRoot = transform.Find("Water");
                waterRoot = existingRoot != null ? existingRoot.gameObject : new GameObject("Water");
                waterRoot.transform.SetParent(transform, false);
                waterRoot.transform.localPosition = Vector3.zero;
                waterRoot.transform.localRotation = Quaternion.identity;
                waterRoot.transform.localScale = Vector3.one;
            }

            if (farWaterObject == null)
            {
                farWaterObject = EnsureWaterChild("Water_Far");
            }

            int targetWaterSegmentCount = GetWaterSegmentCount();
            while (nearWaterSegments.Count < targetWaterSegmentCount)
            {
                int index = nearWaterSegments.Count;
                nearWaterSegments.Add(EnsureWaterChild($"Water_Near_{index:00}"));
            }

            for (int i = 0; i < nearWaterSegments.Count; i++)
            {
                Transform segmentParent = i < targetWaterSegmentCount && chunkManager != null
                    ? chunkManager.GetNearSegmentTransformOrNull(i)
                    : null;
                Transform desiredParent = segmentParent != null ? segmentParent : waterRoot.transform;
                GameObject segment = nearWaterSegments[i];
                if (segment != null && segment.transform.parent != desiredParent)
                {
                    segment.transform.SetParent(desiredParent, false);
                    segment.transform.localPosition = Vector3.zero;
                    segment.transform.localRotation = Quaternion.identity;
                    segment.transform.localScale = Vector3.one;
                }
            }

            for (int i = targetWaterSegmentCount; i < nearWaterSegments.Count; i++)
            {
                if (nearWaterSegments[i] != null)
                {
                    nearWaterSegments[i].SetActive(false);
                }
            }
        }

        private GameObject EnsureWaterChild(string childName)
        {
            Transform existing = waterRoot.transform.Find(childName);
            GameObject child = existing != null ? existing.gameObject : new GameObject(childName);
            child.transform.SetParent(waterRoot.transform, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;

            if (!child.TryGetComponent(out MeshFilter meshFilter))
            {
                meshFilter = child.AddComponent<MeshFilter>();
            }

            if (!child.TryGetComponent(out MeshRenderer meshRenderer))
            {
                meshRenderer = child.AddComponent<MeshRenderer>();
            }

            meshRenderer.sharedMaterial = waterMaterial;
            return child;
        }

        private void RebuildWaterMeshes(Vector3 waterCenter, float waterRadius, Vector3 hemisphereDirection)
        {
            int targetWaterSegmentCount = GetWaterSegmentCount();
            int normalizedLongitudeSegments = Mathf.Max(targetWaterSegmentCount, waterLongitudeSegments);
            int longitudeSegmentsPerWaterSegment = Mathf.Max(1, Mathf.CeilToInt(normalizedLongitudeSegments / (float)targetWaterSegmentCount));
            int totalLongitudeSegments = longitudeSegmentsPerWaterSegment * targetWaterSegmentCount;

            farWaterMesh = ReplaceWaterMesh(
                farWaterObject,
                farWaterMesh,
                BuildWaterSphereMesh(
                    "Water_Far_Mesh",
                    farWaterObject.transform,
                    waterCenter,
                    waterRadius,
                    0,
                    1,
                    waterLatitudeSegments,
                    totalLongitudeSegments,
                    hemisphereDirection,
                    false));

            for (int i = 0; i < targetWaterSegmentCount; i++)
            {
                GameObject segment = nearWaterSegments[i];
                MeshFilter meshFilter = segment.GetComponent<MeshFilter>();
                Mesh previousMesh = meshFilter.sharedMesh;
                Mesh nextMesh = BuildWaterSphereMesh(
                    $"Water_Near_{i:00}_Mesh",
                    segment.transform,
                    waterCenter,
                    waterRadius,
                    i,
                    targetWaterSegmentCount,
                    waterLatitudeSegments,
                    totalLongitudeSegments,
                    hemisphereDirection,
                    true);
                meshFilter.sharedMesh = nextMesh;
                segment.GetComponent<MeshRenderer>().sharedMaterial = waterMaterial;
                DestroyMesh(previousMesh);
            }
        }

        private Mesh ReplaceWaterMesh(GameObject owner, Mesh previousMesh, Mesh nextMesh)
        {
            MeshFilter meshFilter = owner.GetComponent<MeshFilter>();
            meshFilter.sharedMesh = nextMesh;
            owner.GetComponent<MeshRenderer>().sharedMaterial = waterMaterial;
            DestroyMesh(previousMesh);
            return nextMesh;
        }

        private Mesh BuildWaterSphereMesh(
            string meshName,
            Transform meshOwner,
            Vector3 waterCenter,
            float waterRadius,
            int segmentIndex,
            int segmentCount,
            int latitudeSegments,
            int longitudeSegments,
            Vector3 hemisphereDirection,
            bool useWorldSegmentPartition)
        {
            float startLongitude = useWorldSegmentPartition ? 0f : Mathf.PI * 2f * segmentIndex / segmentCount;
            float endLongitude = useWorldSegmentPartition ? Mathf.PI * 2f : Mathf.PI * 2f * (segmentIndex + 1) / segmentCount;
            var vertices = new List<Vector3>((latitudeSegments + 1) * (longitudeSegments + 1));
            var worldVertices = new List<Vector3>((latitudeSegments + 1) * (longitudeSegments + 1));
            var worldNormals = new List<Vector3>((latitudeSegments + 1) * (longitudeSegments + 1));
            var normals = new List<Vector3>((latitudeSegments + 1) * (longitudeSegments + 1));
            var uvs = new List<Vector2>((latitudeSegments + 1) * (longitudeSegments + 1));
            var triangles = new List<int>(latitudeSegments * longitudeSegments * 6);

            for (int lat = 0; lat <= latitudeSegments; lat++)
            {
                float v = lat / (float)latitudeSegments;
                float polar = v * Mathf.PI;
                float sinPolar = Mathf.Sin(polar);
                float cosPolar = Mathf.Cos(polar);

                for (int lon = 0; lon <= longitudeSegments; lon++)
                {
                    float u = lon / (float)longitudeSegments;
                    float longitude = Mathf.Lerp(startLongitude, endLongitude, u);
                    Vector3 normal = new Vector3(
                        Mathf.Cos(longitude) * sinPolar,
                        cosPolar,
                        Mathf.Sin(longitude) * sinPolar);
                    Vector3 worldVertex = waterCenter + normal * waterRadius;
                    vertices.Add(meshOwner.InverseTransformPoint(worldVertex));
                    worldVertices.Add(worldVertex);
                    worldNormals.Add(normal);
                    normals.Add(meshOwner.InverseTransformDirection(normal).normalized);
                    uvs.Add(new Vector2(useWorldSegmentPartition ? u : (segmentIndex + u) / segmentCount, v));
                }
            }

            int row = longitudeSegments + 1;
            for (int lat = 0; lat < latitudeSegments; lat++)
            {
                for (int lon = 0; lon < longitudeSegments; lon++)
                {
                    int a = lat * row + lon;
                    int b = a + 1;
                    int c = a + row;
                    int d = c + 1;
                    AddWaterTriangleIfVisible(triangles, worldVertices, worldNormals, hemisphereDirection, segmentIndex, useWorldSegmentPartition, a, b, c);
                    AddWaterTriangleIfVisible(triangles, worldVertices, worldNormals, hemisphereDirection, segmentIndex, useWorldSegmentPartition, b, d, c);
                }
            }

            Mesh mesh = new Mesh
            {
                name = meshName,
                indexFormat = vertices.Count > 65535
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private Vector3 GetWaterHemisphereDirection(Vector3 waterCenter)
        {
            Vector3 focus = chunkManager != null ? chunkManager.DetailFocusPosition : transform.position;
            Vector3 direction = focus - waterCenter;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        }

        private int GetWaterSegmentCount()
        {
            return chunkManager != null
                ? Mathf.Clamp(chunkManager.NearSegmentCount, 1, 64)
                : Mathf.Clamp(waterSegmentCount, 1, 64);
        }

        private void AddWaterTriangleIfVisible(
            List<int> triangles,
            List<Vector3> worldVertices,
            List<Vector3> worldNormals,
            Vector3 hemisphereDirection,
            int segmentIndex,
            bool useWorldSegmentPartition,
            int a,
            int b,
            int c)
        {
            Vector3 faceDirection = (worldNormals[a] + worldNormals[b] + worldNormals[c]) / 3f;
            if (Vector3.Dot(faceDirection, hemisphereDirection) < 0f)
            {
                return;
            }

            if (useWorldSegmentPartition && chunkManager != null)
            {
                Vector3 centroid = (worldVertices[a] + worldVertices[b] + worldVertices[c]) / 3f;
                if (chunkManager.GetNearSegmentIndexForWorldPosition(centroid) != segmentIndex)
                {
                    return;
                }
            }

            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        private void SetWaterActive(bool farActive, bool nearActive)
        {
            if (farWaterObject != null)
            {
                farWaterObject.SetActive(farActive);
                if (farWaterObject.TryGetComponent(out MeshRenderer renderer))
                {
                    renderer.sharedMaterial = waterMaterial;
                }
            }

            for (int i = 0; i < nearWaterSegments.Count; i++)
            {
                GameObject segment = nearWaterSegments[i];
                if (segment == null)
                {
                    continue;
                }

                bool active = nearActive && i < GetWaterSegmentCount();
                segment.SetActive(active);
                if (segment.TryGetComponent(out MeshRenderer renderer))
                {
                    renderer.sharedMaterial = waterMaterial;
                }
            }
        }

        private static void DestroyMesh(Mesh mesh)
        {
            if (mesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(mesh);
            }
            else
            {
                DestroyImmediate(mesh);
            }
        }

        private static float GetSquaredDistanceToAabb(Vector3 point, Vector3 min, Vector3 max)
        {
            float squaredDistance = 0f;
            squaredDistance += GetSquaredDistanceToRange(point.x, min.x, max.x);
            squaredDistance += GetSquaredDistanceToRange(point.y, min.y, max.y);
            squaredDistance += GetSquaredDistanceToRange(point.z, min.z, max.z);
            return squaredDistance;
        }

        private static float GetSquaredDistanceToRange(float value, float min, float max)
        {
            if (value < min)
            {
                float delta = min - value;
                return delta * delta;
            }

            if (value > max)
            {
                float delta = value - max;
                return delta * delta;
            }

            return 0f;
        }

        private static float GetMaxSquaredDistanceToAabb(Vector3 point, Vector3 min, Vector3 max)
        {
            float x = Mathf.Max(Mathf.Abs(point.x - min.x), Mathf.Abs(point.x - max.x));
            float y = Mathf.Max(Mathf.Abs(point.y - min.y), Mathf.Abs(point.y - max.y));
            float z = Mathf.Max(Mathf.Abs(point.z - min.z), Mathf.Abs(point.z - max.z));
            return x * x + y * y + z * z;
        }

        private void OnDisable()
        {
            if (chunkManager == null)
            {
                declaredChunks.Clear();
                return;
            }

            ReleaseDeclaredChunks(chunkManager);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.35f);
            Gizmos.DrawWireSphere(Center, radius);
        }
    }
}
