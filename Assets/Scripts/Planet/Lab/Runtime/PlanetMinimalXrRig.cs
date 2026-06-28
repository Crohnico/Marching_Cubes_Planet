using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;

namespace MarchingCubesPlanet.Lab
{
    public sealed class PlanetMinimalXrRig : MonoBehaviour
    {
        [Header("Tracked Objects")]
        [SerializeField] private Transform head;
        [SerializeField] private Camera headCamera;
        [SerializeField] private Transform leftHandMarker;
        [SerializeField] private Transform rightHandMarker;
        [SerializeField] private LineRenderer leftRay;
        [SerializeField] private LineRenderer rightRay;

        [Header("Ray")]
        [SerializeField] private float maxRayDistance = 8f;
        [SerializeField] private Vector3 rayLocalEulerOffset;
        [SerializeField] private LayerMask physicsBlockers = ~0;
        [SerializeField] private bool usePhysicsBlockers;
        [SerializeField] private float triggerThreshold = 0.65f;

        [Header("UI")]
        [SerializeField] private Camera uiEventCamera;

        [Header("Diagnostics")]
        [SerializeField] private bool logStartupStatus = true;

        private readonly HandPointer leftPointer = new HandPointer(XRNode.LeftHand, -101);
        private readonly HandPointer rightPointer = new HandPointer(XRNode.RightHand, -102);

        private static readonly List<GraphicRaycaster> Raycasters = new List<GraphicRaycaster>(16);
        private static readonly List<InputDevice> StartupDevices = new List<InputDevice>(16);
        private static readonly StringBuilder StartupLogBuilder = new StringBuilder(512);

        public Transform Head => head;
        public Transform LeftHandMarker => leftHandMarker;
        public Transform RightHandMarker => rightHandMarker;

        private void Awake()
        {
            int disabledLegacyInputModules = DisableLegacyStandaloneInputModules();

            if (uiEventCamera == null)
            {
                uiEventCamera = headCamera;
            }

            leftPointer.Bind(leftHandMarker, leftRay);
            rightPointer.Bind(rightHandMarker, rightRay);

            if (logStartupStatus)
            {
                LogStartupStatus(disabledLegacyInputModules);
            }
        }

        private static int DisableLegacyStandaloneInputModules()
        {
#if UNITY_2023_1_OR_NEWER
            StandaloneInputModule[] inputModules = FindObjectsByType<StandaloneInputModule>(FindObjectsSortMode.None);
#else
            StandaloneInputModule[] inputModules = FindObjectsOfType<StandaloneInputModule>();
#endif

            int disabledCount = 0;
            for (int i = 0; i < inputModules.Length; i++)
            {
                if (inputModules[i].enabled)
                {
                    inputModules[i].enabled = false;
                    disabledCount++;
                }
            }

            return disabledCount;
        }

        private void LogStartupStatus(int disabledLegacyInputModules)
        {
            StartupDevices.Clear();
            InputDevices.GetDevices(StartupDevices);

            StartupLogBuilder.Clear();
            StartupLogBuilder.Append("PlanetMinimalXrRig startup");
            StartupLogBuilder.Append(" | rig=");
            StartupLogBuilder.Append(name);
            StartupLogBuilder.Append(" | camera=");
            StartupLogBuilder.Append(headCamera != null ? headCamera.name : "null");
            StartupLogBuilder.Append(" | uiEventCamera=");
            StartupLogBuilder.Append(uiEventCamera != null ? uiEventCamera.name : "null");
            StartupLogBuilder.Append(" | eventSystem=");
            StartupLogBuilder.Append(EventSystem.current != null ? EventSystem.current.name : "null");
            StartupLogBuilder.Append(" | disabledStandaloneInputModules=");
            StartupLogBuilder.Append(disabledLegacyInputModules);
            StartupLogBuilder.Append(" | xrDevices=");
            StartupLogBuilder.Append(StartupDevices.Count);

            for (int i = 0; i < StartupDevices.Count; i++)
            {
                InputDevice device = StartupDevices[i];
                StartupLogBuilder.Append(" [");
                StartupLogBuilder.Append(device.name);
                StartupLogBuilder.Append(", ");
                StartupLogBuilder.Append(device.characteristics);
                StartupLogBuilder.Append(", valid=");
                StartupLogBuilder.Append(device.isValid);
                StartupLogBuilder.Append(']');
            }

            Debug.Log(StartupLogBuilder.ToString(), this);
        }

