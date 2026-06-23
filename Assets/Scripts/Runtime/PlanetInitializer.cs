using System.Collections.Generic;
using System.Threading.Tasks;
using MarchingCubesPlanet.VoxelEngine.Data;
using MarchingCubesPlanet.VoxelEngine.MarchingCubes;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    public static class PlanetInitializer
    {
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
            return ChunkBuilder.BuildPlanetMeshData(
                chunkOrigin,
                settings.chunkSize,
                settings.cellSize,
                settings.scalarField,
                caseTable,
                requestBuffer);
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

    }
}
