using System;
using UnityEngine;

namespace MarchingCubesPlanet.Lab
{
    public abstract class PlanetLabModule : MonoBehaviour
    {
        public abstract string ModuleName { get; }
        public abstract bool HasLiveResources { get; }

        public abstract void InitModule();
        public abstract void ReleaseModule();
        public abstract bool ValidateModule();
        public abstract PlanetLabMetricsSnapshot CaptureMetrics();

        public virtual void RunModuleTest()
        {
            if (!ValidateModule())
            {
                throw new InvalidOperationException(ModuleName + " validation failed.");
            }

            InitModule();
            CaptureMetrics();
        }

        public virtual void RunModuleStress()
        {
            for (int i = 0; i < 3; i++)
            {
                InitModule();
                ReleaseModule();
            }

            CaptureMetrics();
        }
    }
}
