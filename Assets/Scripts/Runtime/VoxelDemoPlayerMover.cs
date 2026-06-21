using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    [DisallowMultipleComponent]
    public sealed class VoxelDemoPlayerMover : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float planetMoveSpeed = 20f;
        [SerializeField, Min(0f)] private float spaceMoveSpeed = 100f;
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.12f;
        [SerializeField] private bool lockCursorOnEnable = true;
        [SerializeField] private bool useUnscaledTime;
        [Header("Planet Reference")]
        [SerializeField] private bool usePlanetReferenceFrame = true;
        [SerializeField] private VoxelSphereGenerator referencePlanet;
        [SerializeField, Min(0f)] private float planetReferencePadding = 1000f;
        [SerializeField, Min(0f)] private float planetUpAlignSpeed = 8f;

        private Vector3 currentReferenceUp = Vector3.up;
        private bool usingPlanetReference;
        private bool usePlanetSpeedMode;

        private void OnValidate()
        {
            planetMoveSpeed = Mathf.Max(0f, planetMoveSpeed);
            spaceMoveSpeed = Mathf.Max(0f, spaceMoveSpeed);
            mouseSensitivity = Mathf.Max(0f, mouseSensitivity);
            planetReferencePadding = Mathf.Max(0f, planetReferencePadding);
            planetUpAlignSpeed = Mathf.Max(0f, planetUpAlignSpeed);
        }

        private void OnEnable()
        {
            currentReferenceUp = transform.up.sqrMagnitude > 0.0001f
                ? transform.up.normalized
                : Vector3.up;

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
            ToggleSpeedModeIfRequested();
            Vector3 referenceUp = ResolveReferenceUp(deltaTime);
            UpdateRotation(referenceUp);

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

            Vector3 directionalMovement = transform.right * horizontal.x + transform.forward * horizontal.z;
            if (directionalMovement.sqrMagnitude > 1f)
            {
                directionalMovement.Normalize();
            }

            float moveSpeed = usePlanetSpeedMode ? planetMoveSpeed : spaceMoveSpeed;
            Vector3 movement = directionalMovement * moveSpeed
                + referenceUp * (input.y * moveSpeed);
            transform.position += movement * deltaTime;
        }

        private void UpdateRotation(Vector3 referenceUp)
        {
            Vector2 mouseDelta = GetMouseDelta();
            AlignToReferenceUp(referenceUp);
            if (mouseDelta.sqrMagnitude <= 0f)
            {
                return;
            }

            float yawDelta = mouseDelta.x * mouseSensitivity;
            float pitchDelta = -mouseDelta.y * mouseSensitivity;
            transform.rotation = Quaternion.AngleAxis(yawDelta, referenceUp) * transform.rotation;
            transform.rotation = Quaternion.AngleAxis(pitchDelta, transform.right) * transform.rotation;
            AlignToReferenceUp(referenceUp);
        }

        private Vector3 ResolveReferenceUp(float deltaTime)
        {
            Vector3 targetUp = Vector3.up;
            Vector3 planetUp = Vector3.up;
            VoxelSphereGenerator planet = ResolveReferencePlanet();
            usingPlanetReference = usePlanetReferenceFrame && TryGetPlanetReferenceUp(planet, out planetUp);
            if (usingPlanetReference)
            {
                targetUp = planetUp;
            }

            if (planetUpAlignSpeed <= 0f)
            {
                currentReferenceUp = targetUp;
                return currentReferenceUp;
            }

            float t = 1f - Mathf.Exp(-planetUpAlignSpeed * Mathf.Max(0f, deltaTime));
            currentReferenceUp = Vector3.Slerp(currentReferenceUp, targetUp, t).normalized;
            return currentReferenceUp;
        }

        private VoxelSphereGenerator ResolveReferencePlanet()
        {
            if (referencePlanet == null)
            {
                referencePlanet = FindFirstObjectByType<VoxelSphereGenerator>();
            }

            return referencePlanet;
        }

        private bool TryGetPlanetReferenceUp(VoxelSphereGenerator planet, out Vector3 up)
        {
            if (planet == null)
            {
                up = Vector3.up;
                return false;
            }

            Vector3 fromCenter = transform.position - planet.Center;
            float maxDistance = planet.MaximumTerrainRadius + planetReferencePadding;
            if (fromCenter.sqrMagnitude > maxDistance * maxDistance)
            {
                up = Vector3.up;
                return false;
            }

            up = fromCenter.sqrMagnitude > 0.0001f ? fromCenter.normalized : Vector3.up;
            return true;
        }

        private void ToggleSpeedModeIfRequested()
        {
            if (WasSpeedTogglePressed())
            {
                usePlanetSpeedMode = !usePlanetSpeedMode;
            }
        }

        private void AlignToReferenceUp(Vector3 referenceUp)
        {
            Vector3 forward = transform.forward;
            if (Mathf.Abs(Vector3.Dot(forward.normalized, referenceUp)) > 0.999f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(forward, referenceUp);
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

        private static bool WasSpeedTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.qKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Q);
#else
            return false;
#endif
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
                    return keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
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
                    return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
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
