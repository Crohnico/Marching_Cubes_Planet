using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using MarchingCubesPlanet.Coordinates;
using UnityEngine;

namespace MarchingCubesPlanet.MarchingCubes
{
    [DisallowMultipleComponent]
    public sealed class PlanetDirector : MonoBehaviour
    {
        [SerializeField] private PlanetRecipe recipe = PlanetRecipe.Default();
        [SerializeField] private PlanetPlacement placement = PlanetPlacement.Default();
        [SerializeField] private Transform player;
        [SerializeField] private float activationRange = 10000f;
        [SerializeField] private PlanetChunkLod shellLod = PlanetChunkLod.LOD2;
        [SerializeField] private PlanetChunkLod baseLod = PlanetChunkLod.LOD2;

        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private PlanetGpuMarchingCubesSurface gpuSurface;
        [SerializeField] private LODGestor lodGestor;
        private PlanetGrid grid;
        private readonly List<PlanetGridCoordinates> baseChunkCoordinates = new List<PlanetGridCoordinates>(256);
        private readonly List<ChunkLODGestor> chunkLodGestors = new List<ChunkLODGestor>(256);
        private readonly Dictionary<int, ChunkLODGestor> chunkLodGestorsByUid = new Dictionary<int, ChunkLODGestor>(256);
        private readonly List<QueuedLodChunk> queuedLodChunks = new List<QueuedLodChunk>(64);
        private int chunkLoadIndex = -1;
        private int queuedLodSequence;
        private int loadVersion;
        private bool isAlive;
        private bool isSetUp;
        private bool isBaseLoading;
        private bool isLodQueueRunning;
        private Transform chunkLodRoot;

        public PlanetGrid Grid => grid;
        public bool IsAlive => isAlive;
        public bool IsSetUp => isSetUp;
        public Transform Player => player;

        public PlanetRecipe Recipe
        {
            get => recipe;
            set
            {
                recipe = value;
                grid = null;
            }
        }

        public PlanetPlacement Placement => placement;

        private IEnumerator Start()
        {
            SyncPlacementFromTransform();
            gpuSurface = GetGpuSurface();
            lodGestor = GetLodGestor();
            GenerateGrid();

            if (player == null && Camera.main != null)
            {
                player = Camera.main.transform;
            }

            yield return new WaitForSeconds(0.1f);
        
            LoadShell(shellLod, () => isSetUp = false);
        }

        private void Update()
        {
            SyncPlacementFromTransform();
            gpuSurface.SetPlacement(placement);

            if (player == null)
            {
                return;
            }

            bool nextAlive = Vector3.Distance(player.position, transform.position) <= activationRange;
            if (nextAlive == isAlive)
            {
                return;
            }

            isAlive = nextAlive;
            if (isAlive && !isSetUp)
            {
                LoadBase(baseLod, () =>
                {
                    ReleaseShell();
                    isSetUp = true;
                    Debug.Log("BaseLoaded", this);
                });
                return;
            }

            if (!isAlive)
            {
                if (isBaseLoading)
                {
                    CancelBaseLoad();
                    ReleaseBase();
                    return;
                }

                if (isSetUp)
                {
                    LoadShell(shellLod, () =>
                    {
                        ReleaseBase();
                        isSetUp = false;
                    }, true);
                }
            }
        }

        public void LoadShell(PlanetChunkLod lod, Action onComplete = null, bool keepBaseVisibleUntilComplete = false)
        {
            loadVersion++;
            isBaseLoading = false;
            chunkLoadIndex = -1;
            CancelAllQueuedLODChunks();
            if (keepBaseVisibleUntilComplete && lodGestor != null)
            {
                lodGestor.ClearChunks();
            }

            ClearLegacyMesh();
            GetGpuSurface().GenerateShell(
                recipe,
                placement,
                lod,
                meshRenderer,
                keepBaseVisibleUntilComplete);
            onComplete?.Invoke();
        }

        public void LoadBase(PlanetChunkLod lod, Action onComplete = null)
        {
            if (grid == null)
            {
                GenerateGrid();
            }

            grid.CopyInformationCells(baseChunkCoordinates);
            int sequence = ++loadVersion;
            chunkLoadIndex = -1;
            isBaseLoading = true;
            CancelAllQueuedLODChunks();
            DestroyChunkLodObjects();
            CreateChunkLodObjects();
            GetGpuSurface().BeginChunkSequence(true);
            QueueChunkLoad(lod, sequence, onComplete);
        }

