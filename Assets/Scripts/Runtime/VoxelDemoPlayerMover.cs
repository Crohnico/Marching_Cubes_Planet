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
        [SerializeField] private bool useUnscaledTime;

        private void Update()
        {
            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
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

            Vector3 movement = horizontal * moveSpeed + Vector3.up * (input.y * verticalSpeed);
            transform.position += movement * deltaTime;
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
