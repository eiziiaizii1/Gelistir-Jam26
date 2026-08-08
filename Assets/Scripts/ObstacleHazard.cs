using UnityEngine;

namespace IceEscape
{
    public enum HazardMode
    {
        /// <summary>One hit per contact. Pillars, rocks, tentacles.</summary>
        Impact,

        /// <summary>Damage per second while overlapping. Lava pools and flows.</summary>
        Continuous
    }

    /// <summary>
    /// The single obstacle behaviour every hazard prefab shares. Tentacles, lava columns,
    /// static pillars and falling rocks differ only in their mesh, their animation and the
    /// numbers on this component, so keeping one script behind all of them means a fix or a
    /// retune lands everywhere at once — and prefab variants can override the numbers per
    /// obstacle type without touching code.
    ///
    /// A hit costs the block speed and size but never the run: descent is bled down to a
    /// floor and starts rebuilding immediately (see IceSlideController.SlowDescent), and
    /// size comes off through the existing melt system so the HUD and the lose condition
    /// stay in agreement.
    ///
    /// Strength scales with how fast the block arrived, so clipping a tentacle is a nudge
    /// and burying yourself in a pillar at full speed hurts.
    /// </summary>
    [DisallowMultipleComponent]
    public class ObstacleHazard : MonoBehaviour
    {
        [Header("Mode")]
        [SerializeField] private HazardMode mode = HazardMode.Impact;

        [Header("Impact strength")]
        [Tooltip("Contacts slower than this do nothing, so brushing past is free.")]
        [SerializeField] private float minImpactSpeed = 4f;
        [Tooltip("Contact speed that counts as a full-strength hit. At or above this the " +
                 "full descent and size loss below is applied.")]
        [SerializeField] private float referenceImpactSpeed = 28f;

        [Header("Cost of a hit")]
        [Tooltip("Portion of downhill speed removed by a full-strength hit, 0..1.")]
        [Range(0f, 1f)]
        [SerializeField] private float descentLoss = 0.35f;
        [Tooltip("Descent speed the block keeps no matter how hard it is hit, so it always " +
                 "slides clear of the obstacle and rebuilds speed instead of parking.")]
        [SerializeField] private float descentFloor = 6f;
        [Tooltip("Ice removed by a full-strength hit, 0..1 of the whole block. This is what " +
                 "shrinks the character, via the same melt system the HUD reads.")]
        [Range(0f, 1f)]
        [SerializeField] private float sizeLoss = 0.08f;

        [Header("Continuous mode")]
        [Tooltip("Continuous mode only: how often the hazard can bite, in seconds.")]
        [SerializeField] private float continuousTickInterval = 0.25f;

        [Header("Pushback")]
        [Tooltip("Shove away from the obstacle on impact. Keep modest: a big number here " +
                 "throws the block off the slope instead of reading as a glancing blow.")]
        [SerializeField] private float bounceForce = 5f;

        [Header("Destruction")]
        [Tooltip("Obstacle breaks when hit at or above destroyImpactSpeed.")]
        [SerializeField] private bool isDestructible = false;
        [SerializeField] private float destroyImpactSpeed = 22f;

        [Header("Feedback")]
        [SerializeField] private Color flashColor = new Color(1f, 0.25f, 0.05f);
        [SerializeField] private float flashStrength = 0.4f;
        [SerializeField] private float shakeAmount = 0.3f;
        [Tooltip("Colour of the burst spawned at the contact point.")]
        [SerializeField] private Color particleColor = new Color(1f, 0.35f, 0.05f);
        [SerializeField] private bool playAudio = true;

        [Header("Re-hit guard")]
        [Tooltip("Ignore further contacts for this long. Stops one obstacle landing several " +
                 "hits as the block scrapes along it.")]
        [SerializeField] private float hitCooldown = 0.4f;

        private float nextHitTime;

        private void OnCollisionEnter(Collision collision)
        {
            if (mode != HazardMode.Impact)
                return;

            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            TryHit(collision.gameObject, collision.relativeVelocity.magnitude, point);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (mode != HazardMode.Impact)
                return;

            TryHit(other.gameObject, SpeedOf(other.gameObject), other.ClosestPoint(transform.position));
        }