        private void Update()
        {
            UpdateTrackedTransform(XRNode.CenterEye, head);
            UpdateTrackedTransform(XRNode.LeftHand, leftHandMarker);
            UpdateTrackedTransform(XRNode.RightHand, rightHandMarker);

            UpdatePointer(leftPointer);
            UpdatePointer(rightPointer);
        }

        public void ResetRigPose()
        {
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            if (head != null)
            {
                head.localPosition = new Vector3(0f, 1.65f, 0f);
                head.localRotation = Quaternion.identity;
            }

            if (leftHandMarker != null)
            {
                leftHandMarker.localPosition = new Vector3(-0.25f, 1.25f, 0.45f);
                leftHandMarker.localRotation = Quaternion.identity;
            }

            if (rightHandMarker != null)
            {
                rightHandMarker.localPosition = new Vector3(0.25f, 1.25f, 0.45f);
                rightHandMarker.localRotation = Quaternion.identity;
            }
        }

        private void UpdateTrackedTransform(XRNode node, Transform target)
        {
            if (target == null)
            {
                return;
            }

            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid)
            {
                return;
            }

            bool hasPosition = device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position);
            bool hasRotation = device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation);

            if (hasPosition)
            {
                target.localPosition = position;
            }

            if (hasRotation)
            {
                target.localRotation = rotation;
            }
        }

        private void UpdatePointer(HandPointer pointer)
        {
            if (!pointer.IsBound)
            {
                return;
            }

            Quaternion offset = Quaternion.Euler(rayLocalEulerOffset);
            Ray ray = new Ray(pointer.Marker.position, pointer.Marker.rotation * offset * Vector3.forward);
            float endDistance = maxRayDistance;

            if (usePhysicsBlockers && Physics.Raycast(ray, out RaycastHit physicsHit, maxRayDistance, physicsBlockers, QueryTriggerInteraction.Ignore))
            {
                endDistance = physicsHit.distance;
            }

            UiHit uiHit = default;
            bool hasUiHit = TryRaycastUi(pointer, ray, endDistance, out uiHit);
            if (hasUiHit)
            {
                endDistance = uiHit.Distance;
            }

            pointer.SetRay(ray.origin, ray.origin + ray.direction * endDistance);
            UpdateHover(pointer, hasUiHit ? uiHit.Target : null, hasUiHit ? uiHit.EventData : null);
            UpdateClick(pointer, IsTriggerPressed(pointer.Node), hasUiHit ? uiHit.Target : null, hasUiHit ? uiHit.EventData : null);
        }

        private bool TryRaycastUi(HandPointer pointer, Ray ray, float maxDistance, out UiHit bestHit)
        {
            bestHit = default;

            EventSystem eventSystem = EventSystem.current;
            Camera eventCamera = uiEventCamera != null ? uiEventCamera : headCamera;
            if (eventSystem == null || eventCamera == null)
            {
                return false;
            }

            Raycasters.Clear();
#if UNITY_2023_1_OR_NEWER
            Raycasters.AddRange(FindObjectsByType<GraphicRaycaster>(FindObjectsSortMode.None));
#else
            Raycasters.AddRange(FindObjectsOfType<GraphicRaycaster>());
#endif

            bool found = false;
            for (int i = 0; i < Raycasters.Count; i++)
            {
                GraphicRaycaster raycaster = Raycasters[i];
                if (raycaster == null || !raycaster.isActiveAndEnabled)
                {
                    continue;
                }

                Canvas canvas = raycaster.GetComponent<Canvas>();
                RectTransform canvasRect = raycaster.transform as RectTransform;
                if (canvas == null || canvasRect == null || canvas.renderMode != RenderMode.WorldSpace)
                {
                    continue;
                }

                Plane canvasPlane = new Plane(canvasRect.forward, canvasRect.position);
                if (!canvasPlane.Raycast(ray, out float distance) || distance < 0f || distance > maxDistance)
                {
                    continue;
                }

                Vector3 worldPoint = ray.GetPoint(distance);
                Camera canvasCamera = canvas.worldCamera != null ? canvas.worldCamera : eventCamera;
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvasCamera, worldPoint);
                if (!RectTransformUtility.RectangleContainsScreenPoint(canvasRect, screenPoint, canvasCamera))
                {
                    continue;
                }

                PointerEventData eventData = pointer.EventData;
                eventData.Reset();
                eventData.pointerId = pointer.PointerId;
                eventData.position = screenPoint;
                eventData.delta = screenPoint - pointer.LastScreenPosition;
                eventData.button = PointerEventData.InputButton.Left;
                eventData.scrollDelta = Vector2.zero;

                pointer.Results.Clear();
                raycaster.Raycast(eventData, pointer.Results);
                if (pointer.Results.Count == 0)
                {
                    continue;
                }

                RaycastResult result = pointer.Results[0];
                GameObject target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(result.gameObject);
                if (target == null)
                {
                    target = ExecuteEvents.GetEventHandler<IPointerEnterHandler>(result.gameObject);
                }

                if (target == null)
                {
                    target = result.gameObject;
                }

                if (!found || distance < bestHit.Distance)
                {
                    eventData.pointerCurrentRaycast = result;
                    bestHit = new UiHit(distance, target, eventData);
                    found = true;
                }
            }

            return found;
        }

        private void UpdateHover(HandPointer pointer, GameObject target, PointerEventData eventData)
        {
            if (pointer.Hovered == target)
            {
                pointer.LastScreenPosition = eventData != null ? eventData.position : pointer.LastScreenPosition;
                return;
            }

            if (pointer.Hovered != null)
            {
                ExecuteEvents.Execute(pointer.Hovered, pointer.EventData, ExecuteEvents.pointerExitHandler);
            }

            pointer.Hovered = target;

            if (pointer.Hovered != null && eventData != null)
            {
                eventData.pointerEnter = pointer.Hovered;
                ExecuteEvents.Execute(pointer.Hovered, eventData, ExecuteEvents.pointerEnterHandler);
                pointer.LastScreenPosition = eventData.position;
            }
        }

        private void UpdateClick(HandPointer pointer, bool triggerPressed, GameObject target, PointerEventData eventData)
        {
            if (triggerPressed && !pointer.WasTriggerPressed)
            {
                pointer.Pressed = target;
                if (pointer.Pressed != null && eventData != null)
                {
                    eventData.pointerPress = pointer.Pressed;
                    ExecuteEvents.Execute(pointer.Pressed, eventData, ExecuteEvents.pointerDownHandler);
                }
            }

            if (!triggerPressed && pointer.WasTriggerPressed)
            {
                if (pointer.Pressed != null && eventData != null)
                {
                    ExecuteEvents.Execute(pointer.Pressed, eventData, ExecuteEvents.pointerUpHandler);
                    if (pointer.Pressed == target)
                    {
                        ExecuteEvents.Execute(pointer.Pressed, eventData, ExecuteEvents.pointerClickHandler);
                    }
                }

                pointer.Pressed = null;
            }

            pointer.WasTriggerPressed = triggerPressed;
        }

        private bool IsTriggerPressed(XRNode node)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid)
            {
                return false;
            }

            if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerButton) && triggerButton)
            {
                return true;
            }

            return device.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue) && triggerValue >= triggerThreshold;
        }

        private readonly struct UiHit
        {
            public readonly float Distance;
            public readonly GameObject Target;
            public readonly PointerEventData EventData;

            public UiHit(float distance, GameObject target, PointerEventData eventData)
            {
                Distance = distance;
                Target = target;
                EventData = eventData;
            }
        }

        private sealed class HandPointer
        {
            public readonly XRNode Node;
            public readonly int PointerId;
            public readonly List<RaycastResult> Results = new List<RaycastResult>(16);

            private PointerEventData eventData;

            public Transform Marker { get; private set; }
            public GameObject Hovered { get; set; }
            public GameObject Pressed { get; set; }
            public Vector2 LastScreenPosition { get; set; }
            public bool WasTriggerPressed { get; set; }
            public PointerEventData EventData => eventData ??= new PointerEventData(EventSystem.current);

            private LineRenderer line;

            public bool IsBound => Marker != null && line != null;

            public HandPointer(XRNode node, int pointerId)
            {
                Node = node;
                PointerId = pointerId;
            }

            public void Bind(Transform marker, LineRenderer rayLine)
            {
                Marker = marker;
                line = rayLine;
            }

            public void SetRay(Vector3 start, Vector3 end)
            {
                line.positionCount = 2;
                line.SetPosition(0, start);
                line.SetPosition(1, end);
            }
        }
    }
}
