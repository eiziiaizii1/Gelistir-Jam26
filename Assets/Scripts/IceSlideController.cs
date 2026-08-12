using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// Physics-driven downhill slide. Gravity plus slideAcceleration supply the descent, which
/// builds continuously; the mouse only slaps the block sideways (X) and dashes it forward
/// (Y), both as instantaneous impulses so momentum carries on the ice.
///
/// Descent and cross-slope drift are capped independently, so a slap is purely a steering
/// input: it adds lateral velocity and can never trade descent away to pay for it.
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
    [Tooltip("How far a dash may push descent ABOVE maxDescentSpeed. Without this the " +
             "speed cap deletes the dash on the next physics step whenever the block is " +
             "already cruising, which is most of a run. Keep it at or above dashImpulse " +
             "so the whole impulse survives.")]
    [SerializeField] private float dashOverspeed = 12f;
    [Tooltip("How fast that extra headroom bleeds back, in m/s per second. Lower makes a " +
             "dash carry further before the block settles to its normal top speed.")]
    [SerializeField] private float dashOverspeedDecay = 5f;

    [Header("Jump (right mouse button)")]
    [SerializeField] private float jumpImpulse = 10f;
    [Tooltip("Gravity multiplier while rising. Above 1 shortens the float at the top of " +
             "the arc, which is what stops a jump feeling floaty.")]
    [SerializeField] private float riseGravityMultiplier = 1.7f;
    [Tooltip("Gravity multiplier while falling. Still above the rise value for the 'up slow, " +
             "down fast' arc, but no longer so steep that the block arrives at the slope far " +
             "steeper than the slope itself descends — that mismatch is what read as being " +
             "yanked into the ground.")]
    [SerializeField] private float fallGravityMultiplier = 2f;
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
    [Tooltip("One impulse per press. The first qualifying swipe after the button goes down " +
             "fires, and the mouse then does nothing at all until the button is released and " +
             "pressed again — so a held drag cannot feed the block a stream of impulses. " +
             "Needs requireMouseHold, since without a press there is nothing to spend. " +
             "Uncheck for the old behaviour, where reversing or slowing the hand re-arms " +
             "another slap inside the same hold.")]
    [SerializeField] private bool oneImpulsePerPress = true;

    [Header("Input isolation")]
    [Tooltip("One axis must beat the other by this factor to claim the frame. Higher " +
             "means diagonal mouse movement is ignored instead of picking a winner.")]
    [SerializeField] private float axisDominance = 1.5f;

    [Header("Slide")]
    [Tooltip("Ceiling on DOWNHILL speed only. Cross-slope drift is capped separately, so " +
             "piling on lateral speed can never eat into the descent.")]
    [FormerlySerializedAs("maxSpeed")]
    [SerializeField] private float maxDescentSpeed = 60f;
    [Tooltip("Ceiling on cross-slope drift. Capped on its own axis so hitting the limit " +
             "steals nothing from the descent.")]
    [SerializeField] private float maxLateralSpeed = 30f;
    [Tooltip("Continuous downhill acceleration while grounded, on top of gravity. This is " +
             "what makes the block keep winding up on a shallow slope instead of settling " +
             "at whatever speed the incline alone gives. 0 = gravity only.")]
    [SerializeField] private float slideAcceleration = 6f;
    [Tooltip("How fast sideways drift bleeds off, per second. Downhill speed is never " +
             "damped. 0 = pure ice, drift persists until something stops it.")]
    [SerializeField] private float lateralDamping = 1.2f;
    [Tooltip("Impulse scale while airborne. Near 1 so a slap mid-jump lands with real " +
             "weight: air slaps are the point of jumping, and nothing damps drift while " +
             "off the ground, so a mid-air hit carries the whole arc.")]
    [SerializeField] private float airControlMultiplier = 0.85f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Airborne")]
    [Tooltip("Terminal downward speed while airborne. A safety net for long drops, so no " +
             "flight can build up a descent the landing has to violently undo.")]
    [SerializeField] private float maxFallSpeed = 22f;
    [Tooltip("Share of the into-slope speed the cushion sheds before touchdown. 1 = arrive " +
             "almost parallel to the surface.")]
    [Range(0f, 1f)]
    [SerializeField] private float landingSmoothing = 0.85f;
    [Tooltip("Share of the shed speed handed back as along-slope momentum, so a landing flows " +
             "into the slide rather than just deleting energy.")]
    [Range(0f, 1f)]
    [SerializeField] private float landingSpeedRecovery = 0.5f;
    [Tooltip("Height above the surface where the landing cushion starts working. This is what " +
             "makes the landing smooth rather than merely early: the descent is bled off " +
             "gradually across this distance instead of being cancelled in one step, which is " +
             "just as jarring as the collision doing it. Taller = softer and floatier.")]
    [SerializeField] private float landingCushionHeight = 2.5f;
    [Tooltip("Ceiling on cushion deceleration, m/s². Caps how hard the cushion can push back " +
             "in a single step, so a very fast drop still lands firmly instead of hitting an " +
             "invisible wall.")]
    [SerializeField] private float maxCushionAcceleration = 80f;

    [Header("Obstacle impacts")]
    [Tooltip("After a hit, descent is held at or above that hit's floor for this long. " +
             "Without it a solid obstacle pins the block: the contact zeroes velocity every " +
             "step and slideAcceleration just presses into the wall forever.")]
    [SerializeField] private float impactFloorDuration = 0.8f;

    [Header("Respawn")]
    [Tooltip("Drop below this world Y and the block returns to its start point. " +
             "Set to -1000000 to allow procedural endless downhill sliding.")]
    [SerializeField] private float killHeight = -1000000f;

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
    private bool gestureSpent;
    private float dashTimer;
    private bool dashQueued;
    private float overspeedAllowance;

    private float impactFloorSpeed;
    private float impactFloorTimer;

    private float coyoteTimer;
    private float jumpBufferTimer;
    private bool jumpCutQueued;

    private bool grounded;
    private Vector3 groundNormal = Vector3.up;

    private bool controlsEnabled = true;

    private Vector3 startPosition;

    /// <summary>Downhill heading projected onto the current surface. The camera follows this.</summary>
    public Vector3 SlideDirection { get; private set; } = Vector3.forward;

    /// <summary>Cross-slope axis, pointing to the block's right when facing downhill.</summary>
    public Vector3 SlopeRight { get; private set; } = Vector3.right;

    /// <summary>Raised when a lateral impulse lands, carrying the world-space impulse applied.</summary>
    public event System.Action<Vector3> Slapped;

    /// <summary>Raised on touchdown, carrying the speed the block hit the surface with.</summary>
    public event System.Action<float> Landed;

    /// <summary>Raised when a forward dash fires, carrying the world-space impulse applied.</summary>
    public event System.Action<Vector3> Dashed;

    /// <summary>Headroom the current dash still has above maxDescentSpeed, in m/s.</summary>
    public float DashOverspeedRemaining => overspeedAllowance;

    /// <summary>False once the run has ended: physics still runs, but input is ignored.</summary>
    public bool ControlsEnabled => controlsEnabled;

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

    /// <summary>
    /// Bleeds descent speed after hitting an obstacle, without ever bringing the run to a
    /// halt. Descent is scaled down rather than zeroed and is held at a floor, so the block
    /// keeps sliding and slideAcceleration immediately starts winding it back up: a hit
    /// costs momentum and time, it does not end the run.
    ///
    /// The floor also covers the case where the physics collision itself killed the
    /// velocity, which a solid obstacle will happily do.
    /// </summary>
    /// <param name="fraction">Portion of current descent to remove, 0..1.</param>
    /// <param name="floorSpeed">Descent speed to keep no matter how hard the hit was.</param>
    public void SlowDescent(float fraction, float floorSpeed)
    {
        Vector3 forwardAxis = Vector3.ProjectOnPlane(SlideDirection, groundNormal);
        if (forwardAxis.sqrMagnitude < 0.0001f)
            return;

        forwardAxis.Normalize();

        Vector3 velocity = body.linearVelocity;
        float descent = Vector3.Dot(velocity, forwardAxis);
        float target = Mathf.Max(descent * (1f - Mathf.Clamp01(fraction)), floorSpeed);

        body.linearVelocity = velocity + forwardAxis * (target - descent);

        // Hold that floor for a moment. Setting it once is not enough against a solid
        // obstacle: the contact zeroes velocity again on the very next step.
        impactFloorSpeed = floorSpeed;
        impactFloorTimer = impactFloorDuration;

        // A hit ends any dash boost. Letting the headroom survive would let the block
        // shrug off an obstacle it should have been punished for.
        overspeedAllowance = 0f;
    }

    /// <summary>
    /// Keeps the block crawling out of an obstacle it is pressed against, for a short window
    /// after the hit. Runs after ClampSpeed so it is the last word on descent.
    /// </summary>
    private void HoldImpactFloor(Vector3 slopeForward)
    {
        if (impactFloorTimer <= 0f)
            return;

        impactFloorTimer -= Time.fixedDeltaTime;

        Vector3 forwardAxis = Vector3.ProjectOnPlane(slopeForward, groundNormal);
        if (forwardAxis.sqrMagnitude < 0.0001f)
            return;

        forwardAxis.Normalize();

        float descent = Vector3.Dot(body.linearVelocity, forwardAxis);
        if (descent < impactFloorSpeed)
            body.linearVelocity += forwardAxis * (impactFloorSpeed - descent);
    }

    /// <summary>
    /// Turns descent into a short hop off a ramp. Horizontal momentum is kept, so the block
    /// carries its run through the air instead of stalling at the lip.
    ///
    /// Nothing here zeroes velocity: the vertical component is only ever raised. If the block
    /// is already travelling upward — which it is, after physically riding up the ramp face —
    /// that speed is kept and the boost adds to it, rather than replacing it and stealing lift
    /// the ramp had already earned.
    ///
    /// Gravity does the rest. slideAcceleration is grounded-only and the airborne gravity
    /// multiplier is already tuned, so the arc falls off naturally with no extra bookkeeping.
    /// </summary>
    /// <param name="redirectEfficiency">Share of downward speed converted to upward.</param>
    /// <param name="extraUpSpeed">Flat upward speed added on top, m/s.</param>
    /// <param name="horizontalRetention">Multiplier on horizontal velocity. 1 keeps all of it.</param>
    public void LaunchFromRamp(float redirectEfficiency, float extraUpSpeed, float horizontalRetention)
    {
        Vector3 velocity = body.linearVelocity;

        float falling = Mathf.Max(0f, -velocity.y);
        float rising = Mathf.Max(0f, velocity.y);

        float up = rising + falling * Mathf.Clamp01(redirectEfficiency) + Mathf.Max(0f, extraUpSpeed);

        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z) * Mathf.Clamp(horizontalRetention, 0f, 1f);
        body.linearVelocity = horizontal + Vector3.up * up;

        // A ramp is not an obstacle recovery. Leaving the impact floor armed would keep
        // forcing descent along the slope while the block is meant to be flying.
        impactFloorTimer = 0f;
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
        gestureSpent = false;
        dashQueued = false;
        dashTimer = 0f;
        overspeedAllowance = 0f;
        impactFloorTimer = 0f;
        wasHeld = false;
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        jumpCutQueued = false;
        controlsEnabled = true;

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
        if (controlsEnabled)
        {
            ReadJump();
            ReadMouse();
        }

        UpdateVisual();
    }

    /// <summary>
    /// Turns player input on or off. The body keeps sliding under physics either way - this
    /// only stops steering, slapping and jumping, so the run can be locked out once the game
    /// is over while the cube still coasts to a stop.
    /// </summary>
    public void SetControlsEnabled(bool enabled)
    {
        controlsEnabled = enabled;

        if (!enabled)
        {
            // Drop everything queued but not yet applied, so nothing fires on the next
            // physics step after control is taken away.
            pendingSlap = 0f;
            swingActive = false;
            swingSign = 0f;
            dashQueued = false;
            gestureSpent = false;
            wasHeld = false;
            jumpBufferTimer = 0f;
            jumpCutQueued = false;
        }
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

            // Releasing is what re-arms the next slap.
            gestureSpent = false;
            wasHeld = false;
            return;
        }

        Vector2 delta = mouse.delta.ReadValue();

        // Movement made before the click shouldn't count as a swing. Skip the press
        // frame so grabbing the mouse and then clicking doesn't fire a stray slap.
        if (!wasHeld)
        {
            wasHeld = true;
            gestureSpent = false;
            return;
        }

        // This press has already produced its impulse. Read nothing, queue nothing: further
        // dragging must have no physical effect at all until the button is released.
        if (oneImpulsePerPress && gestureSpent)
            return;

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
            // Reversing mid-drag re-arms a slap in legacy mode only. Under one-per-press a
            // flick back the other way is part of the same spent gesture, not a new slap.
            bool reversed = !oneImpulsePerPress && swingSign != 0f && Mathf.Sign(speedX) != swingSign;

            if (!swingActive || reversed)
            {
                // Strength comes from how fast the hand was moving when the swing began.
                // The block follows the drag: a right swipe knocks it right.
                float strength = Mathf.Min(horizontal / slapSpeedReference, maxSlapScale);
                pendingSlap = Mathf.Sign(speedX) * strength;

                swingActive = true;
                swingSign = Mathf.Sign(speedX);
                gestureSpent = true;
            }
        }
        else if (forwardWins)
        {
            if (dashTimer <= 0f)
            {
                dashQueued = true;
                dashTimer = dashCooldown;

                // A dash spends the press too. The gesture resolves to one impulse, whichever
                // axis won it, so a hold can never keep producing pushes of either kind.
                gestureSpent = true;
            }
        }

        // Deliberately outside the branch chain above: folding this in as another
        // "else if" would swallow the dash branch on any frame with little sideways
        // movement, which is exactly when a forward push happens.
        // Legacy mode only. Re-arming on a slow hand is exactly what made a single held drag
        // machine-gun impulses: any dip below this speed opened the gate again mid-gesture.
        // Under one-per-press only releasing the button re-arms.
        if (!oneImpulsePerPress && horizontal < slapReleaseSpeed)
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

        // Captured before ProbeGround so a landing can report the speed it arrived with,
        // not the post-collision speed.
        bool wasGrounded = grounded;
        float approachSpeed = -Vector3.Dot(body.linearVelocity, groundNormal);

        ProbeGround();

        if (grounded && !wasGrounded && Landed != null)
            Landed(Mathf.Max(0f, approachSpeed));

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
        {
            Vector3 candidate = downhill.normalized;

            // Never let the heading reverse. Riding up a ramp puts the block on a face that
            // tilts back up the course, and steepest-descent on that face points BACKWARDS —
            // so this would flip SlideDirection (and with it SlopeRight) for the fraction of a
            // second the block is on the ramp. That swung the camera around and put the slap
            // hand on the wrong side. A ramp is a local feature, not a change of course.
            if (Vector3.Dot(candidate, SlideDirection) > 0f)
                SlideDirection = candidate;
        }

        // Both axes are anchored to the SLOPE, never to current velocity. Deriving them
        // from velocity meant that once the block drifted sideways the "lateral" axis
        // rotated to point up the hill, so a slap could shove it uphill and left/right
        // stopped meaning left/right.
        Vector3 slopeForward = SlideDirection;
        Vector3 slopeRight = Vector3.Cross(groundNormal, slopeForward).normalized;
        SlopeRight = slopeRight;

        // Continuous wind-up along the slope. Applied as acceleration so it is mass- and
        // melt-independent, and only while grounded: airborne descent is already handled
        // by fallGravityMultiplier, and doing both would double-dip.
        if (grounded && slideAcceleration > 0f)
            body.AddForce(slopeForward * slideAcceleration, ForceMode.Acceleration);

        float control = grounded ? 1f : airControlMultiplier;

        if (!Mathf.Approximately(pendingSlap, 0f))
        {
            // Cross-slope only. Perpendicular to downhill by construction, so a slap
            // can never add or remove speed along the slope.
            Vector3 impulse = slopeRight * (pendingSlap * slapImpulse * control);
            body.AddForce(impulse, ForceMode.Impulse);
            pendingSlap = 0f;

            if (Slapped != null)
                Slapped(impulse);
        }

        // Headroom bleeds off continuously, so the surge decays back to normal top speed
        // instead of ending abruptly. Done before the dash below so a fresh dash always
        // gets its full allowance.
        if (overspeedAllowance > 0f)
            overspeedAllowance = Mathf.MoveTowards(overspeedAllowance, 0f, dashOverspeedDecay * Time.fixedDeltaTime);

        if (dashQueued)
        {
            dashQueued = false;

            Vector3 dash = slopeForward * (dashImpulse * control);
            body.AddForce(dash, ForceMode.Impulse);

            // Lift the descent ceiling so the impulse actually survives. Max, not +=, so
            // hammering the dash cannot stack headroom into unbounded speed.
            overspeedAllowance = Mathf.Max(overspeedAllowance, dashOverspeed);

            if (Dashed != null)
                Dashed(dash);
        }

        DampLateralDrift(slopeRight);
        ClampSpeed(slopeForward, slopeRight);
        HoldImpactFloor(slopeForward);

        // Last, so they act on the velocity this step will actually be integrated with.
        ClampFallSpeed();
        CushionLanding();
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

    /// <summary>Terminal velocity, so a long drop can't build a descent that has to be undone.</summary>
    private void ClampFallSpeed()
    {
        if (grounded || maxFallSpeed <= 0f)
            return;

        Vector3 velocity = body.linearVelocity;
        if (velocity.y < -maxFallSpeed)
            body.linearVelocity = new Vector3(velocity.x, -maxFallSpeed, velocity.z);
    }

    /// <summary>
    /// Suspension for the touchdown: bleeds the into-slope speed off gradually across the last
    /// landingCushionHeight metres, so the block arrives nearly parallel to the surface and the
    /// collision has almost nothing left to resolve.
    ///
    /// Spreading it out is the whole point. Cancelling the descent in a single step — whether
    /// the collision does it or this code does — is a velocity discontinuity either way, and
    /// reads as the same snap. Here the required deceleration is solved from v² = 2·a·d, so it
    /// is a smooth continuous push of the same order as gravity rather than one hard stop.
    ///
    /// Part of the shed speed is handed to along-slope momentum, so landing carries the run
    /// forward instead of just costing energy.
    /// </summary>
    private void CushionLanding()
    {
        if (grounded || landingCushionHeight <= 0f)
            return;

        Vector3 velocity = body.linearVelocity;
        if (velocity.y >= 0f)
            return;

        float radius = sphere.radius * MaxAbsScale(transform.lossyScale);
        Vector3 origin = transform.TransformPoint(sphere.center);

        if (!Physics.SphereCast(
                origin,
                radius * 0.95f,
                Vector3.down,
                out RaycastHit hit,
                landingCushionHeight,
                groundMask,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }

        float into = -Vector3.Dot(velocity, hit.normal);
        if (into <= 0.5f)
            return;

        // Deceleration that brings the approach down to its target by the time the surface is
        // reached. Solved over the remaining gap, so it eases in as the ground gets closer.
        float gap = Mathf.Max(hit.distance, 0.05f);
        float target = into * (1f - landingSmoothing);
        float decel = Mathf.Min((into * into - target * target) / (2f * gap), maxCushionAcceleration);
        if (decel <= 0f)
            return;

        float delta = decel * Time.fixedDeltaTime;
        Vector3 cushioned = velocity + hit.normal * delta;

        Vector3 alongSlope = Vector3.ProjectOnPlane(SlideDirection, hit.normal);
        if (alongSlope.sqrMagnitude > 0.0001f)
            cushioned += alongSlope.normalized * (delta * landingSpeedRecovery);

        body.linearVelocity = cushioned;
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

    /// <summary>
    /// Caps descent and cross-slope drift on their own axes.
    ///
    /// This used to clamp the combined along-surface vector and rescale it as a whole,
    /// which coupled the two axes: lateral speed added by a slap pushed the total over the
    /// cap, and the correction came out of BOTH components. Slapping therefore slowed the
    /// descent, and each further slap slowed it again. Splitting the cap keeps a slap
    /// purely lateral, so downhill momentum only ever grows.
    /// </summary>
    private void ClampSpeed(Vector3 slopeForward, Vector3 slopeRight)
    {
        Vector3 velocity = body.linearVelocity;
        Vector3 planar = Vector3.ProjectOnPlane(velocity, groundNormal);
        Vector3 alongNormal = velocity - planar;

        // slopeForward is only guaranteed to lie in the surface plane while grounded;
        // airborne it is the last remembered heading and can tilt out of it. Re-project so
        // the two axes are a true orthonormal basis of the plane and the split below is
        // lossless — otherwise rebuilding the vector would distort velocity in mid-air.
        Vector3 forwardAxis = Vector3.ProjectOnPlane(slopeForward, groundNormal);
        if (forwardAxis.sqrMagnitude < 0.0001f)
            return;

        forwardAxis.Normalize();
        Vector3 rightAxis = Vector3.Cross(groundNormal, forwardAxis).normalized;

        float descent = Vector3.Dot(planar, forwardAxis);
        float lateral = Vector3.Dot(planar, rightAxis);

        // Only the downhill direction is capped. Motion back up the slope is left alone,
        // so bouncing off a rail or an obstacle is never silently amplified.
        float clampedDescent = Mathf.Min(descent, maxDescentSpeed + overspeedAllowance);
        float clampedLateral = Mathf.Clamp(lateral, -maxLateralSpeed, maxLateralSpeed);

        if (Mathf.Approximately(clampedDescent, descent) && Mathf.Approximately(clampedLateral, lateral))
            return;

        body.linearVelocity = forwardAxis * clampedDescent + rightAxis * clampedLateral + alongNormal;
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
