using System.Collections.Generic;
using MarchingCubesPlanet.Compute;
using MarchingCubesPlanet.Coordinates;
using MarchingCubesPlanet.Shape;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MarchingCubesPlanet.Tests
{
    public sealed class PlanetCaveSystemTests
    {
        private const string ShapeShaderPath = "Assets/Shaders/Resources/Compute/PlanetShapeDensity.compute";

        [Test]
        public void CaveDensityIsDeterministicAndPorosityZeroLeavesShapeUntouched()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders are required for the cave density contract.");
            }

            PlanetRecipe baseRecipe = BuildControlledRecipe();
            Vector4[] samples = BuildSamples();

            PlanetCaveSettings disabledCaves = baseRecipe.CaveSystem;
            disabledCaves.Enabled = false;
            baseRecipe.CaveSystem = disabledCaves;
            Vector4[] disabledResults = Evaluate(in baseRecipe, samples);

            PlanetRecipe zeroPorosityRecipe = baseRecipe;
            PlanetCaveSettings zeroPorosityCaves = zeroPorosityRecipe.CaveSystem;
            zeroPorosityCaves.Enabled = true;
            zeroPorosityCaves.Porosity = 0;
            zeroPorosityRecipe.CaveSystem = zeroPorosityCaves;
            Vector4[] zeroPorosityResults = Evaluate(in zeroPorosityRecipe, samples);

            for (int i = 0; i < samples.Length; i++)
            {
                Assert.AreEqual(disabledResults[i].x, zeroPorosityResults[i].x, 0.00001f);
            }

            PlanetRecipe caveRecipe = BuildControlledRecipe();
            Vector4[] first = Evaluate(in caveRecipe, samples);
            Vector4[] second = Evaluate(in caveRecipe, samples);
            bool carvedAtLeastOneSample = false;
            for (int i = 0; i < samples.Length; i++)
            {
                Assert.AreEqual(first[i].x, second[i].x, 0.00001f);
                carvedAtLeastOneSample |= first[i].x < disabledResults[i].x - 0.01f;
            }

            Assert.IsTrue(carvedAtLeastOneSample, "The forced cave recipe did not excavate any controlled sample.");

            PlanetCaveSettings changedSeedCaves = caveRecipe.CaveSystem;
            changedSeedCaves.SeedOffset += 101;
            caveRecipe.CaveSystem = changedSeedCaves;
            Vector4[] changedSeed = Evaluate(in caveRecipe, samples);
            bool changedAtLeastOneSample = false;
            for (int i = 0; i < samples.Length; i++)
            {
                changedAtLeastOneSample |= Mathf.Abs(first[i].x - changedSeed[i].x) > 0.01f;
            }

            Assert.IsTrue(changedAtLeastOneSample, "Changing the cave seed did not change the controlled cave field.");
        }

        [TestCase(0, TestName = "Cavern field can carve independently")]
        [TestCase(1, TestName = "Passage field can carve independently")]
        [TestCase(2, TestName = "Fracture field can carve independently")]
        public void CaveFamiliesCanCarveIndependently(int activeFamily)
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders are required for the cave density contract.");
            }

            PlanetRecipe recipe = BuildControlledRecipe();
            PlanetCaveSettings caves = recipe.CaveSystem;
            caves.CavernAbundance = activeFamily == 0 ? 100 : 0;
            caves.PassageAbundance = activeFamily == 1 ? 100 : 0;
            caves.FractureAbundance = activeFamily == 2 ? 100 : 0;
            caves.WallDetail = 0;
            recipe.CaveSystem = caves;

            Vector4[] samples = BuildSamples();
            PlanetRecipe solidRecipe = recipe;
            PlanetCaveSettings solidCaves = solidRecipe.CaveSystem;
            solidCaves.Enabled = false;
            solidRecipe.CaveSystem = solidCaves;
            Vector4[] solidResults = Evaluate(in solidRecipe, samples);
            Vector4[] caveResults = Evaluate(in recipe, samples);

            bool carvedAtLeastOneSample = false;
            for (int i = 0; i < samples.Length; i++)
            {
                carvedAtLeastOneSample |= caveResults[i].x < solidResults[i].x - 0.01f;
            }

            Assert.IsTrue(carvedAtLeastOneSample, "The selected cave field did not excavate any controlled sample.");
        }

        private static PlanetRecipe BuildControlledRecipe()
        {
            PlanetRecipe recipe = PlanetRecipe.Default();
            recipe.GridRadius = 96;
            recipe.WorldScale = 1f;
            recipe.VoronoiDivision = 8;
            recipe.ContinentCells = 8;
            recipe.MountainBiomeCells = 0;
            recipe.SurfaceNoiseAmplitude = 0f;
            recipe.MinLandElevation = 0.01f;
            recipe.MaxLandElevation = 0.01f;
            recipe.MinHeightModifier = 1f;
            recipe.MaxHeightModifier = 1f;

            PlanetCaveSettings caves = recipe.CaveSystem;
            caves.Enabled = true;
            caves.MinAppearance = 0;
            caves.MaxAppearance = 255;
            caves.Porosity = 100;
            caves.Connectivity = 100;
            caves.CavernScale = 28f;
            caves.PassageScale = 12f;
            caves.Tortuosity = 75;
            caves.CavernAbundance = 100;
            caves.PassageAbundance = 100;
            caves.FractureAbundance = 100;
            caves.EntranceAbundance = 100;
            caves.WallDetail = 45;
            recipe.CaveSystem = caves;
            return recipe;
        }

        private static Vector4[] BuildSamples()
        {
            List<Vector4> samples = new List<Vector4>(2197);
            for (int z = -72; z <= 72; z += 12)
            {
                for (int y = -72; y <= 72; y += 12)
                {
                    for (int x = -72; x <= 72; x += 12)
                    {
                        samples.Add(new Vector4(x, y, z, 0f));
                    }
                }
            }

            return samples.ToArray();
        }

        private static Vector4[] Evaluate(in PlanetRecipe recipe, Vector4[] samples)
        {
            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShapeShaderPath);
            Assert.IsNotNull(shader);
            PlanetGpuShapeCell[] cells = new PlanetGpuShapeCell[recipe.VoronoiDivision];
            PlanetGpuShapeCellBuilder.Build(in recipe, cells);
            Vector4[] results = new Vector4[samples.Length];
            PlanetGpuShapeEvaluator evaluator = new PlanetGpuShapeEvaluator();
            try
            {
                evaluator.Initialize(shader, in recipe, cells, PlanetGpuBufferMode.ComputeBuffer);
                evaluator.EvaluateDensitySamples(samples, results);
                return results;
            }
            finally
            {
                evaluator.Release();
            }
        }
    }
}