        private void OnTriggerStay(Collider other)
        {
            if (mode != HazardMode.Continuous)
                return;

            TryHit(other.gameObject, SpeedOf(other.gameObject), other.ClosestPoint(transform.position));
        }

        private static float SpeedOf(GameObject obj)
        {
            Rigidbody rb = obj.GetComponentInParent<Rigidbody>();
            return rb != null ? rb.linearVelocity.magnitude : 0f;
        }

        private void TryHit(GameObject target, float contactSpeed, Vector3 point)
        {
            if (Time.time < nextHitTime)
                return;

            IMeltSource melt = PlayerLocator.GetMeltSource(target);
            if (melt == null)
                return;

            // Continuous hazards bite on a fixed tick and always at full strength: standing
            // in lava should hurt whether you are crawling or flying through it.
            float strength;
            if (mode == HazardMode.Continuous)
            {
                nextHitTime = Time.time + continuousTickInterval;
                strength = continuousTickInterval;
            }
            else
            {
                if (contactSpeed < minImpactSpeed)
                    return;

                nextHitTime = Time.time + hitCooldown;
                strength = Mathf.InverseLerp(minImpactSpeed, referenceImpactSpeed, contactSpeed);
            }

            ApplyHit(melt, strength, contactSpeed, point);
        }

        /// <summary>
        /// Shoves the block sideways, across the slope, so it glances off and keeps going.
        ///
        /// Pushing straight back from the obstacle is wrong here: on a head-on hit that
        /// points up the hill and fights the whole point of the run. A hit dead centre has
        /// no sideways direction to derive either, so one is chosen deterministically —
        /// picking at random would make the same crash play differently each attempt.
        /// </summary>
        private void Deflect(Rigidbody body, IceSlideController slide, float strength)
        {
            Vector3 across = slide != null ? slide.SlopeRight : transform.right;

            float offset = Vector3.Dot(body.position - transform.position, across);
            float side;

            if (Mathf.Abs(offset) > 0.05f)
            {
                side = Mathf.Sign(offset);
            }
            else
            {
                // Dead centre. Break the tie toward whichever way the block is already
                // drifting, so the deflection agrees with the player's last input.
                float drift = Vector3.Dot(body.linearVelocity, across);
                side = Mathf.Abs(drift) > 0.01f ? Mathf.Sign(drift) : 1f;
            }

            Vector3 push = across * side + Vector3.up * 0.2f;
            body.AddForce(push.normalized * (bounceForce * Mathf.Max(strength, 0.5f)), ForceMode.Impulse);
        }

        private void ApplyHit(IMeltSource melt, float strength, float contactSpeed, Vector3 point)
        {
            MonoBehaviour meltBehaviour = melt as MonoBehaviour;
            Rigidbody body = meltBehaviour != null ? meltBehaviour.GetComponentInParent<Rigidbody>() : null;

            // Size first: this is the melt system, so the HUD bar and the lose condition
            // both pick it up for free.
            melt.RestoreIce(-sizeLoss * strength);

            // Then speed. Only the slide controller knows how to take descent away without
            // stalling the run, so ask it rather than touching the Rigidbody directly.
            if (body != null)
            {
                IceSlideController slide = body.GetComponent<IceSlideController>();
                if (slide != null)
                    slide.SlowDescent(descentLoss * strength, descentFloor);

                if (bounceForce > 0f && mode == HazardMode.Impact)
                    Deflect(body, slide, strength);

                IceSquashAndStretch squash = body.GetComponentInChildren<IceSquashAndStretch>();
                if (squash != null)
                    squash.ApplySquash(new Vector3(0.3f, -0.4f, 0.3f) * strength);
            }

            IceGameHUD hud = PlayerLocator.FindHUD();
            if (hud != null)
                hud.TriggerScreenFlash(flashColor, flashStrength * Mathf.Max(strength, 0.35f));

            PlayerLocator.ShakeCamera(shakeAmount * strength);

            if (playAudio && IceAudioManager.Instance != null)
                IceAudioManager.Instance.PlayExplosion();

            if (mode == HazardMode.Impact)
                ObstacleImpactBurst.Spawn(point, particleColor, strength);

            if (isDestructible && contactSpeed >= destroyImpactSpeed)
                Destroy(gameObject);
        }
    }
}
