using MarchingCubesPlanet.Coordinates;
using MarchingCubesPlanet.MarchingCubes;
using NUnit.Framework;
using UnityEngine;

namespace MarchingCubesPlanet.Tests
{
    public sealed class PlanetChunkLodTests
    {
        [Test]
        public void LodRecipesKeepWorldRadiusFromLod1Base()
        {
            PlanetRecipe lod1 = PlanetRecipe.Default();
            lod1.GridRadius = 130;
            lod1.WorldScale = 61.5f;

            PlanetRecipe lod0 = PlanetChunkLodUtility.BuildRecipeForLod(in lod1, PlanetChunkLod.LOD0);
            PlanetRecipe lod2 = PlanetChunkLodUtility.BuildRecipeForLod(in lod1, PlanetChunkLod.LOD2);

            Assert.AreEqual(260, lod0.GridRadius);
            Assert.AreEqual(30.75f, lod0.WorldScale);
            Assert.AreEqual(130, lod1.GridRadius);
            Assert.AreEqual(61.5f, lod1.WorldScale);
            Assert.AreEqual(65, lod2.GridRadius);
            Assert.AreEqual(123f, lod2.WorldScale);
            Assert.AreEqual(lod1.WorldRadius, lod0.WorldRadius);
            Assert.AreEqual(lod1.WorldRadius, lod2.WorldRadius);
        }

        [Test]
        public void DefaultChunkSizesUseCanonicalLod1Size()
        {
            Assert.AreEqual(8, PlanetChunkLodUtility.GetChunkSizeForLod(PlanetChunkLod.LOD2));
            Assert.AreEqual(16, PlanetChunkLodUtility.GetChunkSizeForLod(PlanetChunkLod.LOD1));
            Assert.AreEqual(32, PlanetChunkLodUtility.GetChunkSizeForLod(PlanetChunkLod.LOD0));
        }

        [Test]
        public void LodRecipesKeepCaveScaleConstantInWorldSpace()
        {
            PlanetRecipe lod1 = PlanetRecipe.Default();
            lod1.GridRadius = 1000;
            lod1.WorldScale = 4f;
            PlanetCaveSettings caves = lod1.CaveSystem;
            caves.PassageScale = 20f;
            caves.CavernScale = 80f;
            lod1.CaveSystem = caves;

            PlanetRecipe lod0 = PlanetChunkLodUtility.BuildRecipeForLod(in lod1, PlanetChunkLod.LOD0);
            PlanetRecipe lod2 = PlanetChunkLodUtility.BuildRecipeForLod(in lod1, PlanetChunkLod.LOD2);

            Assert.AreEqual(40f, lod0.CaveSystem.PassageScale);
            Assert.AreEqual(160f, lod0.CaveSystem.CavernScale);
            Assert.AreEqual(10f, lod2.CaveSystem.PassageScale);
            Assert.AreEqual(40f, lod2.CaveSystem.CavernScale);
            Assert.AreEqual(lod1.CaveSystem.PassageScale * lod1.WorldScale, lod0.CaveSystem.PassageScale * lod0.WorldScale);
            Assert.AreEqual(lod1.CaveSystem.CavernScale * lod1.WorldScale, lod2.CaveSystem.CavernScale * lod2.WorldScale);
        }

        [Test]
        public void ActivationConfigOverridesCanonicalChunkSize()
        {
            PlanetChunkLodActivationConfig config = PlanetChunkLodActivationConfig.Default();
            config.canonicalChunkSize = 32;
            config.EnsureValid();

            Assert.AreEqual(16, PlanetChunkLodUtility.GetChunkSizeForLod(PlanetChunkLod.LOD2, in config));
            Assert.AreEqual(32, PlanetChunkLodUtility.GetChunkSizeForLod(PlanetChunkLod.LOD1, in config));
            Assert.AreEqual(64, PlanetChunkLodUtility.GetChunkSizeForLod(PlanetChunkLod.LOD0, in config));
        }

        [Test]
        public void DistanceThresholdsAssignDesiredLod()
        {
            PlanetChunkLodResolutionContext context = new PlanetChunkLodResolutionContext(
                Vector3.zero,
                10f,
                PlanetChunkLodActivationConfig.Default());

            Assert.AreEqual(PlanetChunkLod.LOD1, PlanetChunkLodResolver.ResolveDesiredLod(899f, in context));
            Assert.AreEqual(PlanetChunkLod.LOD1, PlanetChunkLodResolver.ResolveDesiredLod(1799f, in context));
            Assert.AreEqual(PlanetChunkLod.LOD2, PlanetChunkLodResolver.ResolveDesiredLod(1800f, in context));
        }

