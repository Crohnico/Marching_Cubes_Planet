using System.Collections.Generic;
using System.Threading.Tasks;
using MarchingCubesPlanet.VoxelEngine.Data;
using MarchingCubesPlanet.VoxelEngine.Jobs;
using MarchingCubesPlanet.VoxelEngine.MarchingCubes;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    public static class PlanetInitializer
    {
        private const int MaxVerticesPerCell = 36;

        public static Task<PlanetData> InitializePlanet(PlanetManager planetConfig)
        {
            PlanetInitializationSettings settings = planetConfig.CreateInitializationSettings();
            HashSet<int3> declaredChunks = new HashSet<int3>();
            planetConfig.CollectDeclaredChunksForInitialization(declaredChunks);

            PlanetData planetData = new PlanetData
            {
                stellarID = settings.stellarID,
                planetID = settings.planetID,
                worldPosition = settings.worldPosition,
                radius = settings.radius,
                seed = settings.seed,
                chunkSize = settings.chunkSize,
                segmentCount = settings.segmentCount,
                segmentGrid = settings.segmentGrid,
                declaredChunks = declaredChunks
            };

            MarchingCubesCaseTable caseTable = MarchingCubesCaseTableBuilder.Build(Allocator.Persistent);
            try
            {
                foreach (int3 chunkCoord in declaredChunks)
                {
                    planetData.chunks.Add(BuildChunkData(settings, caseTable, chunkCoord));
                }
            }
            finally
            {
                caseTable.Dispose();
            }

            return Task.FromResult(planetData);
        }

        private static PlanetChunkBuildData BuildChunkData(
            PlanetInitializationSettings settings,
            MarchingCubesCaseTable caseTable,
            int3 chunkCoord)
        {
            int3 chunkOrigin = VoxelChunkUtility.GetChunkOrigin(chunkCoord, settings.chunkSize);
            PlanetMeshData meshData = BuildChunkMeshData(settings, caseTable, chunkOrigin);
            float3 chunkSize = new float3(settings.chunkSize.x, settings.chunkSize.y, settings.chunkSize.z);

            return new PlanetChunkBuildData
            {
                coord = chunkCoord,
                segmentId = GetSegmentIndexForChunk(settings, chunkOrigin),
                cellSize = settings.cellSize,
                detailFocusKey = settings.detailFocusKey,
                origin = chunkOrigin,
                boundsCenter = new float3(chunkOrigin.x, chunkOrigin.y, chunkOrigin.z) + chunkSize * 0.5f,
                boundsSize = chunkSize,
                mesh = meshData
            };
        }

        private static PlanetMeshData BuildChunkMeshData(
            PlanetInitializationSettings settings,
            MarchingCubesCaseTable caseTable,
            int3 chunkOrigin)
        {
            List<VoxelCellBuildRequest> requestBuffer = new List<VoxelCellBuildRequest>();
            BuildCellRequests(requestBuffer, chunkOrigin, settings.chunkSize, settings.cellSize);
            int cellCount = requestBuffer.Count;

            NativeArray<VoxelCellBuildRequest> requests = new NativeArray<VoxelCellBuildRequest>(cellCount, Allocator.Persistent);
            NativeArray<VoxelCell> cells = new NativeArray<VoxelCell>(cellCount, Allocator.Persistent);
            NativeList<float3> vertices = new NativeList<float3>(cellCount * MaxVerticesPerCell, Allocator.Persistent);
            NativeList<float3> normals = new NativeList<float3>(cellCount * MaxVerticesPerCell, Allocator.Persistent);
            NativeList<float2> uvs = new NativeList<float2>(cellCount * MaxVerticesPerCell, Allocator.Persistent);
            NativeList<int> interiorIndices = new NativeList<int>(cellCount * MaxVerticesPerCell, Allocator.Persistent);
            NativeList<int> transitionIndices = new NativeList<int>(cellCount * MaxVerticesPerCell, Allocator.Persistent);
            NativeList<int> surfaceIndices = new NativeList<int>(cellCount * MaxVerticesPerCell, Allocator.Persistent);

            try
            {
                for (int i = 0; i < requestBuffer.Count; i++)
                {
                    requests[i] = requestBuffer[i];
                }

                EvaluateVoxelCellsJob evaluateJob = new EvaluateVoxelCellsJob
                {
                    scalarField = settings.scalarField,
                    requests = requests,
                    cells = cells
                };

                GenerateChunkMeshJob meshJob = new GenerateChunkMeshJob
                {
                    cells = cells,
                    cornerIndexAFromEdge = caseTable.cornerIndexAFromEdge,
                    cornerIndexBFromEdge = caseTable.cornerIndexBFromEdge,
                    triangulation = caseTable.triangulation,
                    chunkOrigin = chunkOrigin,
                    chunkSize = settings.chunkSize,
                    scalarField = settings.scalarField,
                    vertices = vertices,
                    normals = normals,
                    uvs = uvs,
                    interiorIndices = interiorIndices,
                    transitionIndices = transitionIndices,
                    surfaceIndices = surfaceIndices
                };

                JobHandle evaluateHandle = evaluateJob.Schedule(cellCount, 64);
                meshJob.Schedule(evaluateHandle).Complete();

                return CreateMeshData(vertices, normals, uvs, interiorIndices, transitionIndices, surfaceIndices);
            }
            finally
            {
                requests.Dispose();
                cells.Dispose();
                vertices.Dispose();
                normals.Dispose();
                uvs.Dispose();
                interiorIndices.Dispose();
                transitionIndices.Dispose();
                surfaceIndices.Dispose();
            }
        }

        private static PlanetMeshData CreateMeshData(
            NativeList<float3> vertices,
            NativeList<float3> normals,
            NativeList<float2> uvs,
            NativeList<int> interiorIndices,
            NativeList<int> transitionIndices,
            NativeList<int> surfaceIndices)
        {
            PlanetMeshData meshData = new PlanetMeshData
            {
                boundsCenter = float3.zero,
                boundsSize = float3.zero
            };

            Copy(vertices, meshData.vertices);
            Copy(normals, meshData.normals);
            Copy(uvs, meshData.uvs);
            Copy(interiorIndices, meshData.interiorIndices);
            Copy(transitionIndices, meshData.transitionIndices);
            Copy(surfaceIndices, meshData.surfaceIndices);

            if (meshData.vertices.Count > 0)
            {
                CalculateBounds(meshData.vertices, out meshData.boundsCenter, out meshData.boundsSize);
            }

            return meshData;
        }

        private static void BuildCellRequests(
            List<VoxelCellBuildRequest> requests,
            int3 origin,
            int3 size,
            int cellSize)
        {
            requests.Clear();
            int normalizedCellSize = math.max(1, NormalizeCellSizeForChunk(cellSize, size));
            for (int x = 0; x < size.x; x += normalizedCellSize)
            {
                for (int y = 0; y < size.y; y += normalizedCellSize)
                {
                    for (int z = 0; z < size.z; z += normalizedCellSize)
                    {
                        requests.Add(new VoxelCellBuildRequest
                        {
                            origin = origin + new int3(x, y, z),
                            size = normalizedCellSize
                        });
                    }
                }
            }
        }

        private static int GetSegmentIndexForChunk(PlanetInitializationSettings settings, int3 chunkOrigin)
        {
            float3 chunkCenterWorld = new float3(chunkOrigin.x, chunkOrigin.y, chunkOrigin.z)
                + new float3(settings.chunkSize.x, settings.chunkSize.y, settings.chunkSize.z) * 0.5f;
            Vector3 local = settings.worldToPlanetLocal.MultiplyPoint3x4(
                new Vector3(chunkCenterWorld.x, chunkCenterWorld.y, chunkCenterWorld.z));
            return GetSegmentIndexForLocalPosition(settings, local);
        }

        private static int GetSegmentIndexForLocalPosition(PlanetInitializationSettings settings, Vector3 localPosition)
        {
            int safeSegmentCount = math.max(1, settings.segmentCount);
            int3 grid = settings.segmentGrid;
            if (grid.x <= 0 || grid.y <= 0 || grid.z <= 0)
            {
                unchecked
                {
                    uint hash = (uint)Mathf.FloorToInt(localPosition.x) * 73856093u
                        ^ (uint)Mathf.FloorToInt(localPosition.y) * 19349663u
                        ^ (uint)Mathf.FloorToInt(localPosition.z) * 83492791u;
                    return (int)(hash % (uint)safeSegmentCount);
                }
            }

            float3 localCenter = new float3(localPosition.x, localPosition.y, localPosition.z) - settings.sphereLocalPosition;
            float radius = math.max(0.01f, settings.maximumTerrainRadius);
            float3 normalized = (localCenter + new float3(radius, radius, radius)) / (radius * 2f);
            int x = math.clamp((int)(normalized.x * grid.x), 0, grid.x - 1);
            int y = math.clamp((int)(normalized.y * grid.y), 0, grid.y - 1);
            int z = math.clamp((int)(normalized.z * grid.z), 0, grid.z - 1);
            return x + grid.x * (y + grid.y * z);
        }

        private static int NormalizeCellSizeForChunk(int requestedSize, int3 chunkSize)
        {
            int requested = math.max(1, math.min(requestedSize, math.min(chunkSize.x, math.min(chunkSize.y, chunkSize.z))));
            for (int size = requested; size >= 1; size--)
            {
                if (chunkSize.x % size == 0
                    && chunkSize.y % size == 0
                    && chunkSize.z % size == 0)
                {
                    return size;
                }
            }

            return 1;
        }

        private static void CalculateBounds(List<float3> vertices, out float3 center, out float3 size)
        {
            float3 min = vertices[0];
            float3 max = vertices[0];
            for (int i = 1; i < vertices.Count; i++)
            {
                min = math.min(min, vertices[i]);
                max = math.max(max, vertices[i]);
            }

            center = (min + max) * 0.5f;
            size = max - min;
        }

        private static void Copy(NativeList<float3> source, List<float3> destination)
        {
            destination.Capacity = math.max(destination.Capacity, source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                destination.Add(source[i]);
            }
        }

        private static void Copy(NativeList<float2> source, List<float2> destination)
        {
            destination.Capacity = math.max(destination.Capacity, source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                destination.Add(source[i]);
            }
        }

        private static void Copy(NativeList<int> source, List<int> destination)
        {
            destination.Capacity = math.max(destination.Capacity, source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                destination.Add(source[i]);
            }
        }
    }
}
