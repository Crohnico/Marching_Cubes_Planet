using System;
using UnityEngine;

namespace MarchingCubesPlanet.Coordinates
{
    [Serializable]
    public struct PlanetPlacement
    {
        [SerializeField] private UniversePosition planetStellarCenter;
        [SerializeField] private UniversePosition activeOrigin;
        [SerializeField] private Quaternion planetRotation;

        public PlanetPlacement(Vector3 planetWorldCenter)
            : this(UniversePosition.FromVector3(planetWorldCenter), UniversePosition.Zero, Quaternion.identity)
        {
        }

        public PlanetPlacement(UniversePosition planetStellarCenter, UniversePosition activeOrigin, Quaternion planetRotation)
        {
            this.planetStellarCenter = planetStellarCenter;
            this.activeOrigin = activeOrigin;
            this.planetRotation = NormalizeRotation(planetRotation);
        }

        public UniversePosition PlanetStellarCenter
        {
            get => planetStellarCenter;
            set => planetStellarCenter = value;
        }

        public UniversePosition ActiveOrigin
        {
            get => activeOrigin;
            set => activeOrigin = value;
        }

        public Quaternion PlanetRotation
        {
            get => NormalizeRotation(planetRotation);
            set => planetRotation = NormalizeRotation(value);
        }

        public Vector3 PlanetWorldCenter
        {
            get => planetStellarCenter - activeOrigin;
            set => planetStellarCenter = activeOrigin + value;
        }

        public static PlanetPlacement Default()
        {
            return new PlanetPlacement(Vector3.zero);
        }

        private static Quaternion NormalizeRotation(Quaternion rotation)
        {
            float magnitudeSquared =
                rotation.x * rotation.x +
                rotation.y * rotation.y +
                rotation.z * rotation.z +
                rotation.w * rotation.w;

            if (magnitudeSquared <= Mathf.Epsilon)
            {
                return Quaternion.identity;
            }

            float inverseMagnitude = 1f / Mathf.Sqrt(magnitudeSquared);
            return new Quaternion(
                rotation.x * inverseMagnitude,
                rotation.y * inverseMagnitude,
                rotation.z * inverseMagnitude,
                rotation.w * inverseMagnitude);
        }
    }
}
