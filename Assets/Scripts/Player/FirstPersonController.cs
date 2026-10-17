using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieSurvival.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0f)] private float walkSpeed = 4f;
        [SerializeField, Min(0f)] private float runSpeed = 7f;
        [SerializeField, Min(0.1f)] private float gravity = 20f;

        [Header("Look")]
        [SerializeField] private Transform playerCamera;
        [Tooltip("Degrees per pixel of mouse movement.")]
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.1f;
        [SerializeField, Range(1f, 89f)] private float verticalLookLimit = 85f;

        private CharacterController controller;
        private float verticalVelocity;
        private float pitch;
        private bool skipLook;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (playerCamera == null)
            {
                Camera childCamera = GetComponentInChildren<Camera>();
                if (childCamera != null) playerCamera = childCamera.transform;
            }
            if (playerCamera == null)
            {
                Debug.LogError("FirstPersonController requires a child camera.", this);
                enabled = false;
                return;
            }
            pitch = Mathf.DeltaAngle(0f, playerCamera.localEulerAngles.x);
        }

        private void OnEnable() => SetCursorCaptured(true);
        private void OnDisable() => SetCursorCaptured(false);
        private void OnApplicationFocus(bool focused)
        {
            // Regaining focus requires an explicit click so returning to the Editor is safe.
            if (!focused) SetCursorCaptured(false);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                SetCursorCaptured(false);
            else if (Application.isFocused && mouse != null && mouse.leftButton.wasPressedThisFrame)
                SetCursorCaptured(true);

            bool acceptingInput = Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
            Vector2 movement = Vector2.zero;
            bool running = false;
            if (acceptingInput && keyboard != null)
            {
                movement = new Vector2((keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                    (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
                movement = Vector2.ClampMagnitude(movement, 1f);
                running = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            }

            if (acceptingInput && mouse != null && !skipLook)
            {
                // Mouse delta already represents movement this frame; do not multiply by deltaTime.
                Vector2 look = mouse.delta.ReadValue() * mouseSensitivity;
                transform.Rotate(0f, look.x, 0f);
                pitch = Mathf.Clamp(pitch - look.y, -verticalLookLimit, verticalLookLimit);
                playerCamera.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
            skipLook = false;

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity = Mathf.Max(verticalVelocity - gravity * Time.deltaTime, -50f);
            Vector3 velocity = (transform.right * movement.x + transform.forward * movement.y)
                * (running ? runSpeed : walkSpeed);
            velocity.y = verticalVelocity;
            CollisionFlags collisions = controller.Move(velocity * Time.deltaTime);
            if ((collisions & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
        }

        private void SetCursorCaptured(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
            skipLook = true;
        }
    }
}
