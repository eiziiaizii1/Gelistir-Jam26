using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IceEscape
{
    /// <summary>
    /// Drives the outro screen shown after the player escapes: hooks the return-to-menu button
    /// up to the menu scene load.
    /// </summary>
    public class OutroScreen : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button returnToMenuButton;

        [Header("Navigation")]
        [Tooltip("Scene loaded by the return button. Must be in Build Settings.")]
        [SerializeField] private string menuSceneName = "Menu";

        private void Awake()
        {
            if (returnToMenuButton == null)
            {
                GameObject found = GameObject.Find("ReturnToMenuButton");
                if (found != null) returnToMenuButton = found.GetComponent<Button>();
            }

            if (returnToMenuButton != null)
            {
                returnToMenuButton.onClick.AddListener(OnReturnToMenuClicked);
            }
            else
            {
                Debug.LogWarning("[OutroScreen] No return-to-menu button assigned.", this);
            }

            // The gameplay scene hides and locks the cursor; the outro needs it back.
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void OnDestroy()
        {
            if (returnToMenuButton != null)
            {
                returnToMenuButton.onClick.RemoveListener(OnReturnToMenuClicked);
            }
        }

        public void OnReturnToMenuClicked()
        {
            if (string.IsNullOrEmpty(menuSceneName))
            {
                Debug.LogWarning("[OutroScreen] No menu scene name assigned.", this);
                return;
            }

            SceneManager.LoadScene(menuSceneName);
        }
    }
}
