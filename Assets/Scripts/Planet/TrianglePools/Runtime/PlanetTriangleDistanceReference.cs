using UnityEngine;

namespace MarchingCubesPlanet.TrianglePools
{
    [DisallowMultipleComponent]
    public sealed class PlanetTriangleDistanceReference : MonoBehaviour
    {
        public Vector3 Position => transform.position;

        private void OnEnable()
        {
            PlanetTrianglePoolRegistry.RegisterDistanceReference(this);
        }

        private void OnDisable()
        {
            PlanetTrianglePoolRegistry.UnregisterDistanceReference(this);
        }
    }
}