        public async void LoadChunk(PlanetChunkLod lod, PlanetGridCoordinates? chunkID = null, Action onComplete = null)
        {

            if (!chunkID.HasValue)
            {
                if (grid.InformationCount <= 0)
                {
                    onComplete?.Invoke();
                    return;
                }

                chunkID = grid.GetInfoCell(UnityEngine.Random.Range(0, grid.InformationCount));
            }

            ClearLegacyMesh();
            GetGpuSurface().BeginChunkSequence();
            GetGpuSurface().GenerateChunk(
                recipe,
                placement,
                lod,
                chunkID.Value,
                meshRenderer);
            await Task.Yield();
            onComplete?.Invoke();
        }

        public void ReleaseShell()
        {
            GetGpuSurface().ReleaseShell();
        }

        public void ReleaseBase()
        {
            CancelAllQueuedLODChunks();
            DestroyChunkLodObjects();
            GetGpuSurface().ReleaseChunkAggregate();
            isBaseLoading = false;
            chunkLoadIndex = -1;
            baseChunkCoordinates.Clear();
        }

        public PlanetChunkLod CalculateLODFromPlayerPosition()
        {
            return PlanetChunkLod.LOD2;
        }

        public bool EnqueueLODChunk(int uid, int desiredLod, Action onComplete = null)
        {
            if (!chunkLodGestorsByUid.ContainsKey(uid))
            {
                return false;
            }

            PlanetChunkLod lod = (PlanetChunkLod)Mathf.Clamp(
                desiredLod,
                (int)PlanetChunkLod.LOD0,
                (int)PlanetChunkLod.LOD2);
            for (int i = queuedLodChunks.Count - 1; i >= 0; i--)
            {
                if (queuedLodChunks[i].Uid != uid)
                {
                    continue;
                }

                if (queuedLodChunks[i].Lod == lod)
                {
                    return true;
                }

                queuedLodChunks.RemoveAt(i);
            }

            queuedLodChunks.Add(new QueuedLodChunk(uid, lod, queuedLodSequence++, onComplete));
            ProcessQueuedLODChunks();
            return true;
        }

        public bool CancelQueuedLODChunk(int uid)
        {
            bool removed = false;
            for (int i = queuedLodChunks.Count - 1; i >= 0; i--)
            {
                if (queuedLodChunks[i].Uid != uid)
                {
                    continue;
                }

                queuedLodChunks.RemoveAt(i);
                removed = true;
            }

            return removed;
        }

        private async void QueueChunkLoad(PlanetChunkLod lod, int sequence, Action onComplete)
        {
            if (sequence != loadVersion || !isBaseLoading || grid == null)
            {
                return;
            }

            chunkLoadIndex++;
            if (chunkLoadIndex >= baseChunkCoordinates.Count)
            {
                isBaseLoading = false;
                onComplete?.Invoke();
                GetLodGestor().SetChunks(chunkLodGestors, this, player);
                return;
            }

            PlanetGridCoordinates chunkID = baseChunkCoordinates[chunkLoadIndex];
            ClearLegacyMesh();
            GetGpuSurface().GenerateChunk(
                recipe,
                placement,
                lod,
                chunkID,
                meshRenderer);
            await Task.Yield();
            QueueChunkLoad(lod, sequence, onComplete);
        }

        private async void ProcessQueuedLODChunks()
        {
            if (isLodQueueRunning)
            {
                return;
            }

            isLodQueueRunning = true;
            await Task.Yield();
            while (queuedLodChunks.Count > 0 && isSetUp)
            {
                int requestIndex = FindBestQueuedLODChunkIndex();
                QueuedLodChunk request = queuedLodChunks[requestIndex];
                queuedLodChunks.RemoveAt(requestIndex);
                if (!chunkLodGestorsByUid.TryGetValue(request.Uid, out ChunkLODGestor chunk) || chunk == null)
                {
                    continue;
                }

                GetGpuSurface().GenerateChunk(
                    recipe,
                    placement,
                    request.Lod,
                    chunk.Coordinates,
                    meshRenderer);
                await Task.Yield();
                request.OnComplete?.Invoke();
            }

            isLodQueueRunning = false;
        }

        private int FindBestQueuedLODChunkIndex()
        {
            int bestIndex = 0;
            int bestLod = int.MaxValue;
            int bestSequence = int.MaxValue;
            for (int i = 0; i < queuedLodChunks.Count; i++)
            {
                QueuedLodChunk request = queuedLodChunks[i];
                int lod = (int)request.Lod;
                if (lod > bestLod)
                {
                    continue;
                }

                if (lod == bestLod && request.Sequence >= bestSequence)
                {
                    continue;
                }

                bestIndex = i;
                bestLod = lod;
                bestSequence = request.Sequence;
            }

            return bestIndex;
        }

