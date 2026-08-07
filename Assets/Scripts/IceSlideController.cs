using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Physics-driven downhill slide. Gravity supplies the forward motion; the mouse only
/// steers (X) and dashes (Y). The rigidbody is a frictionless sphere, the visible cube
/// is a child that gets oriented and banked to sell the "sliding block of ice" look.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class IceSlideController : MonoBehaviour
{
    [Header("Steering (mouse X)")]
    [SerializeField] private float steerForce = 30f;
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float steerSmoothing = 12f;

    [Header("Dash (mouse forward / Y)")]
    [SerializeField] private float dashImpulse = 12f;
    [SerializeField] private float dashThreshold = 5f;
    [SerializeField] private float dashCooldown = 0.6f;

    [Header("Slide")]
    [SerializeField] private float maxSpeed = 40f;
    [SerializeField] private float airControlMultiplier = 0.25f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Respawn")]
    [Tooltip("Drop below this world Y and the block returns to its start point. " +
             "Must sit clear below the bottom of the slope (~-30 for the default hill).")]
    [SerializeField] private float killHeight = -60f;

    [Header("Visual")]
    [SerializeField] private Transform visual;
    [SerializeField] private float bankAngle = 18f;
    [SerializeField] private float visualTurnSpeed = 8f;

    private Rigidbody body;
    private SphereCollider sphere;

    private float steerInput;
    private float smoothedSteer;
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

        // Rolling would tumble the ice block; drive orientation from the visual instead.
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (visual == null && transform.childCount > 0)
            visual = transform.GetChild(0);

        startPosition = transform.position;
    }

    /// <summary>Return to the start of the slope with all momentum cleared.</summary>
    public void Respawn()
    {
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        transform.position = startPosition;

        smoothedSteer = 0f;
        steerInput = 0f;
        dashQueued = false;
        dashTimer = 0f;
    }

    private void Update()
    {
        ReadMouse();

        if (dashTimer > 0f)
            dashTimer -= Time.deltaTime;

        UpdateVisual();
    }

    private void ReadMouse()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        Vector2 delta = mouse.delta.ReadValue();

        steerInput = Mathf.Clamp(delta.x * mouseSensitivity, -1f, 1f);

        // Pushing the mouse forward (up the desk) fires a burst of speed.
        if (delta.y >= dashThreshold && dashTimer <= 0f)
        {
            dashQueued = true;
            dashTimer = dashCooldown;
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
        // keep the last heading so steering and the camera stay stable.
        Vector3 downhill = Vector3.ProjectOnPlane(Physics.gravity, groundNormal);
        if (downhill.sqrMagnitude > 0.0001f)
            SlideDirection = downhill.normalized;

        Vector3 planarVelocity = Vector3.ProjectOnPlane(body.linearVelocity, groundNormal);
        Vector3 heading = planarVelocity.sqrMagnitude > 1f ? planarVelocity.normalized : SlideDirection;
        Vector3 lateral = Vector3.Cross(groundNormal, heading).normalized;

        smoothedSteer = Mathf.Lerp(smoothedSteer, steerInput, steerSmoothing * Time.fixedDeltaTime);

        float control = grounded ? 1f : airControlMultiplier;
        body.AddForce(-lateral * (smoothedSteer * steerForce * control), ForceMode.Acceleration);

        if (dashQueued)
        {
            dashQueued = false;
            body.AddForce(heading * dashImpulse, ForceMode.VelocityChange);
        }

        ClampSpeed();

        // Mouse delta is per-frame; without this the last value would repeat when the
        // mouse stops moving.
        steerInput = 0f;
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

        Vector3 planarVelocity = Vector3.ProjectOnPlane(body.linearVelocity, groundNormal);
        Vector3 forward = planarVelocity.sqrMagnitude > 1f ? planarVelocity.normalized : SlideDirection;

        Quaternion target = Quaternion.LookRotation(forward, groundNormal)
                            * Quaternion.Euler(0f, 0f, -smoothedSteer * bankAngle);

        visual.rotation = Quaternion.Slerp(visual.rotation, target, visualTurnSpeed * Time.deltaTime);
    }

    private static float MaxAbsScale(Vector3 scale)
    {
        return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
    }
}
