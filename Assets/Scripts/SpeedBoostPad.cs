using UnityEngine;

namespace IceEscape
{
    public class SpeedBoostPad : MonoBehaviour
    {
        [Header("Boost Settings")]
        [SerializeField] private float boostImpulse = 28f;
        [SerializeField] private Color boostColor = new Color(0.2f, 0.85f, 1.0f); // Electric Neon Blue
        [SerializeField] private float cooldown = 0.4f;
        [SerializeField] private bool playAudio = true;

        private float nextBoostTime;

        private void OnTriggerEnter(Collider other)
        {
            if (Time.time < nextBoostTime)
                return;

            Rigidbody rb = other.attachedRigidbody ?? other.GetComponentInParent<Rigidbody>();
            if (rb == null)
                return;

            IceSlideController slide = rb.GetComponent<IceSlideController>();
            if (slide == null)
                return;

            nextBoostTime = Time.time + cooldown;

            // Apply forward direction boost along current slope heading
            Vector3 boostDir = slide.SlideDirection;
            rb.AddForce(boostDir.normalized * boostImpulse, ForceMode.Impulse);

            // Visual feedback: Electric cyan particle burst at pad position
            ObstacleImpactBurst.Spawn(transform.position + Vector3.up * 0.5f, boostColor, 0.8f);

            // Camera shake & HUD flash
            PlayerLocator.ShakeCamera(0.2f);
            IceGameHUD hud = PlayerLocator.FindHUD();
            if (hud != null)
            {
                hud.TriggerScreenFlash(new Color(0.2f, 0.8f, 1f, 0.6f), 0.25f);
            }

            if (playAudio && IceAudioManager.Instance != null)
            {
                IceAudioManager.Instance.PlaySlapWhoosh();
            }
        }
    }
}