        [Test]
        public void FallbackLod2UsesImmediateShellPackage()
        {
            PlanetChunkWorkPackageSummary summary = PlanetChunkWorkPackagePlanner.BuildSummary(2, 17);

            Assert.AreEqual(PlanetChunkWorkPackageMode.ImmediateLod2Shell, summary.Mode);
            Assert.IsTrue(summary.IsImmediateShell);
            Assert.AreEqual(17, summary.PackageSize);
            Assert.AreEqual(1, summary.PackageCount);
        }

        [Test]
        public void RefinementLodsUseSmallPackages()
        {
            PlanetChunkWorkPackageSummary summary = PlanetChunkWorkPackagePlanner.BuildSummary(0, 17);

            Assert.AreEqual(PlanetChunkWorkPackageMode.RefinementPackages, summary.Mode);
            Assert.IsFalse(summary.IsImmediateShell);
            Assert.AreEqual(PlanetChunkWorkPackagePlanner.DefaultRefinementPackageSize, summary.PackageSize);
            Assert.AreEqual(3, summary.PackageCount);
        }

        [Test]
        public void RuntimeEntryTracksRequestedLodUntilInitialized()
        {
            PlanetChunkLodRuntimeEntry entry = new PlanetChunkLodRuntimeEntry(37, Vector3.zero);

            Assert.IsTrue(entry.NeedsLodRequest);
            Assert.AreEqual(PlanetChunkLodRuntimeEntry.NoRequestedLod, entry.RequestedLod);

            entry.MarkRequested(PlanetChunkLod.LOD1);

            Assert.IsTrue(entry.HasRequestedLod);
            Assert.AreEqual((int)PlanetChunkLod.LOD1, entry.RequestedLod);

            entry.MarkInitialized(PlanetChunkLod.LOD1);

            Assert.IsFalse(entry.HasRequestedLod);
            Assert.AreEqual((int)PlanetChunkLod.LOD1, entry.CurrentLod);
        }

        [Test]
        public void RuntimeRequestQueuePrioritizesLowerLodIndex()
        {
            PlanetChunkLodRequestQueue queue = new PlanetChunkLodRequestQueue();

            queue.Enqueue(2, PlanetChunkLod.LOD2);
            queue.Enqueue(1, PlanetChunkLod.LOD1);
            queue.Enqueue(0, PlanetChunkLod.LOD0);

            Assert.IsTrue(queue.TryDequeue(out PlanetChunkLodRequest first));
            Assert.AreEqual(0, first.EntryIndex);
            Assert.AreEqual(PlanetChunkLod.LOD0, first.RequestedLod);

            Assert.IsTrue(queue.TryDequeue(out PlanetChunkLodRequest second));
            Assert.AreEqual(1, second.EntryIndex);
            Assert.AreEqual(PlanetChunkLod.LOD1, second.RequestedLod);

            Assert.IsTrue(queue.TryDequeue(out PlanetChunkLodRequest third));
            Assert.AreEqual(2, third.EntryIndex);
            Assert.AreEqual(PlanetChunkLod.LOD2, third.RequestedLod);
        }

        [Test]
        public void PlanetGridStoresInformationByChunkCoordinates()
        {
            PlanetGrid grid = new PlanetGrid(2);
            PlanetGridCoordinates chunk = new PlanetGridCoordinates(4, -2, 7);

            Assert.IsFalse(grid.HasInformation(chunk));
            Assert.AreEqual(0u, grid[chunk]);

            grid.Set(chunk, 1u);

            Assert.IsTrue(grid.HasInformation(chunk));
            Assert.AreEqual(1u, grid[chunk]);
            Assert.AreEqual(1, grid.InformationCount);

            grid.Set(chunk, 0u);

            Assert.IsFalse(grid.HasInformation(chunk));
            Assert.AreEqual(0u, grid[chunk]);
            Assert.AreEqual(0, grid.InformationCount);
        }

