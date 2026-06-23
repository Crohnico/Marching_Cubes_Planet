using System.Collections.Generic;
using MarchingCubesPlanet.VoxelEngine.Data;
using MarchingCubesPlanet.VoxelEngine.Jobs;
using MarchingCubesPlanet.VoxelEngine.MarchingCubes;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    internal static class ChunkBuilder
    {
        private const int MaxVerticesPerCell = 36;
        private static readonly ProfilerMarker BuildRequestsMarker = new ProfilerMarker("VoxelEngine.BuildCellRequests");
        private static readonly ProfilerMarker StartChunkBuildMarker = new ProfilerMarker("VoxelEngine.StartChunkBuild");

        public static ChunkBuild StartChunkBuild(
            int3 chunkCoord,
            DesiredChunkState desiredState,
            int3 chunkSize,
            ScalarFieldSettings scalarField,
            MarchingCubesCaseTable caseTable,
            List<VoxelCellBuildRequest> requestBuffer)
        {
            using (StartChunkBuildMarker.Auto())
            {
                int3 chunkOrigin = VoxelChunkUtility.GetChunkOrigin(chunkCoord, chunkSize);
                using (BuildRequestsMarker.Auto())
                {
                    BuildCellRequests(
                        requestBuffer,
                        chunkOrigin,
                        chunkSize,
                        desiredState.cellSize);
                }

                int cellCount = requestBuffer.Count;
                NativeArray<VoxelCellBuildRequest> requests = new NativeArray<VoxelCellBuildRequest>(cellCount, Allocator.Persistent);
                NativeArray<VoxelCell> cells = new NativeArray<VoxelCell>(cellCount, Allocator.Persistent);
                NativeList<float3> vertices = CreateMeshList<float3>(cellCount, Allocator.Persistent);
                NativeList<float3> normals = CreateMeshList<float3>(cellCount, Allocator.Persistent);
                NativeList<float2> uvs = CreateMeshList<float2>(cellCount, Allocator.Persistent);
                NativeList<int> interiorIndices = CreateMeshList<int>(cellCount, Allocator.Persistent);
                NativeList<int> transitionIndices = CreateMeshList<int>(cellCount, Allocator.Persistent);
                NativeList<int> surfaceIndices = CreateMeshList<int>(cellCount, Allocator.Persistent);

                CopyRequests(requestBuffer, requests);

                JobHandle evaluateHandle = ScheduleEvaluateCells(scalarField, requests, cells, cellCount);
                GenerateChunkMeshJob meshJob = CreateMeshJob(
                    caseTable,
                    scalarField,
                    chunkOrigin,
                    chunkSize,
                    cells,
                    vertices,
                    normals,
                    uvs,
                    interiorIndices,
                    transitionIndices,
                    surfaceIndices);

                return new ChunkBuild
                {
                    chunkCoord = chunkCoord,
                    cellSize = desiredState.cellSize,
                    detailFocusKey = desiredState.detailFocusKey,
                    chunkOrigin = chunkOrigin,
                    chunkSize = chunkSize,
                    requests = requests,
                    cells = cells,
                    vertices = vertices,
                    normals = normals,
                    uvs = uvs,
                    interiorIndices = interiorIndices,
                    transitionIndices = transitionIndices,
                    surfaceIndices = surfaceIndices,
                    jobHandle = meshJob.Schedule(evaluateHandle)
                };
            }
        }

        public static Mesh BuildChunkMeshNow(
            string meshName,
            int3 chunkOrigin,
            int3 chunkSize,
            int cellSize,
            ScalarFieldSettings scalarField,
            MarchingCubesCaseTable caseTable,
            List<VoxelCellBuildRequest> requestBuffer,
            out VoxelChunkAltIndices altIndices)
        {
            using (BuildRequestsMarker.Auto())
            {
                BuildCellRequests(requestBuffer, chunkOrigin, chunkSize, cellSize);
            }

            if (requestBuffer.Count == 0)
            {
                altIndices = default;
                return null;
            }

            NativeArray<VoxelCellBuildRequest> requests = default;
            NativeArray<VoxelCell> cells = default;
            NativeList<float3> vertices = default;
            NativeList<float3> normals = default;
            NativeList<float2> uvs = default;
            NativeList<int> interiorIndices = default;
            NativeList<int> transitionIndices = default;
            NativeList<int> surfaceIndices = default;

            try
            {
                int cellCount = requestBuffer.Count;
                requests = new NativeArray<VoxelCellBuildRequest>(cellCount, Allocator.TempJob);
                cells = new NativeArray<VoxelCell>(cellCount, Allocator.TempJob);
                vertices = CreateMeshList<float3>(cellCount, Allocator.TempJob);
                normals = CreateMeshList<float3>(cellCount, Allocator.TempJob);
                uvs = CreateMeshList<float2>(cellCount, Allocator.TempJob);
                interiorIndices = CreateMeshList<int>(cellCount, Allocator.TempJob);
                transitionIndices = CreateMeshList<int>(cellCount, Allocator.TempJob);
                surfaceIndices = CreateMeshList<int>(cellCount, Allocator.TempJob);

                CopyRequests(requestBuffer, requests);
                JobHandle evaluateHandle = ScheduleEvaluateCells(scalarField, requests, cells, cellCount);
                CreateMeshJob(
                    caseTable,
                    scalarField,
                    chunkOrigin,
                    chunkSize,
                    cells,
                    vertices,
                    normals,
                    uvs,
                    interiorIndices,
                    transitionIndices,
                    surfaceIndices).Schedule(evaluateHandle).Complete();

                return MeshCrafter.BuildChunkMesh(
                    meshName,
                    vertices,
                    normals,
                    uvs,
                    interiorIndices,
                    transitionIndices,
                    surfaceIndices,
                    out altIndices);
            }
            finally
            {
                DisposeIfCreated(requests);
                DisposeIfCreated(cells);
                DisposeIfCreated(vertices);
                DisposeIfCreated(normals);
                DisposeIfCreated(uvs);
                DisposeIfCreated(interiorIndices);
                DisposeIfCreated(transitionIndices);
                DisposeIfCreated(surfaceIndices);
            }
        }

        public static PlanetMeshData BuildPlanetMeshData(
            int3 chunkOrigin,
            int3 chunkSize,
            int cellSize,
            ScalarFieldSettings scalarField,
            MarchingCubesCaseTable caseTable,
            List<VoxelCellBuildRequest> requestBuffer)
        {
            using (BuildRequestsMarker.Auto())
            {
                BuildCellRequests(requestBuffer, chunkOrigin, chunkSize, cellSize);
            }

            int cellCount = requestBuffer.Count;
            NativeArray<VoxelCellBuildRequest> requests = new NativeArray<VoxelCellBuildRequest>(cellCount, Allocator.Persistent);
            NativeArray<VoxelCell> cells = new NativeArray<VoxelCell>(cellCount, Allocator.Persistent);
            NativeList<float3> vertices = CreateMeshList<float3>(cellCount, Allocator.Persistent);
            NativeList<float3> normals = CreateMeshList<float3>(cellCount, Allocator.Persistent);
            NativeList<float2> uvs = CreateMeshList<float2>(cellCount, Allocator.Persistent);
            NativeList<int> interiorIndices = CreateMeshList<int>(cellCount, Allocator.Persistent);
            NativeList<int> transitionIndices = CreateMeshList<int>(cellCount, Allocator.Persistent);
            NativeList<int> surfaceIndices = CreateMeshList<int>(cellCount, Allocator.Persistent);

            try
            {
                CopyRequests(requestBuffer, requests);
                JobHandle evaluateHandle = ScheduleEvaluateCells(scalarField, requests, cells, cellCount);
                CreateMeshJob(
                    caseTable,
                    scalarField,
                    chunkOrigin,
                    chunkSize,
                    cells,
                    vertices,
                    normals,
                    uvs,
                    interiorIndices,
                    transitionIndices,
                    surfaceIndices).Schedule(evaluateHandle).Complete();

                return MeshCrafter.ToPlanetMeshData(
                    vertices,
                    normals,
                    uvs,
                    interiorIndices,
                    transitionIndices,
                    surfaceIndices);
            }
            finally
            {
                DisposeIfCreated(requests);
                DisposeIfCreated(cells);
                DisposeIfCreated(vertices);
                DisposeIfCreated(normals);
                DisposeIfCreated(uvs);
                DisposeIfCreated(interiorIndices);
                DisposeIfCreated(transitionIndices);
                DisposeIfCreated(surfaceIndices);
            }
        }

        public static void BuildCellRequests(
            List<VoxelCellBuildRequest> requests,
            int3 chunkOrigin,
            int3 chunkSize,
            int maximumCellSize,
            bool clearRequests = true)
        {
            int normalizedMaximumCellSize = math.max(1, maximumCellSize);
            normalizedMaximumCellSize = NormalizeCellSizeForChunk(normalizedMaximumCellSize, chunkSize);
            if (clearRequests)
            {
                requests.Clear();
            }

            AddCells(requests, chunkOrigin, chunkSize, normalizedMaximumCellSize);
        }

        public static int NormalizeCellSizeForChunk(int requestedSize, int3 chunkSize)
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

        private static NativeList<T> CreateMeshList<T>(int cellCount, Allocator allocator)
            where T : unmanaged
        {
            return new NativeList<T>(math.max(1, cellCount * MaxVerticesPerCell), allocator);
        }

        private static JobHandle ScheduleEvaluateCells(
            ScalarFieldSettings scalarField,
            NativeArray<VoxelCellBuildRequest> requests,
            NativeArray<VoxelCell> cells,
            int cellCount)
        {
            EvaluateVoxelCellsJob evaluateJob = new EvaluateVoxelCellsJob
            {
                scalarField = scalarField,
                requests = requests,
                cells = cells
            };

            return evaluateJob.Schedule(cellCount, 64);
        }

        private static GenerateChunkMeshJob CreateMeshJob(
            MarchingCubesCaseTable caseTable,
            ScalarFieldSettings scalarField,
            int3 chunkOrigin,
            int3 chunkSize,
            NativeArray<VoxelCell> cells,
            NativeList<float3> vertices,
            NativeList<float3> normals,
            NativeList<float2> uvs,
            NativeList<int> interiorIndices,
            NativeList<int> transitionIndices,
            NativeList<int> surfaceIndices)
        {
            return new GenerateChunkMeshJob
            {
                cells = cells,
                cornerIndexAFromEdge = caseTable.cornerIndexAFromEdge,
                cornerIndexBFromEdge = caseTable.cornerIndexBFromEdge,
                triangulation = caseTable.triangulation,
                chunkOrigin = chunkOrigin,
                chunkSize = chunkSize,
                scalarField = scalarField,
                vertices = vertices,
                normals = normals,
                uvs = uvs,
                interiorIndices = interiorIndices,
                transitionIndices = transitionIndices,
                surfaceIndices = surfaceIndices
            };
        }

        private static void CopyRequests(
            List<VoxelCellBuildRequest> source,
            NativeArray<VoxelCellBuildRequest> destination)
        {
            for (int i = 0; i < source.Count; i++)
            {
                destination[i] = source[i];
            }
        }

        private static void AddCells(
            List<VoxelCellBuildRequest> requests,
            int3 origin,
            int3 size,
            int cellSize)
        {
            for (int x = 0; x < size.x; x += cellSize)
            {
                for (int y = 0; y < size.y; y += cellSize)
                {
                    for (int z = 0; z < size.z; z += cellSize)
                    {
                        requests.Add(new VoxelCellBuildRequest
                        {
                            origin = origin + new int3(x, y, z),
                            size = cellSize
                        });
                    }
                }
            }
        }

        private static void DisposeIfCreated<T>(NativeArray<T> array)
            where T : struct
        {
            if (array.IsCreated)
            {
                array.Dispose();
            }
        }

        private static void DisposeIfCreated<T>(NativeList<T> list)
            where T : unmanaged
        {
            if (list.IsCreated)
            {
                list.Dispose();
            }
        }
    }
}
