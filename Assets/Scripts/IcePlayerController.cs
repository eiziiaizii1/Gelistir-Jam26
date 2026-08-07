using UnityEngine;
using UnityEngine.InputSystem;

namespace IceEscape
{
    public enum ControlMode
    {
        MouseFlickSlap, // "Şaplak Hissi" - Quick mouse flicks apply sharp side sliding impulses
        ScreenDrag,     // Click & Drag anywhere on screen
        CursorPosition, // Follows Mouse Cursor X position
        ButtonHold      // Left/Right Click hold
    }

    [RequireComponent(typeof(Rigidbody))]
    public class IcePlayerController : MonoBehaviour
    {
        [Header("Control Settings")]
        [SerializeField] private ControlMode controlMode = ControlMode.MouseFlickSlap;
        [SerializeField] private float moveForce = 40f;
        [SerializeField] private float maxSpeed = 20f;
        [SerializeField] private float torqueForce = 6f;
        [SerializeField] private float jumpForce = 8.5f;

        [Header("Şaplak / Flick Impulse Settings")]
        [SerializeField] private bool enableMouseFlick = true;
        [SerializeField] private float flickThreshold = 8f;        // Minimum mouse delta speed to trigger slap
        [SerializeField] private float flickImpulseForce = 14f;    // Sharp side force on slap
        [SerializeField] private float maxVisualTiltAngle = 30f;   // Visual roll tilt angle when slapped
        [SerializeField] private float tiltDamping = 8f;

        [Header("Auto Slide Settings")]
        [SerializeField] private bool autoForward = true;
        [SerializeField] private float autoForwardForce = 1.0f;

        [Header("Ground Check & Alignment")]
        [SerializeField] private float sphereCastRadius = 0.4f;
        [SerializeField] private float groundCheckDistance = 0.5f;
        [SerializeField] private LayerMask groundLayer = ~0;
        [SerializeField] private Transform visualTransform;
        [SerializeField] private float alignSpeed = 14f;
        [SerializeField] private float normalSmoothSpeed = 14f;
        [SerializeField] private bool unparentVisualOnStart = true;

        [Header("Camera Reference")]
        [SerializeField] private Transform cameraTransform;

        private Rigidbody rb;
        private bool isGrounded;
        private Vector3 targetGroundNormal = Vector3.up;
        private Vector3 smoothedNormal = Vector3.up;
        private Vector3 lastMoveDir = Vector3.forward;

        private float currentTiltRoll = 0f;
        private float targetTiltRoll = 0f;
        private Vector2 dragStartPos;
        private bool isDragging;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();

            if (visualTransform == null)
            {
                Transform child = transform.Find("IceVisual");
                if (child != null) visualTransform = child;
            }
        }

        private void Start()
        {
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            if (visualTransform != null && unparentVisualOnStart)
            {
                visualTransform.SetParent(null);
            }
        }

        private void Update()
        {
            HandleDragInputState();
            DetectMouseFlickSlap();

            if (GetJumpInput() && isGrounded)
            {
                Jump();
            }
        }

        private void FixedUpdate()
        {
            HandleMovement();
        }

        private void LateUpdate()
        {
            CheckGround();
            UpdateVisualTransform();
        }

        private void OnDestroy()
        {
            if (visualTransform != null && unparentVisualOnStart && visualTransform.gameObject != null)
            {
                Destroy(visualTransform.gameObject);
            }
        }

