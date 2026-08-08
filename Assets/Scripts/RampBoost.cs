using UnityEngine;

namespace IceEscape
{
    /// <summary>
    /// Launch ramp. The wedge itself is a solid collider, so most of the redirect is real
    /// physics — the block rides up the face and its velocity turns upward on its own. This
    /// component only adds the pop at the lip that turns a ride-up into a short flight.
    ///
    /// Splitting it that way is deliberate: a purely scripted launch off a trigger reads as
    /// the block teleporting into the air, and a purely physical ramp at these speeds barely
    /// lifts at all. The trigger sits near the top of the wedge so the physical ride has
    /// already happened by the time the boost lands.
    ///
    /// Unlike SpeedBoostPad, which just adds forward force, nothing here resets velocity.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class RampBoost : MonoBehaviour
    {
        [Header("Launch window")]
        [Tooltip("Approach speed below which the ramp does nothing and the block just slides " +
                 "over it. Stops a crawling block being flicked into the air.")]
        [SerializeField] private float minLaunchSpeed = 12f;
        [Tooltip("Approach speed that earns the full boost. Between this and minLaunchSpeed " +
                 "the launch scales up, so a faster run visibly flies further.")]
        [SerializeField] private float referenceSpeed = 35f;

        [Header("Launch")]
        [Tooltip("Share of the block's downward speed turned upward. Kept well under 1 " +
                 "because the solid ramp face has already done part of this job.")]
        [Range(0f, 1f)]
        [SerializeField] private float redirectEfficiency = 0.35f;
        [Tooltip("Extra upward speed at a full-speed approach, m/s. This is the knob for how " +
                 "high the hop goes — raise it for more air, not redirectEfficiency.")]
        [SerializeField] private float launchSpeed = 4f;
        [Tooltip("Multiplier on horizontal velocity through the launch. 1 keeps the whole run; " +
                 "drop slightly below 1 if the ramp should cost a little speed.")]
        [Range(0.5f, 1f)]
        [SerializeField] private float horizontalRetention = 1f;

        [Header("Feedback")]
        [Tooltip("Left at 0 by default. The arc itself reads clearly, and the camera is " +
                 "deliberately calm — a launch is not an impact.")]
        [SerializeField] private float launchShake = 0f;
        [SerializeField] private Color sparkleColor = new Color(0.65f, 0.95f, 1f);
        [SerializeField] private bool playAudio = true;

        [Header("Re-trigger guard")]
        [SerializeField] private float cooldown = 0.5f;

        private float nextLaunchTime;

        private void OnTriggerEnter(Collider other)
        {
            TryLaunch(other.gameObject);
        }

        private void TryLaunch(GameObject target)
        {
            if (Time.time < nextLaunchTime)
                return;

            Rigidbody body = target.GetComponentInParent<Rigidbody>();
            if (body == null)
                return;

            IceSlideController slide = body.GetComponent<IceSlideController>();
            if (slide == null)
                return;

            float approach = body.linearVelocity.magnitude;
            if (approach < minLaunchSpeed)
                return;

            nextLaunchTime = Time.time + cooldown;

            float strength = Mathf.Clamp01(Mathf.InverseLerp(minLaunchSpeed, referenceSpeed, approach));

            slide.LaunchFromRamp(redirectEfficiency, launchSpeed * strength, horizontalRetention);

            ObstacleImpactBurst.Spawn(body.position, sparkleColor, 0.3f + 0.5f * strength);

            if (launchShake > 0f)
                PlayerLocator.ShakeCamera(launchShake * strength);

            // Crystal chime rather than the whoosh the dash and the slap hand already use, so
            // a ramp is audibly its own event.
            if (playAudio && IceAudioManager.Instance != null)
                IceAudioManager.Instance.PlayCrystalChime();
        }
    }
}
