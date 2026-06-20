using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    [DisallowMultipleComponent]
    public sealed class VoxelDemoPlayerMover : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float moveSpeed = 12f;
        [SerializeField, Min(0f)] private float verticalSpeed = 8f;
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.12f;
        [SerializeField] private bool lockCursorOnEnable = true;
        [SerializeField] private bool useUnscaledTime;

        private float yaw;
        private float pitch;

        private void OnEnable()
        {
            Vector3 eulerAngles = transform.rotation.eulerAngles;
            yaw = eulerAngles.y;
            pitch = NormalizeAngle(eulerAngles.x);

            if (lockCursorOnEnable)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void OnDisable()
        {
            if (lockCursorOnEnable && Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void Update()
        {
            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            UpdateRotation();

            Vector3 input = new Vector3(
                GetAxis(KeyBinding.Right, KeyBinding.Left),
                GetAxis(KeyBinding.Up, KeyBinding.Down),
                GetAxis(KeyBinding.Forward, KeyBinding.Back));

            if (input.sqrMagnitude <= 0f)
            {
                return;
            }

            Vector3 horizontal = new Vector3(input.x, 0f, input.z);
            if (horizontal.sqrMagnitude > 1f)
            {
                horizontal.Normalize();
            }

            Vector3 movement = (transform.right * horizontal.x + transform.forward * horizontal.z) * moveSpeed
                + Vector3.up * (input.y * verticalSpeed);
            transform.position += movement * deltaTime;
        }

        private void UpdateRotation()
        {
            Vector2 mouseDelta = GetMouseDelta();
            if (mouseDelta.sqrMagnitude <= 0f)
            {
                return;
            }

            yaw += mouseDelta.x * mouseSensitivity;
            pitch = Mathf.Clamp(pitch - mouseDelta.y * mouseSensitivity, -89f, 89f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private static Vector2 GetMouseDelta()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            return mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
#else
            return Vector2.zero;
#endif
        }

        private static float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }

        private static float GetAxis(KeyBinding positive, KeyBinding negative)
        {
            float value = 0f;
            if (IsPressed(positive))
            {
                value += 1f;
            }

            if (IsPressed(negative))
            {
                value -= 1f;
            }

            return value;
        }

        private static bool IsPressed(KeyBinding binding)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            switch (binding)
            {
                case KeyBinding.Forward:
                    return keyboard.wKey.isPressed;
                case KeyBinding.Back:
                    return keyboard.sKey.isPressed;
                case KeyBinding.Left:
                    return keyboard.aKey.isPressed;
                case KeyBinding.Right:
                    return keyboard.dKey.isPressed;
                case KeyBinding.Up:
                    return keyboard.eKey.isPressed;
                case KeyBinding.Down:
                    return keyboard.qKey.isPressed;
                default:
                    return false;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            switch (binding)
            {
                case KeyBinding.Forward:
                    return Input.GetKey(KeyCode.W);
                case KeyBinding.Back:
                    return Input.GetKey(KeyCode.S);
                case KeyBinding.Left:
                    return Input.GetKey(KeyCode.A);
                case KeyBinding.Right:
                    return Input.GetKey(KeyCode.D);
                case KeyBinding.Up:
                    return Input.GetKey(KeyCode.E);
                case KeyBinding.Down:
                    return Input.GetKey(KeyCode.Q);
                default:
                    return false;
            }
#else
            return false;
#endif
        }

        private enum KeyBinding
        {
            Forward,
            Back,
            Left,
            Right,
            Up,
            Down
        }
    }
}
