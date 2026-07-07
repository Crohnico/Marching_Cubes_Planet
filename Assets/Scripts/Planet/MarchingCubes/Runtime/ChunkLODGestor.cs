using System;
using UnityEngine;

namespace MarchingCubesPlanet.MarchingCubes
{
    public sealed class ChunkLODGestor : MonoBehaviour
    {
        [SerializeField] private int uid = -1;
        [SerializeField] private uint hasMesh;
        [SerializeField] private int currentLOD = -1;
        [SerializeField] private int desireLOD = -1;
        [SerializeField] private int currentQueuedLOD = -1;

        private PlanetDirector director;
        private PlanetGridCoordinates coordinates;

        public int UID => uid;
        public uint HasMesh => hasMesh;
        public int CurrentLOD => currentLOD;
        public int DesireLOD => desireLOD;
        public int CurrentQueuedLOD => currentQueuedLOD;
        public PlanetGridCoordinates Coordinates => coordinates;

        public void SetUp(int uid, PlanetDirector director, PlanetGridCoordinates coordinates)
        {
            this.uid = uid;
            this.director = director;
            this.coordinates = coordinates;
            hasMesh = 1u;
            desireLOD = (int)PlanetChunkLod.LOD2;
            currentLOD = (int)PlanetChunkLod.LOD2;
            currentQueuedLOD = -1;
        }

        public void SetDesireLOD(int lod)
        {
            if (hasMesh == 0u || director == null)
            {
                return;
            }

            lod = Mathf.Clamp(lod, (int)PlanetChunkLod.LOD0, (int)PlanetChunkLod.LOD2);
            if (currentQueuedLOD != -1)
            {
                if (currentQueuedLOD == lod)
                {
                    return;
                }

                director.CancelQueuedLODChunk(uid);
                currentQueuedLOD = -1;
            }

            desireLOD = lod;
            if (currentLOD == desireLOD)
            {
                return;
            }

            int requestedLOD = desireLOD;
            currentQueuedLOD = requestedLOD;
            director.EnqueueLODChunk(uid, requestedLOD, () => CompleteQueuedLOD(requestedLOD));
        }

        private void CompleteQueuedLOD(int completedLOD)
        {
            if (currentQueuedLOD == completedLOD)
            {
                currentQueuedLOD = -1;
            }

            if (desireLOD == completedLOD)
            {
                currentLOD = completedLOD;
            }
        }
    }
}
