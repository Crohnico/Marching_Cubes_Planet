using UnityEngine;
using UnityEngine.XR;

namespace MarchingCubesPlanet.Lab
{
    public sealed class DebugMinimalLocomotion : MonoBehaviour
    {
        private enum SpeedMode
        {
            Approach = 0,
            Interior = 1
        }

        [Header("Speed")]
        [SerializeField] private float approachSpeed = 900f;
        [SerializeField] private float interiorSpeed = 30f;
        [SerializeField] private SpeedMode speedMode = SpeedMode.Approach;

        [Header("Look")]
        [SerializeField] private float pitchDegreesPerSecond = 90f;
        [SerializeField] private float yawDegreesPerSecond = 90f;
        [SerializeField] private float inputDeadzone = 0.15f;

        private bool wasTogglePressed;

        private void Update()
        {
            InputDevice rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            InputDevice leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

            UpdateSpeedToggle(rightController);
            UpdateRotation(leftController);
            UpdateMovement(rightController);
        }

        private void UpdateSpeedToggle(InputDevice rightController)
        {
            bool togglePressed = rightController.isValid &&
                                 rightController.TryGetFeatureValue(CommonUsages.primaryButton, out bool primaryButton) &&
                                 primaryButton;

            if (togglePressed && !wasTogglePressed)
            {
                speedMode = speedMode == SpeedMode.Approach ? SpeedMode.Interior : SpeedMode.Approach;
            }

            wasTogglePressed = togglePressed;
        }

        private void UpdateRotation(InputDevice leftController)
        {
            if (!TryReadThumbstick(leftController, out Vector2 lookInput))
            {
                return;
            }

            float yawInput = ApplyDeadzone(lookInput.x);
            float pitchInput = ApplyDeadzone(lookInput.y);
            if (Mathf.Approximately(yawInput, 0f) && Mathf.Approximately(pitchInput, 0f))
            {
                return;
            }

            Quaternion rotation = transform.rotation;
            if (!Mathf.Approximately(yawInput, 0f))
            {
                rotation = Quaternion.AngleAxis(
                    yawInput * yawDegreesPerSecond * Time.deltaTime,
                    rotation * Vector3.up) * rotation;
            }

            if (!Mathf.Approximately(pitchInput, 0f))
            {
                rotation = Quaternion.AngleAxis(
                    -pitchInput * pitchDegreesPerSecond * Time.deltaTime,
                    rotation * Vector3.right) * rotation;
            }

            transform.rotation = rotation;
        }

        private void UpdateMovement(InputDevice rightController)
        {
            if (!TryReadThumbstick(rightController, out Vector2 moveInput))
            {
                return;
            }

            Vector2 filteredInput = new Vector2(
                ApplyDeadzone(moveInput.x),
                ApplyDeadzone(moveInput.y));
            if (filteredInput.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector3 moveDirection = transform.forward * filteredInput.y + transform.right * filteredInput.x;
            if (moveDirection.sqrMagnitude > 1f)
            {
                moveDirection.Normalize();
            }

            transform.position += moveDirection * CurrentSpeed * Time.deltaTime;
        }

        private float CurrentSpeed => speedMode == SpeedMode.Approach ? approachSpeed : interiorSpeed;

        private static bool TryReadThumbstick(InputDevice device, out Vector2 value)
        {
            value = Vector2.zero;
            return device.isValid && device.TryGetFeatureValue(CommonUsages.primary2DAxis, out value);
        }

        private float ApplyDeadzone(float value)
        {
            return Mathf.Abs(value) >= inputDeadzone ? value : 0f;
        }
    }
}
