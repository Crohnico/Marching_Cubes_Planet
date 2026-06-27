using System;
using UnityEngine;

namespace MarchingCubesPlanet.Lab
{
    [Serializable]
    public struct PlanetLabStressPreset
    {
        public string presetName;

        [TextArea]
        public string whatItTests;

        [TextArea]
        public string expectedResult;

        [TextArea]
        public string riskCovered;

        [Min(1)]
        public int cycleCount;

        [Min(1)]
        public int bufferElementCount;

        [Min(1)]
        public int dispatchRepeatCount;

        [Min(0)]
        public int trianglePayloadLimit;

        public bool releaseBetweenCycles;
        public bool captureMetricsEachCycle;

        public bool IsValid(out string message)
        {
            if (cycleCount <= 0)
            {
                message = "cycleCount must be greater than zero.";
                return false;
            }

            if (bufferElementCount <= 0)
            {
                message = "bufferElementCount must be greater than zero.";
                return false;
            }

            if (dispatchRepeatCount <= 0)
            {
                message = "dispatchRepeatCount must be greater than zero.";
                return false;
            }

            message = "Preset is valid.";
            return true;
        }
    }
}
