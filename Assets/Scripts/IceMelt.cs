using UnityEngine;

namespace IceEscape
{
    /// <summary>
    /// Anything that can report how much ice is left, so the HUD does not care which
    /// movement controller is driving the character.
    /// </summary>
    public interface IMeltSource
    {
        float CurrentMeltPercent { get; }

        /// <summary>Add ice back, or pass a negative amount to melt some off.</summary>
        void RestoreIce(float amount);
    }

    /// <summary>
    /// Standalone melt: the block shrinks and lightens as it melts away.
    ///
    /// Split out of the old all-in-one player controller so melting can run on a character
    /// using any movement controller. Behaviour and defaults match the original.
    /// </summary>
    public class IceMelt : MonoBehaviour, IMeltSource
    {
        [Header("Targets")]
        [Tooltip("Transform that gets scaled down. Defaults to the first child.")]
        [SerializeField] private Transform visualTransform;
        [Tooltip("Body that gets lighter as the ice melts. Defaults to this object's.")]
        [SerializeField] private Rigidbody body;

        [Header("Melting")]
        [SerializeField] private bool enableMelting = true;
        [Tooltip("Fraction of the block lost per second. 0.015 melts out in about 65s.")]
        [SerializeField] private float meltRatePerSecond = 0.015f;
        [Tooltip("Smallest the visual shrinks to, as a fraction of its starting scale.")]
        [SerializeField] private float minMeltScaleRatio = 0.35f;

        [Header("Mass")]
        [Tooltip("Mass when fully melted. Lighter ice reacts harder to slaps.")]
        [SerializeField] private float minMass = 0.3f;
        [Tooltip("Mass when whole.")]
        [SerializeField] private float maxMass = 1f;

        private float currentMeltPercent = 1f;
        private Vector3 initialVisualScale = Vector3.one;

        public float CurrentMeltPercent => currentMeltPercent;

        private void Awake()
        {
            if (body == null)
                body = GetComponent<Rigidbody>();

            if (visualTransform == null && transform.childCount > 0)
                visualTransform = transform.GetChild(0);

            if (visualTransform != null)
                initialVisualScale = visualTransform.localScale;
        }

        private void Update()
        {
            if (!enableMelting || currentMeltPercent <= 0f)
                return;

            currentMeltPercent = Mathf.Clamp01(currentMeltPercent - meltRatePerSecond * Time.deltaTime);
            ApplyMeltVisuals();
        }

        private void ApplyMeltVisuals()
        {
            if (visualTransform != null)
            {
                float scaleFactor = Mathf.Lerp(minMeltScaleRatio, 1f, currentMeltPercent);
                visualTransform.localScale = initialVisualScale * scaleFactor;
            }

            if (body != null)
                body.mass = Mathf.Lerp(minMass, maxMass, currentMeltPercent);
        }

        /// <summary>Top the ice back up. Used by IcePickup.</summary>
        public void RestoreIce(float amount)
        {
            currentMeltPercent = Mathf.Clamp01(currentMeltPercent + amount);
            ApplyMeltVisuals();
        }
    }
}
