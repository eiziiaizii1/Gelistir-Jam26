using UnityEngine;

namespace IceEscape
{
    public class LavaHazard : MonoBehaviour
    {
        [Header("Lava Settings")]
        [SerializeField] private float extraMeltRatePerSecond = 0.15f; // Extra 15% melt per second on contact

        private void OnTriggerStay(Collider other)
        {
            IMeltSource player = PlayerLocator.GetMeltSource(other.gameObject);
            if (player == null)
                return;

            player.RestoreIce(-extraMeltRatePerSecond * Time.deltaTime);

            IceGameHUD hud = PlayerLocator.FindHUD();
            if (hud != null)
            {
                hud.TriggerScreenFlash(new Color(1.0f, 0.2f, 0.1f), 0.15f);
            }

            PlayerLocator.ShakeCamera(0.08f);
        }
    }
}
