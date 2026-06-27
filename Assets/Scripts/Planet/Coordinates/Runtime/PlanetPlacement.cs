using System;
using UnityEngine;

namespace MarchingCubesPlanet.Coordinates
{
    [Serializable]
    public struct PlanetPlacement
    {
        [SerializeField] private Vector3 planetWorldCenter;

        public PlanetPlacement(Vector3 planetWorldCenter)
        {
            this.planetWorldCenter = planetWorldCenter;
        }

        public Vector3 PlanetWorldCenter
        {
            get => planetWorldCenter;
            set => planetWorldCenter = value;
        }

        public static PlanetPlacement Default()
        {
            return new PlanetPlacement(Vector3.zero);
        }
    }
}
