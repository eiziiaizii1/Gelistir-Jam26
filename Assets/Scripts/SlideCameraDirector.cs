using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;

/// <summary>
/// Drives a CinemachineCamera from the player's motion so speed is felt through the lens
/// rather than only read off the character. Handles four things, all keyed to how fast
/// the block is actually travelling:
///
///   FOV       widens with speed, so the world stretches past the edges of the frame
///   Dutch     rolls into lateral drift, so slaps read as weight shifting
///   Damping   tightens at speed, so the camera stops feeling like it is being towed
///   Anticipation  pushes the look target downhill, so the camera leads the run
///
/// Everything is smoothed. Snapping any of these to raw velocity reads as camera jitter,
/// especially the dutch, because lateral velocity changes discontinuously on every slap.
/// </summary>
// Must run before CinemachineBrain (order 0) resolves the camera, otherwise the brain
// consumes last frame's follow offset, damping and look target. With both at order 0 the
// ordering is undefined and can differ frame to frame, which reads as jitter.
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(CinemachineCamera))]
public class SlideCameraDirector : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private IceSlideController controller;
    [SerializeField] private Rigidbody body;
    [Tooltip("Empty transform the camera aims at. Led downhill to anticipate motion.")]
    [SerializeField] private Transform lookTarget;

    [Header("Speed reference")]
    [Tooltip("Speed treated as 'flat out'. All the effects below reach full strength here.")]
    [SerializeField] private float referenceSpeed = 34f;
    [Tooltip("Smoothing on the speed reading itself. Every effect reads this one value, " +
             "so filtering here keeps physics-step noise out of all of them at once.")]
    [SerializeField] private float speedSmoothTime = 0.25f;

    [Header("Field of view")]
    [SerializeField] private float baseFov = 58f;
    [SerializeField] private float maxFov = 76f;
    [Tooltip("Seconds for FOV to catch up. Slow on purpose: fast FOV tracking reads as " +
             "the lens breathing on every bump.")]
    [SerializeField] private float fovSmoothTime = 0.45f;
    [Tooltip("Extra degrees kicked in briefly when a dash or hard slap lands.")]
    [SerializeField] private float fovPunch = 6f;
    [SerializeField] private float fovPunchDecay = 3.5f;

    [Header("Dutch tilt")]
    [Tooltip("Max roll in degrees at full lateral drift.")]
    [SerializeField] private float maxDutch = 7f;
    [Tooltip("Lateral speed that produces full roll.")]
    [SerializeField] private float dutchReferenceSpeed = 14f;
    [SerializeField] private float dutchSmoothTime = 0.28f;

    [Header("Follow offset")]
    [SerializeField] private float followDistance = 9.5f;
    [SerializeField] private float followHeight = 3.6f;
    [Tooltip("Extra distance pulled back at full speed, so speed opens the framing up.")]
    [SerializeField] private float speedPullback = 3.2f;

    [Header("Damping")]
    [Tooltip("Position damping when crawling. Loose and floaty.")]
    [SerializeField] private Vector3 slowDamping = new Vector3(0.7f, 0.7f, 0.7f);
    [Tooltip("Position damping at full speed. Tighter, so the camera feels driven.")]
    [SerializeField] private Vector3 fastDamping = new Vector3(0.25f, 0.35f, 0.2f);

    [Header("Anticipation")]
    [Tooltip("How far downhill the look target leads at full speed.")]
    [SerializeField] private float lookAheadDistance = 11f;
    [SerializeField] private float lookAheadHeight = 1.4f;
    [SerializeField] private float lookAheadSmoothTime = 0.25f;

    private CinemachineCamera cam;
    private CinemachineFollow follow;

    private float smoothedSpeed01;
    private float speedVelocity;
    private float fovVelocity;
    private float dutchValue;
    private float dutchVelocity;
    private float punch;
    private Vector3 lookTargetVelocity;

    private void Awake()
    {
        cam = GetComponent<CinemachineCamera>();
        follow = GetComponent<CinemachineFollow>();

        if (controller == null && cam.Follow != null)
            controller = cam.Follow.GetComponentInParent<IceSlideController>();

        if (body == null && controller != null)
            body = controller.GetComponent<Rigidbody>();

        cam.Lens.FieldOfView = baseFov;
    }

    private void OnEnable()
    {
        if (controller != null)
            controller.Slapped += OnSlapped;
    }

    private void OnDisable()
    {
        if (controller != null)
            controller.Slapped -= OnSlapped;
    }

    private void OnSlapped(Vector3 impulse)
    {
        // Only a genuinely hard hit punches the lens; light taps would make it flutter.
        float strength = Mathf.InverseLerp(6f, 18f, impulse.magnitude);
        punch = Mathf.Max(punch, strength);
    }

    private void LateUpdate()
    {
        if (controller == null || body == null)
            return;

        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        Vector3 velocity = body.linearVelocity;
        float alongSlope = Mathf.Max(0f, Vector3.Dot(velocity, controller.SlideDirection));
        float rawSpeed01 = Mathf.Clamp01(alongSlope / Mathf.Max(referenceSpeed, 0.001f));

        // Smoothed once, here, because every effect below reads it. Rigidbody velocity
        // changes in 60 Hz steps while this runs per rendered frame, so feeding the raw
        // value into the follow offset made the camera distance twitch every frame.
        // One filter on the noisy input beats a filter on each output.
        float speed01 = Mathf.SmoothDamp(
            smoothedSpeed01, rawSpeed01, ref speedVelocity, speedSmoothTime);
        smoothedSpeed01 = speed01;

        UpdateFov(speed01, dt);
        UpdateDutch(velocity, dt);
        UpdateFollow(speed01, dt);
        UpdateLookTarget(speed01, dt);
    }

    private void UpdateFov(float speed01, float dt)
    {
        punch = Mathf.MoveTowards(punch, 0f, fovPunchDecay * dt);

        float target = Mathf.Lerp(baseFov, maxFov, speed01) + punch * fovPunch;

        cam.Lens.FieldOfView = Mathf.SmoothDamp(
            cam.Lens.FieldOfView, target, ref fovVelocity, fovSmoothTime);
    }

    private void UpdateDutch(Vector3 velocity, float dt)
    {
        // Roll into the drift. Sign is negated so the camera banks the way a rider would
        // lean, rather than away from the turn.
        float lateral = Vector3.Dot(velocity, controller.SlopeRight);
        float target = -Mathf.Clamp(lateral / Mathf.Max(dutchReferenceSpeed, 0.001f), -1f, 1f) * maxDutch;

        dutchValue = Mathf.SmoothDamp(dutchValue, target, ref dutchVelocity, dutchSmoothTime);
        cam.Lens.Dutch = dutchValue;
    }

    private void UpdateFollow(float speed01, float dt)
    {
        if (follow == null)
            return;

        // Offset is rebuilt in world space from the slide direction each frame, so the rig
        // stays behind the block on any slope orientation rather than assuming +Z downhill.
        Vector3 back = controller.SlideDirection;
        back.y = 0f;
        if (back.sqrMagnitude < 0.0001f)
            back = Vector3.forward;
        back.Normalize();

        // Written straight through. CinemachineFollow already damps its way to the offset,
        // so smoothing here too stacked two filters in series on the same value and made
        // the rig lag unevenly whenever frame times varied.
        follow.FollowOffset = -back * (followDistance + speedPullback * speed01)
                              + Vector3.up * followHeight;

        TrackerSettings tracker = follow.TrackerSettings;
        tracker.PositionDamping = Vector3.Lerp(slowDamping, fastDamping, speed01);
        follow.TrackerSettings = tracker;
    }

    private void UpdateLookTarget(float speed01, float dt)
    {
        if (lookTarget == null || cam.Follow == null)
            return;

        Vector3 ahead = controller.SlideDirection * (lookAheadDistance * speed01);
        Vector3 target = cam.Follow.position + ahead + Vector3.up * lookAheadHeight;

        lookTarget.position = Vector3.SmoothDamp(
            lookTarget.position, target, ref lookTargetVelocity, lookAheadSmoothTime);
    }
}
