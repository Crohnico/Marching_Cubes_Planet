using MarchingCubesPlanet.Lab;
using NUnit.Framework;

namespace MarchingCubesPlanet.Lab.Tests
{
    public sealed class PlanetLabStressPresetTests
    {
        [Test]
        public void ValidPresetPassesValidation()
        {
            PlanetLabStressPreset preset = new PlanetLabStressPreset
            {
                cycleCount = 1,
                bufferElementCount = 1,
                dispatchRepeatCount = 1
            };

            Assert.IsTrue(preset.IsValid(out string message));
            Assert.AreEqual("Preset is valid.", message);
        }

        [Test]
        public void InvalidCycleCountFailsValidation()
        {
            PlanetLabStressPreset preset = new PlanetLabStressPreset
            {
                cycleCount = 0,
                bufferElementCount = 1,
                dispatchRepeatCount = 1
            };

            Assert.IsFalse(preset.IsValid(out string message));
            Assert.AreEqual("cycleCount must be greater than zero.", message);
        }
    }
}