        private void HandleDragInputState()
        {
            if (Mouse.current != null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    isDragging = true;
                    dragStartPos = Mouse.current.position.ReadValue();
                }
                else if (Mouse.current.leftButton.wasReleasedThisFrame)
                {
                    isDragging = false;
                }
            }
        }

        private void DetectMouseFlickSlap()
        {
            if (!enableMouseFlick || Mouse.current == null) return;

            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            float deltaX = mouseDelta.x;

            // Detect quick mouse swipe / flick (Şaplak hissi!)
            if (Mathf.Abs(deltaX) >= flickThreshold)
            {
                float flickDir = Mathf.Sign(deltaX);

                Vector3 camRight = Vector3.right;
                if (cameraTransform != null)
                {
                    camRight = cameraTransform.right;
                    camRight.y = 0f;
                    camRight.Normalize();
                }

                // Apply sharp instantaneous impulse force sideways
                rb.AddForce(camRight * flickDir * flickImpulseForce, ForceMode.Impulse);

                // Set visual tilt roll angle
                targetTiltRoll = -flickDir * maxVisualTiltAngle;
            }

            // Smoothly damp tilt roll angle back to zero
            targetTiltRoll = Mathf.MoveTowards(targetTiltRoll, 0f, Time.deltaTime * maxVisualTiltAngle * 2.5f);
            currentTiltRoll = Mathf.Lerp(currentTiltRoll, targetTiltRoll, Time.deltaTime * tiltDamping);
        }

        private bool GetJumpInput()
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                return true;

            if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
                return true;

            return false;
        }

        private Vector2 GetMoveInput()
        {
            Vector2 input = Vector2.zero;

            // 1. Keyboard Input (WASD / Arrow Keys)
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) input.y += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) input.y -= 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) input.x -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input.x += 1f;
            }

            // 2. Gamepad Input
            if (Gamepad.current != null)
            {
                Vector2 stick = Gamepad.current.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.05f) input += stick;
            }

            // 3. Mouse Steering & Flick Input
            if (Mouse.current != null)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();

                switch (controlMode)
                {
                    case ControlMode.MouseFlickSlap:
                        // Continuous smooth mouse delta + flick impulses
                        if (Mathf.Abs(delta.x) > 0.05f)
                        {
                            input.x += delta.x * 0.15f;
                        }
                        break;

                    case ControlMode.ScreenDrag:
                        if (isDragging)
                        {
                            Vector2 currentMousePos = Mouse.current.position.ReadValue();
                            float screenDeltaX = (currentMousePos.x - dragStartPos.x) / Screen.width;
                            input.x += screenDeltaX * 3.5f;
                        }
                        else if (Mathf.Abs(delta.x) > 0.05f)
                        {
                            input.x += delta.x * 0.1f;
                        }
                        break;

                    case ControlMode.CursorPosition:
                        Vector2 mousePos = Mouse.current.position.ReadValue();
                        float normalizedX = (mousePos.x / Screen.width) * 2f - 1f;
                        input.x += normalizedX;
                        break;

                    case ControlMode.ButtonHold:
                        if (Mouse.current.leftButton.isPressed) input.x -= 1f;
                        if (Mouse.current.rightButton.isPressed) input.x += 1f;
                        break;
                }
            }

            // Auto-forward slide down the escape track
            if (autoForward && input.y >= 0f)
            {
                input.y = Mathf.Max(input.y, autoForwardForce);
            }

            return Vector2.ClampMagnitude(input, 1f);
        }

        private void CheckGround()
        {
            RaycastHit hit;
            if (Physics.SphereCast(transform.position, sphereCastRadius, Vector3.down, out hit, groundCheckDistance, groundLayer))
            {
                isGrounded = true;
                targetGroundNormal = hit.normal;
            }
            else if (Physics.Raycast(transform.position, Vector3.down, out hit, 1.2f, groundLayer))
            {
                isGrounded = true;
                targetGroundNormal = hit.normal;
            }
            else
            {
                isGrounded = false;
                targetGroundNormal = Vector3.up;
            }
        }

        private void UpdateVisualTransform()
        {
            if (visualTransform == null) return;

            visualTransform.position = transform.position;
            smoothedNormal = Vector3.Slerp(smoothedNormal, targetGroundNormal, Time.deltaTime * normalSmoothSpeed);

            Vector3 referenceForward = lastMoveDir;
            if (cameraTransform != null)
            {
                Vector3 camFwd = cameraTransform.forward;
                camFwd.y = 0f;
                if (camFwd.sqrMagnitude > 0.01f)
                {
                    referenceForward = camFwd.normalized;
                }
            }

            Vector3 forwardOnPlane = Vector3.ProjectOnPlane(referenceForward, smoothedNormal).normalized;
            if (forwardOnPlane.sqrMagnitude < 0.001f)
            {
                forwardOnPlane = Vector3.ProjectOnPlane(Vector3.forward, smoothedNormal).normalized;
            }

            // Base ground slope rotation
            Quaternion slopeRotation = Quaternion.LookRotation(forwardOnPlane, smoothedNormal);

            // Add dynamic "Şaplak" tilt roll angle into the turn
            Quaternion tiltRotation = Quaternion.AngleAxis(currentTiltRoll, forwardOnPlane);

            visualTransform.rotation = Quaternion.Slerp(visualTransform.rotation, tiltRotation * slopeRotation, Time.deltaTime * alignSpeed);
        }

        private void HandleMovement()
        {
            Vector2 input = GetMoveInput();

            if (input.sqrMagnitude < 0.01f)
                return;

            Vector3 camForward = Vector3.forward;
            Vector3 camRight = Vector3.right;

            if (cameraTransform != null)
            {
                camForward = cameraTransform.forward;
                camForward.y = 0f;
                camForward.Normalize();

                camRight = cameraTransform.right;
                camRight.y = 0f;
                camRight.Normalize();
            }

            Vector3 moveDirection = (camForward * input.y + camRight * input.x).normalized;
            lastMoveDir = moveDirection;

            Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            if (horizontalVelocity.magnitude < maxSpeed)
            {
                rb.AddForce(moveDirection * moveForce, ForceMode.Force);
            }

            Vector3 torqueAxis = Vector3.Cross(Vector3.up, moveDirection);
            rb.AddTorque(torqueAxis * torqueForce, ForceMode.Force);
        }

        private void Jump()
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position + Vector3.down * groundCheckDistance, sphereCastRadius);
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, smoothedNormal * 2f);
        }
    }
}
