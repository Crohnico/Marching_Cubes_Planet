using UnityEngine;

namespace MarchingCubesPlanet.TrianglePools
{
    [DisallowMultipleComponent]
    public sealed class PlanetLodAgent : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private Transform positionSource;
        [SerializeField] private Transform viewSource;

        [Header("Push Thresholds")]
        [SerializeField] private float minPositionDeltaMeters = 0.025f;
        [SerializeField] private float minViewAngleDeltaDegrees = 0.25f;
        [SerializeField] private float maxPushIntervalSeconds = 0.1f;

        [Header("State")]
        [SerializeField] private Vector3 lastPushedPositionWorld;
        [SerializeField] private Vector3 lastPushedForwardWorld = Vector3.forward;
        [SerializeField] private int pushVersion;

        private float lastPushTime;
        private bool hasPushed;

        public Vector3 PositionWorld => ResolvePositionSource().position;
        public Vector3 ForwardWorld => ResolveViewSource().forward.normalized;
        public int PushVersion => pushVersion;

        private void OnEnable()
        {
            PlanetTrianglePoolRegistry.RegisterLodAgent(this);
            ForcePush();
        }

        private void LateUpdate()
        {
            PushIfNeeded();
        }

        private void OnDisable()
        {
            PlanetTrianglePoolRegistry.UnregisterLodAgent(this);
        }

        public void SetSources(Transform position, Transform view)
        {
            positionSource = position;
            viewSource = view;
            ForcePush();
        }

        public void ForcePush()
        {
            PushSnapshot(PositionWorld, ForwardWorld);
        }

        public bool PushIfNeeded()
        {
            Vector3 positionWorld = PositionWorld;
            Vector3 forwardWorld = ForwardWorld;

            if (!hasPushed ||
                HasMovedEnough(positionWorld) ||
                HasRotatedEnough(forwardWorld) ||
                Time.unscaledTime - lastPushTime >= Mathf.Max(0.001f, maxPushIntervalSeconds))
            {
                PushSnapshot(positionWorld, forwardWorld);
                return true;
            }

            return false;
        }

        private bool HasMovedEnough(Vector3 positionWorld)
        {
            float threshold = Mathf.Max(0f, minPositionDeltaMeters);
            return (positionWorld - lastPushedPositionWorld).sqrMagnitude >= threshold * threshold;
        }

        private bool HasRotatedEnough(Vector3 forwardWorld)
        {
            float threshold = Mathf.Max(0f, minViewAngleDeltaDegrees);
            return Vector3.Angle(lastPushedForwardWorld, forwardWorld) >= threshold;
        }

        private void PushSnapshot(Vector3 positionWorld, Vector3 forwardWorld)
        {
            if (forwardWorld.sqrMagnitude <= 0.0001f)
            {
                forwardWorld = Vector3.forward;
            }

            lastPushedPositionWorld = positionWorld;
            lastPushedForwardWorld = forwardWorld.normalized;
            lastPushTime = Time.unscaledTime;
            hasPushed = true;
            pushVersion++;
            PlanetTrianglePoolRegistry.PushLodAgentView(this, lastPushedPositionWorld, lastPushedForwardWorld, pushVersion);
        }

        private Transform ResolvePositionSource()
        {
            return positionSource != null ? positionSource : transform;
        }

        private Transform ResolveViewSource()
        {
            return viewSource != null ? viewSource : transform;
        }
    }
}
