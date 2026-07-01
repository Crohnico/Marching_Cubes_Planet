using UnityEngine;

namespace MarchingCubesPlanet.TrianglePools
{
    [DisallowMultipleComponent]
    public sealed class PlanetTriangleDistanceReference : MonoBehaviour
    {
        public Vector3 Position => transform.position;
        public Vector3 Forward => transform.forward.normalized;

        private int pushVersion;

        private void OnEnable()
        {
            PlanetTrianglePoolRegistry.RegisterDistanceReference(this);
            Push();
        }

        private void LateUpdate()
        {
            Push();
        }

        private void OnDisable()
        {
            PlanetTrianglePoolRegistry.UnregisterDistanceReference(this);
        }

        private void Push()
        {
            pushVersion++;
            PlanetTrianglePoolRegistry.PushDistanceReferenceView(this, Position, Forward, pushVersion);
        }
    }
}
