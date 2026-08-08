using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Turns gameplay impacts into Cinemachine impulses. Lives on the player next to a
/// CinemachineImpulseSource; any vcam carrying a CinemachineImpulseListener picks the
/// signal up, so the shake survives camera changes without extra wiring.
///
/// Shakes are directional: a slap to the right kicks the camera right, so the hit reads
/// as a physical shove rather than generic rumble.
/// </summary>
[RequireComponent(typeof(CinemachineImpulseSource))]
public class SlideImpactShake : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private IceSlideController controller;

    [Header("Slap")]
    [Tooltip("Shake velocity per 1 m/s of slap impulse. Kept low on purpose: the slap " +
             "should register as a punch, not throw the whole frame around. Slapping is the " +
             "main verb, so this fires constantly — it has to stay under the threshold where " +
             "the player notices the camera instead of the hit.")]
    [SerializeField] private float slapShakeScale = 0.028f;
    [Tooltip("Ceiling on slap shake, so a max-strength flick can't blow out the view.")]
    [SerializeField] private float maxSlapShake = 0.45f;
    [Tooltip("Fraction of the kick pushed back along the camera's view axis, which adds " +
             "a little punch-in without making the shake feel loose.")]
    [SerializeField] private float slapPunchBack = 0.35f;

    [Header("Dash")]
    [Tooltip("Shake velocity for a forward dash. Directed along the slide, so the kick " +
             "reads as the block being shoved forward rather than jolted at random.")]
    [SerializeField] private float dashShake = 0.35f;

    [Header("Landing")]
    [Tooltip("Ignore touchdowns softer than this, so settling on the slope stays quiet. " +
             "Raised above the speed the block skips over minor bumps at, which is what made " +
             "the shake feel constant rather than event-driven.")]
    [SerializeField] private float landingMinSpeed = 7f;
    [Tooltip("Shake velocity per 1 m/s of landing impact.")]
    [SerializeField] private float landingShakeScale = 0.03f;
    [SerializeField] private float maxLandingShake = 0.55f;

    private CinemachineImpulseSource source;

    private void Awake()
    {
        source = GetComponent<CinemachineImpulseSource>();

        if (controller == null)
            controller = GetComponent<IceSlideController>();
    }

    private void OnEnable()
    {
        if (controller == null)
            return;

        controller.Slapped += OnSlapped;
        controller.Landed += OnLanded;
        controller.Dashed += OnDashed;
    }

    private void OnDisable()
    {
        if (controller == null)
            return;

        controller.Slapped -= OnSlapped;
        controller.Landed -= OnLanded;
        controller.Dashed -= OnDashed;
    }

    private void OnSlapped(Vector3 impulse)
    {
        float magnitude = Mathf.Min(impulse.magnitude * slapShakeScale, maxSlapShake);
        if (magnitude <= 0.0001f)
            return;

        Vector3 direction = impulse.normalized;

        // Bias the kick slightly away from the slide direction so it also reads as a jolt
        // toward the viewer, not purely a sideways slide of the frame.
        direction -= controller.SlideDirection * slapPunchBack;

        source.GenerateImpulseWithVelocity(direction.normalized * magnitude);
    }

    /// <summary>
    /// Kicks the camera backwards along the slide, which is what a shove forward looks
    /// like from behind the block, and fires the whoosh so the dash is heard as well as
    /// seen. Both matter: a dash that only changes a number reads as nothing happening.
    /// </summary>
    private void OnDashed(Vector3 impulse)
    {
        if (dashShake > 0.0001f)
            source.GenerateImpulseWithVelocity(-controller.SlideDirection * dashShake);

        if (IceEscape.IceAudioManager.Instance != null)
            IceEscape.IceAudioManager.Instance.PlaySlapWhoosh();
    }

    private void OnLanded(float impactSpeed)
    {
        if (impactSpeed < landingMinSpeed)
            return;

        float magnitude = Mathf.Min(impactSpeed * landingShakeScale, maxLandingShake);

        // Straight down: a landing should compress the view, not sway it sideways.
        source.GenerateImpulseWithVelocity(Vector3.down * magnitude);
    }
}