        [Test]
        public void TopologyDebugBuildsOneComponentForAnAirChunk()
        {
            Vector4[] density = new Vector4[PlanetChunkTopologyDebugData.CellCount];
            for (int index = 0; index < density.Length; index++)
            {
                density[index] = new Vector4(-1f, 0f, 0f, 0f);
            }

            PlanetChunkTopologyDebugData topology = PlanetChunkTopologyDebugBuilder.Build(
                new PlanetGridCoordinates(0, 0, 0),
                density,
                0f);

            Assert.That(topology.IsCompletelyAir, Is.True);
            Assert.That(topology.ContainsSurface, Is.False);
            Assert.That(topology.ComponentCount, Is.EqualTo(1));
            for (int face = 0; face < 6; face++)
            {
                Assert.That(topology.GetBoundaryComponent((PlanetChunkFace)face, 8, 8), Is.EqualTo(0));
            }
        }

        [Test]
        public void TopologyDebugKeepsSeparatedFaceGroupsInDifferentComponents()
        {
            Vector4[] density = new Vector4[PlanetChunkTopologyDebugData.CellCount];
            for (int index = 0; index < density.Length; index++)
            {
                density[index] = new Vector4(-1f, 0f, 0f, 0f);
            }

            for (int z = 0; z < PlanetChunkTopologyDebugData.ChunkSize; z++)
            {
                for (int y = 0; y < PlanetChunkTopologyDebugData.ChunkSize; y++)
                {
                    int wall = CellIndex(8, y, z);
                    density[wall] = new Vector4(1f, 0f, 0f, 0f);
                }
            }

            PlanetChunkTopologyDebugData topology = PlanetChunkTopologyDebugBuilder.Build(
                new PlanetGridCoordinates(0, 0, 0),
                density,
                0f);

            int negativeX = topology.GetBoundaryComponent(PlanetChunkFace.NegativeX, 8, 8);
            int positiveX = topology.GetBoundaryComponent(PlanetChunkFace.PositiveX, 8, 8);
            Assert.That(topology.ComponentCount, Is.EqualTo(2));
            Assert.That(topology.ContainsSurface, Is.True);
            Assert.That(negativeX, Is.GreaterThanOrEqualTo(0));
            Assert.That(positiveX, Is.GreaterThanOrEqualTo(0));
            Assert.That(negativeX, Is.Not.EqualTo(positiveX));
        }

        [Test]
        public void TopologyDebugDoesNotConnectDiagonalAirCells()
        {
            Vector4[] density = new Vector4[PlanetChunkTopologyDebugData.CellCount];
            for (int index = 0; index < density.Length; index++)
            {
                density[index] = new Vector4(1f, 0f, 0f, 0f);
            }

            density[CellIndex(0, 0, 0)] = new Vector4(-1f, 0f, 0f, 0f);
            density[CellIndex(1, 1, 0)] = new Vector4(-1f, 0f, 0f, 0f);

            PlanetChunkTopologyDebugData topology = PlanetChunkTopologyDebugBuilder.Build(
                new PlanetGridCoordinates(0, 0, 0),
                density,
                0f);

            Assert.That(topology.ComponentCount, Is.EqualTo(2));
        }

        [Test]
        public void TopologyDebugCachesOneBidirectionalLinkForAnOpenSharedFace()
        {
            Vector4[] density = new Vector4[PlanetChunkTopologyDebugData.CellCount];
            for (int index = 0; index < density.Length; index++)
            {
                density[index] = new Vector4(-1f, 0f, 0f, 0f);
            }

            PlanetChunkTopologyDebugData left = PlanetChunkTopologyDebugBuilder.Build(
                new PlanetGridCoordinates(0, 0, 0),
                density,
                0f);
            PlanetChunkTopologyDebugData right = PlanetChunkTopologyDebugBuilder.Build(
                new PlanetGridCoordinates(1, 0, 0),
                density,
                0f);

            PlanetChunkTopologyDebugLinker.LinkOppositeFaces(
                left,
                right,
                PlanetChunkFace.PositiveX);

            Assert.That(left.GetLinkCount(0), Is.EqualTo(1));
            Assert.That(right.GetLinkCount(0), Is.EqualTo(1));
        }

        private static int CellIndex(int x, int y, int z)
        {
            int size = PlanetChunkTopologyDebugData.ChunkSize;
            return x + size * (y + size * z);
        }
    }
}
