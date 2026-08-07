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
    [Header("Lateral slap (mouse X, inverted)")]
    [Tooltip("Impulse from one slap. Dragging RIGHT hits the block's right side and " +
             "shoves it LEFT, and vice versa.")]
    [SerializeField] private float slapImpulse = 5f;
    [Tooltip("Mouse pixels of horizontal travel in a frame before a slap registers.")]
    [SerializeField] private float slapThreshold = 4f;
    [Tooltip("Minimum seconds between slaps, so a fast drag reads as hits, not a push.")]
    [SerializeField] private float slapCooldown = 0.08f;
    [Tooltip("Hardest slap allowed, as a multiple of slapImpulse.")]
    [SerializeField] private float maxSlapScale = 2.5f;

    [Header("Forward dash (mouse Y)")]
    [SerializeField] private float dashImpulse = 10f;
    [SerializeField] private float dashThreshold = 6f;
    [SerializeField] private float dashCooldown = 0.6f;

    [Header("Input isolation")]
    [Tooltip("One axis must beat the other by this factor to claim the frame. Higher " +
             "means diagonal mouse movement is ignored instead of picking a winner.")]
    [SerializeField] private float axisDominance = 1.5f;

    [Header("Slide")]
    [SerializeField] private float maxSpeed = 40f;
    [Tooltip("How fast sideways drift bleeds off, per second. Downhill speed is never " +
             "damped. 0 = pure ice, drift persists until something stops it.")]
    [SerializeField] private float lateralDamping = 1.2f;
    [Tooltip("Impulse scale while airborne.")]
    [SerializeField] private float airControlMultiplier = 0.25f;
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

    private float pendingSlap;
    private float slapTimer;
    private float dashTimer;
    private bool dashQueued;

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
        slapTimer = 0f;
        dashQueued = false;
        dashTimer = 0f;

        if (visual != null)
            visual.rotation = UprightRotation();
    }

    private void Update()
    {
        if (slapTimer > 0f)
            slapTimer -= Time.deltaTime;

        if (dashTimer > 0f)
            dashTimer -= Time.deltaTime;

        ReadMouse();
        UpdateVisual();
    }

    private void ReadMouse()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        Vector2 delta = mouse.delta.ReadValue();
        float horizontal = Mathf.Abs(delta.x);
        float forward = delta.y;

        // Exactly one branch can run per frame. Horizontal movement resolves to a slap
        // and nothing else; forward movement resolves to a dash and nothing else.
        bool horizontalWins = horizontal >= slapThreshold && horizontal > Mathf.Abs(forward) * axisDominance;
        bool forwardWins = forward >= dashThreshold && forward > horizontal * axisDominance;

        if (horizontalWins)
        {
            if (slapTimer <= 0f)
            {
                // Inverted: a drag to the right is a hit on the right side, so the
                // block is knocked to the left.
                float strength = Mathf.Min(horizontal / slapThreshold, maxSlapScale);
                pendingSlap = -Mathf.Sign(delta.x) * strength;
                slapTimer = slapCooldown;
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
    }

    private void FixedUpdate()
    {
        if (transform.position.y < killHeight)
        {
            Respawn();
            return;
        }

        ProbeGround();

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
