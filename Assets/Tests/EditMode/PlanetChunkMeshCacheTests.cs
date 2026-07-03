using System;
using System.Collections.Generic;
using System.IO;
using MarchingCubesPlanet.Coordinates;
using MarchingCubesPlanet.MarchingCubes;
using NUnit.Framework;
using UnityEngine;

namespace MarchingCubesPlanet.Lab.Tests
{
    public sealed class PlanetChunkMeshCacheTests
    {
        private readonly List<string> createdRoots = new List<string>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < createdRoots.Count; i++)
            {
                if (Directory.Exists(createdRoots[i]))
                {
                    Directory.Delete(createdRoots[i], true);
                }
            }

            createdRoots.Clear();
        }

        [Test]
        public void PrepareCreatesPlanetIdAndChunksFolder()
        {
            string root = CreateTempRoot();
            PlanetChunkMeshCache cache = new PlanetChunkMeshCache(root);

            Assert.IsTrue(cache.Prepare(PlanetRecipe.Default()), cache.LastDiagnostic);

            Assert.IsTrue(File.Exists(Path.Combine(root, PlanetChunkMeshCache.PlanetIdFileName)));
            Assert.IsTrue(Directory.Exists(Path.Combine(root, PlanetChunkMeshCache.ChunksFolderName)));
            StringAssert.StartsWith("planet_", cache.PlanetId);
        }

        [Test]
        public void PrepareInvalidatesChunksWhenRecipeChanges()
        {
            string root = CreateTempRoot();
            PlanetChunkMeshCache cache = new PlanetChunkMeshCache(root);
            PlanetRecipe recipe = PlanetRecipe.Default();
            Assert.IsTrue(cache.Prepare(recipe), cache.LastDiagnostic);

            string lodDirectory = cache.GetChunkLodDirectory(3, 2);
            Directory.CreateDirectory(lodDirectory);
            string staleFile = Path.Combine(lodDirectory, PlanetChunkMeshCache.MeshFileName);
            File.WriteAllText(staleFile, "stale");

            recipe.Seed += 1;
            Assert.IsTrue(cache.Prepare(recipe), cache.LastDiagnostic);

            Assert.IsFalse(File.Exists(staleFile));
        }

        [Test]
        public void SaveAndLoadRoundTripsSurfaceAndWaterMeshes()
        {
            string root = CreateTempRoot();
            PlanetChunkMeshCache cache = new PlanetChunkMeshCache(root);
            Assert.IsTrue(cache.Prepare(PlanetRecipe.Default()), cache.LastDiagnostic);

            Mesh surfaceMesh = CreateTriangleMesh("surface", 0f);
            Mesh waterMesh = CreateTriangleMesh("water", -0.25f);

            Assert.IsTrue(cache.SaveChunk(7, 2, surfaceMesh, waterMesh), cache.LastDiagnostic);

            string lodDirectory = cache.GetChunkLodDirectory(7, 2);
            Assert.IsTrue(File.Exists(Path.Combine(lodDirectory, PlanetChunkMeshCache.MeshFileName)));
            Assert.IsTrue(File.Exists(Path.Combine(lodDirectory, PlanetChunkMeshCache.ChunkDataFileName)));
            Assert.IsTrue(File.Exists(Path.Combine(lodDirectory, PlanetChunkMeshCache.WaterMeshFileName)));
            Assert.IsTrue(File.Exists(Path.Combine(lodDirectory, PlanetChunkMeshCache.WaterDataFileName)));

            List<PlanetCachedChunkMesh> loadedChunks = new List<PlanetCachedChunkMesh>();
            Assert.IsTrue(cache.TryLoadAllChunkMeshes(2, loadedChunks), cache.LastDiagnostic);

            Assert.AreEqual(1, loadedChunks.Count);
            Assert.AreEqual(7, loadedChunks[0].ChunkId);
            Assert.AreEqual(2, loadedChunks[0].Lod);
            Assert.AreEqual(3, loadedChunks[0].SurfaceVertexCount);
            Assert.AreEqual(1, loadedChunks[0].SurfaceTriangleCount);
            Assert.AreEqual(3, loadedChunks[0].WaterVertexCount);
            Assert.AreEqual(1, loadedChunks[0].WaterTriangleCount);
            Assert.AreEqual(new Vector3(0f, 0f, 0f), loadedChunks[0].SurfaceMesh.vertices[0]);
            Assert.AreEqual(new Vector3(0f, -0.25f, 0f), loadedChunks[0].WaterMesh.vertices[0]);

            loadedChunks[0].ReleaseMeshes();
            UnityEngine.Object.DestroyImmediate(surfaceMesh);
            UnityEngine.Object.DestroyImmediate(waterMesh);
        }

        private string CreateTempRoot()
        {
            string root = Path.Combine("Temp", "PlanetChunkMeshCacheTests", Guid.NewGuid().ToString("N"));
            createdRoots.Add(root);
            return root;
        }

        private static Mesh CreateTriangleMesh(string name, float yOffset)
        {
            Mesh mesh = new Mesh
            {
                name = name
            };
            mesh.vertices = new[]
            {
                new Vector3(0f, yOffset, 0f),
                new Vector3(1f, yOffset, 0f),
                new Vector3(0f, yOffset, 1f)
            };
            mesh.normals = new[]
            {
                Vector3.up,
                Vector3.up,
                Vector3.up
            };
            mesh.uv = new[]
            {
                Vector2.zero,
                Vector2.right,
                Vector2.up
            };
            mesh.colors32 = new[]
            {
                new Color32(255, 0, 0, 255),
                new Color32(0, 255, 0, 255),
                new Color32(0, 0, 255, 255)
            };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
