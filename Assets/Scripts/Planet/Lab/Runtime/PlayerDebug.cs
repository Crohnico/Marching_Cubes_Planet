using System;
using System.Reflection;
using UnityEngine;

namespace MarchingCubesPlanet.Lab
{
    public sealed class PlayerDebug : MonoBehaviour
    {
        [SerializeField] private float mouseSensitivity = 0.12f;
        [SerializeField] private bool requireRightMouseButton = true;
        [SerializeField] private bool lockCursorWhileRotating = true;
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;

        private float yaw;
        private float pitch;
        private bool capturedCursor;
        private CursorLockMode previousLockMode;
        private bool previousCursorVisible;

        private void OnEnable()
        {
            Vector3 euler = transform.rotation.eulerAngles;
            yaw = euler.y;
            pitch = NormalizePitch(euler.x);
        }

        private void OnDisable()
        {
            RestoreCursor();
        }

        private void Update()
        {
            if (!ShouldRotate())
            {
                RestoreCursor();
                return;
            }

            CaptureCursor();

            Vector2 mouseDelta = ReadMouseDelta();
            yaw += mouseDelta.x * mouseSensitivity;
            pitch = Mathf.Clamp(pitch - mouseDelta.y * mouseSensitivity, minPitch, maxPitch);

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private bool ShouldRotate()
        {
            if (!requireRightMouseButton)
            {
                return true;
            }

            if (InputSystemMouse.TryReadRightButton(out bool pressed))
            {
                return pressed;
            }

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(1);
#else
            return false;
#endif
        }

        private static Vector2 ReadMouseDelta()
        {
            if (InputSystemMouse.TryReadDelta(out Vector2 delta))
            {
                return delta;
            }

#if ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
#else
            return Vector2.zero;
#endif
        }

        private void CaptureCursor()
        {
            if (!lockCursorWhileRotating || capturedCursor)
            {
                return;
            }

            previousLockMode = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            capturedCursor = true;
        }

        private void RestoreCursor()
        {
            if (!capturedCursor)
            {
                return;
            }

            Cursor.lockState = previousLockMode;
            Cursor.visible = previousCursorVisible;
            capturedCursor = false;
        }

        private static float NormalizePitch(float pitchDegrees)
        {
            return pitchDegrees > 180f ? pitchDegrees - 360f : pitchDegrees;
        }

        private static class InputSystemMouse
        {
            private static bool initialized;
            private static PropertyInfo currentProperty;
            private static PropertyInfo rightButtonProperty;
            private static PropertyInfo isPressedProperty;
            private static PropertyInfo deltaProperty;
            private static MethodInfo readValueMethod;

            public static bool TryReadRightButton(out bool pressed)
            {
                pressed = false;
                object mouse = GetCurrentMouse();
                if (mouse == null || rightButtonProperty == null || isPressedProperty == null)
                {
                    return false;
                }

                object rightButton = rightButtonProperty.GetValue(mouse);
                if (rightButton == null)
                {
                    return false;
                }

                object value = isPressedProperty.GetValue(rightButton);
                if (value is bool boolValue)
                {
                    pressed = boolValue;
                    return true;
                }

                return false;
            }

            public static bool TryReadDelta(out Vector2 delta)
            {
                delta = Vector2.zero;
                object mouse = GetCurrentMouse();
                if (mouse == null || deltaProperty == null || readValueMethod == null)
                {
                    return false;
                }

                object deltaControl = deltaProperty.GetValue(mouse);
                if (deltaControl == null)
                {
                    return false;
                }

                object value = readValueMethod.Invoke(deltaControl, null);
                if (value is Vector2 vectorValue)
                {
                    delta = vectorValue;
                    return true;
                }

                return false;
            }

            private static object GetCurrentMouse()
            {
                EnsureInitialized();
                return currentProperty?.GetValue(null);
            }

            private static void EnsureInitialized()
            {
                if (initialized)
                {
                    return;
                }

                initialized = true;

                Type mouseType = Type.GetType("UnityEngine.InputSystem.Mouse, Unity.InputSystem");
                if (mouseType == null)
                {
                    return;
                }

                currentProperty = mouseType.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
                rightButtonProperty = mouseType.GetProperty("rightButton", BindingFlags.Public | BindingFlags.Instance);
                deltaProperty = mouseType.GetProperty("delta", BindingFlags.Public | BindingFlags.Instance);

                Type buttonType = rightButtonProperty?.PropertyType;
                isPressedProperty = buttonType?.GetProperty("isPressed", BindingFlags.Public | BindingFlags.Instance);

                Type deltaType = deltaProperty?.PropertyType;
                readValueMethod = deltaType?.GetMethod("ReadValue", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            }
        }
    }
}
