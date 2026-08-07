using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Physics-driven downhill slide. Gravity supplies the forward motion; the mouse only
/// slaps the block sideways (X) and dashes it forward (Y), both as instantaneous
/// impulses so momentum carries on the ice.
///
/// The two mouse axes are strictly isolated: a single frame of mouse movement resolves
/// to a slap OR a dash, never both, so side-to-side correction can never fire the dash.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class IceSlideController : MonoBehaviour
{
    [Header("Lateral slap (mouse X)")]
    [Tooltip("Impulse from one slap. The block follows the drag: dragging RIGHT pushes " +
             "it RIGHT, dragging LEFT pushes it LEFT.")]
    [SerializeField] private float slapImpulse = 8f;
    [Tooltip("Mouse speed in pixels/second before a slap registers. Slower than this " +
             "is treated as aiming, not swinging.")]
    [SerializeField] private float slapSpeedThreshold = 250f;
    [Tooltip("Mouse speed in pixels/second that produces a full-strength (1.0) slap. " +
             "Lower makes the block more sensitive to gentle swipes.")]
    [SerializeField] private float slapSpeedReference = 900f;
    [Tooltip("Drop below this mouse speed (pixels/second) to end the swing and arm the " +
             "next slap. Keep it well under slapSpeedThreshold so a steady drag can't " +
             "flicker in and out and machine-gun impulses.")]
    [SerializeField] private float slapReleaseSpeed = 90f;
    [Tooltip("Hardest slap allowed, as a multiple of slapImpulse.")]
    [SerializeField] private float maxSlapScale = 2.5f;

    [Header("Forward dash (mouse Y)")]
    [SerializeField] private float dashImpulse = 10f;
    [Tooltip("Forward mouse speed in pixels/second before a dash fires.")]
    [SerializeField] private float dashSpeedThreshold = 700f;
    [SerializeField] private float dashCooldown = 0.6f;

    [Header("Jump (right mouse button)")]
    [SerializeField] private float jumpImpulse = 10f;
    [Tooltip("Gravity multiplier while rising. Above 1 shortens the float at the top of " +
             "the arc, which is what stops a jump feeling floaty.")]
    [SerializeField] private float riseGravityMultiplier = 2.2f;
    [Tooltip("Gravity multiplier while falling. Higher than the rise value gives the " +
             "snappy 'up slow, down fast' arc games use; real gravity is symmetric and " +
             "reads as sluggish.")]
    [SerializeField] private float fallGravityMultiplier = 3.4f;
    [Tooltip("Releasing the right button while still rising cuts upward speed to this " +
             "fraction, so a quick click is a short hop and a held click is a full jump.")]
    [SerializeField] private float jumpCutMultiplier = 0.5f;
    [Tooltip("Grace period after leaving the ground where a jump still counts. Stops " +
             "jumps being eaten when the block skips over a bump.")]
    [SerializeField] private float coyoteTime = 0.12f;
    [Tooltip("How early a click can land before touchdown and still fire on landing.")]
    [SerializeField] private float jumpBuffer = 0.12f;

    [Header("Input gate")]
    [Tooltip("Mouse steering only responds while the left mouse button is held down. " +
             "Uncheck to have the mouse always live.")]
    [SerializeField] private bool requireMouseHold = true;

    [Header("Input isolation")]
    [Tooltip("One axis must beat the other by this factor to claim the frame. Higher " +
             "means diagonal mouse movement is ignored instead of picking a winner.")]
    [SerializeField] private float axisDominance = 1.5f;

    [Header("Slide")]
    [SerializeField] private float maxSpeed = 40f;
    [Tooltip("How fast sideways drift bleeds off, per second. Downhill speed is never " +
             "damped. 0 = pure ice, drift persists until something stops it.")]
    [SerializeField] private float lateralDamping = 1.2f;
    [Tooltip("Impulse scale while airborne. Near 1 so a slap mid-jump lands with real " +
             "weight: air slaps are the point of jumping, and nothing damps drift while " +
             "off the ground, so a mid-air hit carries the whole arc.")]
    [SerializeField] private float airControlMultiplier = 0.85f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Respawn")]
    [Tooltip("Drop below this world Y and the block returns to its start point. " +
             "Must sit clear below the bottom of the slope (~-30 for the default hill).")]
    [SerializeField] private float killHeight = -60f;

    [Header("Visual")]
    [SerializeField] private Transform visual;
    [Tooltip("How fast the cube yaws to face downhill. It never pitches or rolls.")]
    [SerializeField] private float visualTurnSpeed = 6f;

    private Rigidbody body;
    private SphereCollider sphere;

    private bool wasHeld;
    private float pendingSlap;
    private bool swingActive;
    private float swingSign;
    private float dashTimer;
    private bool dashQueued;

    private float coyoteTimer;
    private float jumpBufferTimer;
    private bool jumpCutQueued;

    private bool grounded;
    private Vector3 groundNormal = Vector3.up;

    private Vector3 startPosition;

    /// <summary>Downhill heading projected onto the current surface. The camera follows this.</summary>
    public Vector3 SlideDirection { get; private set; } = Vector3.forward;

    public float Speed => body != null ? body.linearVelocity.magnitude : 0f;
    public bool IsGrounded => grounded;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        sphere = GetComponent<SphereCollider>();

        // The sphere must never spin: rolling would fight the impulses and make the
        // cube snap around. All orientation is driven by the visual child instead.
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (visual == null && transform.childCount > 0)
            visual = transform.GetChild(0);

        startPosition = transform.position;

        if (visual != null)
            visual.rotation = UprightRotation();
    }

    /// <summary>Return to the start of the slope with all momentum cleared.</summary>
    public void Respawn()
    {
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        transform.position = startPosition;
        transform.rotation = Quaternion.identity;

        pendingSlap = 0f;
        swingActive = false;
        swingSign = 0f;
        dashQueued = false;
        dashTimer = 0f;
        wasHeld = false;
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        jumpCutQueued = false;

        if (visual != null)
            visual.rotation = UprightRotation();
    }

    private void Update()
    {
        if (dashTimer > 0f)
            dashTimer -= Time.deltaTime;

        if (jumpBufferTimer > 0f)
            jumpBufferTimer -= Time.deltaTime;

        // Read before ReadMouse, and separately from it: ReadMouse bails out when the
        // left button is up, so jumping must not live behind that gate.
        ReadJump();
        ReadMouse();
        UpdateVisual();
    }

    private void ReadJump()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        if (mouse.rightButton.wasPressedThisFrame)
            jumpBufferTimer = jumpBuffer;

        if (mouse.rightButton.wasReleasedThisFrame)
            jumpCutQueued = true;
    }

    private void ReadMouse()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        bool held = !requireMouseHold || mouse.leftButton.isPressed;

        if (!held)
        {
            // Drop anything queued but not yet applied, so releasing the button can't
            // leave one last impulse to fire on the next physics step.
            pendingSlap = 0f;
            swingActive = false;
            swingSign = 0f;
            dashQueued = false;
            wasHeld = false;
            return;
        }

        Vector2 delta = mouse.delta.ReadValue();

        // Movement made before the click shouldn't count as a swing. Skip the press
        // frame so grabbing the mouse and then clicking doesn't fire a stray slap.
        if (!wasHeld)
        {
            wasHeld = true;
            return;
        }

        // Work in pixels/second, not pixels/frame. Raw delta scales with frame time, so
        // the same hand movement would hit softer at high framerates.
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float speedX = delta.x / dt;
        float speedY = delta.y / dt;

        float horizontal = Mathf.Abs(speedX);
        float forward = speedY;

        // Exactly one branch can run per frame. Horizontal movement resolves to a slap
        // and nothing else; forward movement resolves to a dash and nothing else.
        bool horizontalWins = horizontal >= slapSpeedThreshold && horizontal > Mathf.Abs(forward) * axisDominance;
        bool forwardWins = forward >= dashSpeedThreshold && forward > horizontal * axisDominance;

        if (horizontalWins)
        {
            // ONE impulse per swing. Holding the button and dragging does not push the
            // block continuously: the gate below stays shut for the rest of the gesture,
            // so a long drag is a single slap, not a stream of them. Firing every frame
            // (or on a short cooldown) integrates into smooth velocity and reads as the
            // block following the cursor, which is exactly what we don't want.
            bool reversed = swingSign != 0f && Mathf.Sign(speedX) != swingSign;

            if (!swingActive || reversed)
            {
                // Strength comes from how fast the hand was moving when the swing began.
                // The block follows the drag: a right swipe knocks it right.
                float strength = Mathf.Min(horizontal / slapSpeedReference, maxSlapScale);
                pendingSlap = Mathf.Sign(speedX) * strength;

                swingActive = true;
                swingSign = Mathf.Sign(speedX);
            }
        }
        else if (forwardWins)
        {
            if (dashTimer <= 0f)
            {
                dashQueued = true;
                dashTimer = dashCooldown;
            }
        }

        // Deliberately outside the branch chain above: folding this in as another
        // "else if" would swallow the dash branch on any frame with little sideways
        // movement, which is exactly when a forward push happens.
        if (horizontal < slapReleaseSpeed)
        {
            // Hand has slowed to a stop. The gesture is over, so the next one can land.
            swingActive = false;
            swingSign = 0f;
        }
    }

    private void FixedUpdate()
    {
        if (transform.position.y < killHeight)
        {
            Respawn();
            return;
        }

        ProbeGround();

        if (grounded)
            coyoteTimer = coyoteTime;
        else if (coyoteTimer > 0f)
            coyoteTimer -= Time.fixedDeltaTime;

        TryJump();
        ApplyJumpGravity();

        // Downhill = gravity flattened against the surface. On a flat patch or mid-air,
        // keep the last heading so the slap axis and the camera stay stable.
        Vector3 downhill = Vector3.ProjectOnPlane(Physics.gravity, groundNormal);
        if (downhill.sqrMagnitude > 0.0001f)
            SlideDirection = downhill.normalized;

        // Both axes are anchored to the SLOPE, never to current velocity. Deriving them
        // from velocity meant that once the block drifted sideways the "lateral" axis
        // rotated to point up the hill, so a slap could shove it uphill and left/right
        // stopped meaning left/right.
        Vector3 slopeForward = SlideDirection;
        Vector3 slopeRight = Vector3.Cross(groundNormal, slopeForward).normalized;

        float control = grounded ? 1f : airControlMultiplier;

        if (!Mathf.Approximately(pendingSlap, 0f))
        {
            // Cross-slope only. Perpendicular to downhill by construction, so a slap
            // can never add or remove speed along the slope.
            body.AddForce(slopeRight * (pendingSlap * slapImpulse * control), ForceMode.Impulse);
            pendingSlap = 0f;
        }

        if (dashQueued)
        {
            dashQueued = false;
            body.AddForce(slopeForward * (dashImpulse * control), ForceMode.Impulse);
        }

        DampLateralDrift(slopeRight);
        ClampSpeed();
    }

    /// <summary>
    /// Extra gravity, airborne only. Kept off the ground on purpose: the downhill slide
    /// is gravity-driven, so multiplying gravity while grounded would speed up the whole
    /// run as a side effect of retuning the jump.
    /// </summary>
    private void ApplyJumpGravity()
    {
        if (grounded)
        {
            jumpCutQueued = false;
            return;
        }

        float vertical = Vector3.Dot(body.linearVelocity, Vector3.up);

        if (jumpCutQueued)
        {
            jumpCutQueued = false;

            // Only cuts a rising jump. Releasing the button on the way down must not
            // yank the block toward the ground.
            if (vertical > 0f)
            {
                body.linearVelocity -= Vector3.up * (vertical * (1f - jumpCutMultiplier));
                vertical *= jumpCutMultiplier;
            }
        }

        float multiplier = vertical > 0f ? riseGravityMultiplier : fallGravityMultiplier;
        body.AddForce(Physics.gravity * (multiplier - 1f), ForceMode.Acceleration);
    }

    private void TryJump()
    {
        if (jumpBufferTimer <= 0f || coyoteTimer <= 0f)
            return;

        jumpBufferTimer = 0f;
        coyoteTimer = 0f;

        // Leave along the surface normal, not world up. What decides whether the block
        // separates from the ground is the velocity component along the normal, and a
        // world-up jump only contributes cos(slope) of itself to that: identical at 0
        // degrees, but 9% weaker at 25 and 29% weaker at 45. Using the normal keeps jump
        // strength constant however steep the hill gets.
        Vector3 normal = grounded ? groundNormal : Vector3.up;

        // Cancel only motion into the surface, so along-surface momentum is untouched.
        float into = Vector3.Dot(body.linearVelocity, normal);
        if (into < 0f)
            body.linearVelocity -= normal * into;

        body.AddForce(normal * jumpImpulse, ForceMode.Impulse);
    }

    private void ProbeGround()
    {
        float radius = sphere.radius * MaxAbsScale(transform.lossyScale);
        Vector3 origin = transform.TransformPoint(sphere.center);

        grounded = Physics.SphereCast(
            origin,
            radius * 0.95f,
            Vector3.down,
            out RaycastHit hit,
            radius * 0.35f + 0.05f,
            groundMask,
            QueryTriggerInteraction.Ignore);

        groundNormal = grounded ? hit.normal : Vector3.up;
    }

    /// <summary>
    /// Bleeds off sideways drift only. Downhill momentum is untouched, so the ice feel
    /// survives — this just stops every slap accumulating forever on a frictionless
    /// surface until the block parks against a rail. Set lateralDamping to 0 to disable.
    /// </summary>
    private void DampLateralDrift(Vector3 slopeRight)
    {
        if (!grounded || lateralDamping <= 0f)
            return;

        float sideways = Vector3.Dot(body.linearVelocity, slopeRight);
        float decayed = sideways * Mathf.Exp(-lateralDamping * Time.fixedDeltaTime);
        body.linearVelocity += slopeRight * (decayed - sideways);
    }

    private void ClampSpeed()
    {
        Vector3 velocity = body.linearVelocity;

        // Only the along-surface component is capped, so falling is never throttled.
        Vector3 planar = Vector3.ProjectOnPlane(velocity, groundNormal);
        if (planar.magnitude > maxSpeed)
            body.linearVelocity = planar.normalized * maxSpeed + (velocity - planar);
    }

    private void UpdateVisual()
    {
        if (visual == null)
            return;

        // Yaw only. Deriving this from SlideDirection rather than velocity keeps it
        // steady: velocity flips direction near zero and would snap the cube around.
        visual.rotation = Quaternion.Slerp(
            visual.rotation,
            UprightRotation(),
            visualTurnSpeed * Time.deltaTime);
    }

    /// <summary>Level with the world, facing downhill. No pitch, no roll, ever.</summary>
    private Quaternion UprightRotation()
    {
        Vector3 flat = SlideDirection;
        flat.y = 0f;

        if (flat.sqrMagnitude < 0.0001f)
            return visual != null ? visual.rotation : Quaternion.identity;

        return Quaternion.LookRotation(flat.normalized, Vector3.up);
    }

    private static float MaxAbsScale(Vector3 scale)
    {
        return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
    }
}
