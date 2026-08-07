using UnityEngine;

namespace IceEscape
{
    public class LavaHazard : MonoBehaviour
    {
        [Header("Lava Settings")]
        [SerializeField] private float extraMeltRatePerSecond = 0.15f; // Extra 15% melt per second on contact

        private void OnTriggerStay(Collider other)
        {
            IcePlayerController player = other.GetComponent<IcePlayerController>();
            if (player == null)
            {
                player = other.GetComponentInParent<IcePlayerController>();
            }

            if (player != null)
            {
                player.RestoreIce(-extraMeltRatePerSecond * Time.deltaTime);

                IceGameHUD hud = FindFirstObjectByType<IceGameHUD>();
                if (hud != null)
                {
                    hud.TriggerScreenFlash(new Color(1.0f, 0.2f, 0.1f), 0.15f);
                }

                CameraFollow camFollow = FindFirstObjectByType<CameraFollow>();
                if (camFollow != null)
                {
                    camFollow.TriggerShake(0.08f);
                }
            }
        }
    }
}