        private void CancelBaseLoad()
        {
            loadVersion++;
            isBaseLoading = false;
            chunkLoadIndex = -1;
            baseChunkCoordinates.Clear();
        }

        private void GenerateGrid()
        {
            grid = PlanetGenerator.GenerateGrid(recipe);
            chunkLoadIndex = -1;
        }

        private void SyncPlacementFromTransform()
        {
            placement.PlanetWorldCenter = transform.position;
            placement.PlanetRotation = transform.rotation;
        }

        private void CreateChunkLodObject(int uid, PlanetGridCoordinates coordinates)
        {
            EnsureChunkLodRoot();
            float chunkSizeWorld = PlanetChunkLodUtility.GetChunkSizeForLod(PlanetChunkLod.LOD1) * recipe.WorldScale;
            GameObject chunkObject = new GameObject("ChunkLOD_" + uid + "_" + coordinates);
            chunkObject.hideFlags = HideFlags.HideInHierarchy;
            Transform chunkTransform = chunkObject.transform;
            chunkTransform.SetParent(chunkLodRoot, false);
            chunkTransform.localPosition = new Vector3(
                (coordinates.x + 0.5f) * chunkSizeWorld,
                (coordinates.y + 0.5f) * chunkSizeWorld,
                (coordinates.z + 0.5f) * chunkSizeWorld);

            ChunkLODGestor chunk = chunkObject.AddComponent<ChunkLODGestor>();
            chunk.SetUp(uid, this, coordinates);
            chunkLodGestors.Add(chunk);
            chunkLodGestorsByUid[uid] = chunk;
        }

        private void CreateChunkLodObjects()
        {
            for (int i = 0; i < baseChunkCoordinates.Count; i++)
            {
                CreateChunkLodObject(i, baseChunkCoordinates[i]);
            }
        }

        private void EnsureChunkLodRoot()
        {
            if (chunkLodRoot != null)
            {
                return;
            }

            GameObject root = new GameObject("ChunkLODGestors");
            root.hideFlags = HideFlags.HideInHierarchy;
            chunkLodRoot = root.transform;
            chunkLodRoot.SetParent(transform, false);
        }

        private void DestroyChunkLodObjects()
        {
            if (lodGestor != null)
            {
                lodGestor.ClearChunks();
            }

            chunkLodGestors.Clear();
            chunkLodGestorsByUid.Clear();
            if (chunkLodRoot == null)
            {
                return;
            }

            DestroyUnityObject(chunkLodRoot.gameObject);
            chunkLodRoot = null;
        }

        private void CancelAllQueuedLODChunks()
        {
            queuedLodChunks.Clear();
            queuedLodSequence = 0;
        }

        private MeshFilter GetMeshFilter()
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            return meshFilter;
        }

        private PlanetGpuMarchingCubesSurface GetGpuSurface()
        {
            if (gpuSurface == null) gpuSurface = GetComponent<PlanetGpuMarchingCubesSurface>();
            if (gpuSurface == null) gpuSurface = gameObject.AddComponent<PlanetGpuMarchingCubesSurface>();
            return gpuSurface;
        }

        private LODGestor GetLodGestor()
        {
            if (lodGestor != null)
            {
                return lodGestor;
            }

#if UNITY_2023_1_OR_NEWER
            lodGestor = FindFirstObjectByType<LODGestor>();
#else
            lodGestor = FindObjectOfType<LODGestor>();
#endif
            if (lodGestor != null)
            {
                return lodGestor;
            }

            GameObject lodGestorObject = new GameObject("LODGestor");
            lodGestor = lodGestorObject.AddComponent<LODGestor>();
            return lodGestor;
        }

        private void ClearLegacyMesh()
        {
            MeshFilter target = GetMeshFilter();
            if (target != null)
            {
                target.sharedMesh = null;
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.1f, 0.7f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, activationRange));
        }

        private static void DestroyUnityObject(UnityEngine.Object instance)
        {
            if (instance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(instance);
            }
            else
            {
                DestroyImmediate(instance);
            }
        }

        private readonly struct QueuedLodChunk
        {
            public QueuedLodChunk(int uid, PlanetChunkLod lod, int sequence, Action onComplete)
            {
                Uid = uid;
                Lod = lod;
                Sequence = sequence;
                OnComplete = onComplete;
            }

            public int Uid { get; }
            public PlanetChunkLod Lod { get; }
            public int Sequence { get; }
            public Action OnComplete { get; }
        }
    }
}
