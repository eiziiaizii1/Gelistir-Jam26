using UnityEngine;
using UnityEngine.SceneManagement;

namespace IceEscape
{
    /// <summary>
    /// End-point volume for the run. Sits at the far end of the track as a huge transparent
    /// trigger box; the first time the player passes through it the run counts as successfully
    /// completed and the outro scene is loaded.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class FinishLineTrigger : MonoBehaviour
    {
        [Header("Outro")]
        [Tooltip("Scene loaded once the player reaches the end point. Must be in Build Settings.")]
        [SerializeField] private string outroSceneName = "Outro";

        [Header("Player Detection")]
        [Tooltip("Only colliders belonging to this tag complete the run.")]
        [SerializeField] private string playerTag = "Player";

        // Guards against the trigger firing again while the player is still inside the volume,
        // and against a second collider on the player (child colliders) loading the scene twice.
        private bool hasCompleted;

        private void Reset()
        {
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasCompleted)
                return;

            if (!IsPlayer(other))
                return;

            hasCompleted = true;
            CompleteRun();
        }

        private bool IsPlayer(Collider other)
        {
            // The player's colliders can sit on child objects, so resolve back to the body
            // that actually owns them before checking the tag.
            Rigidbody body = other.attachedRigidbody;
            Transform root = body != null ? body.transform : other.transform;

            if (root.CompareTag(playerTag))
                return true;

            // Fallback for setups where the tag was lost but the controller is still there.
            return root.GetComponentInParent<IceSlideController>() != null;
        }

        private void CompleteRun()
        {
            IceGameHUD hud = FindFirstObjectByType<IceGameHUD>();
            if (hud != null)
            {
                hud.TriggerVictory();
            }

            if (string.IsNullOrEmpty(outroSceneName))
            {
                Debug.LogWarning("[FinishLineTrigger] No outro scene name assigned.", this);
                return;
            }

            SceneManager.LoadScene(outroSceneName);
        }
    }
}
