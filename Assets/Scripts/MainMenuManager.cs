using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

namespace IceEscape
{
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Scene Loading")]
        [SerializeField] private string gameplaySceneName = "intro anim";

        [Header("UI Component References (Pre-baked in Scene)")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("Settings Modal References")]
        [SerializeField] private GameObject settingsModalPanel;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Text volumeText;
        [SerializeField] private Button muteButton;
        [SerializeField] private Text muteText;
        [SerializeField] private Button qualityButton;
        [SerializeField] private Text qualityText;
        [SerializeField] private Button closeSettingsButton;

        [Header("Overlay")]
        [SerializeField] private Image fadeOverlay;

        private int currentQualityLevel;

        private void Awake()
        {
            SetupMenuCamera();
            BindSceneReferences();
            InitSettingsState();
        }

        private void SetupMenuCamera()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                mainCam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
            }

            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color(0.06f, 0.02f, 0.02f, 1.0f);
        }

        private void BindSceneReferences()
        {
            // Auto-bind scene UI components if not manually set in inspector
            if (startButton == null) startButton = GameObject.Find("StartButton")?.GetComponent<Button>();
            if (settingsButton == null) settingsButton = GameObject.Find("SettingsButton")?.GetComponent<Button>();
            if (quitButton == null) quitButton = GameObject.Find("QuitButton")?.GetComponent<Button>();

            if (settingsModalPanel == null) settingsModalPanel = GameObject.Find("SettingsModalPanel");
            if (volumeSlider == null) volumeSlider = GameObject.Find("VolumeSlider")?.GetComponent<Slider>();
            if (volumeText == null) volumeText = GameObject.Find("VolumeText")?.GetComponent<Text>();

            if (muteButton == null) muteButton = GameObject.Find("MuteButton")?.GetComponent<Button>();
            if (muteText == null && muteButton != null) muteText = muteButton.GetComponentInChildren<Text>();

            if (qualityButton == null) qualityButton = GameObject.Find("QualityButton")?.GetComponent<Button>();
            if (qualityText == null && qualityButton != null) qualityText = qualityButton.GetComponentInChildren<Text>();

            if (closeSettingsButton == null) closeSettingsButton = GameObject.Find("CloseButton")?.GetComponent<Button>();
            if (fadeOverlay == null) fadeOverlay = GameObject.Find("FadeOverlay")?.GetComponent<Image>();

            // Hook up Button listeners
            if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);

            if (volumeSlider != null)
            {
                volumeSlider.value = IceAudioManager.MasterVolume;
                volumeSlider.onValueChanged.AddListener(OnVolumeSliderChanged);
            }

            if (muteButton != null) muteButton.onClick.AddListener(OnMuteToggleClicked);
            if (qualityButton != null) qualityButton.onClick.AddListener(OnQualityCycleClicked);
            if (closeSettingsButton != null) closeSettingsButton.onClick.AddListener(OnCloseSettingsClicked);
        }

        private void InitSettingsState()
        {
            currentQualityLevel = QualitySettings.GetQualityLevel();

            UpdateVolumeText(IceAudioManager.MasterVolume);
            if (muteText != null) muteText.text = GetMuteText();
            if (qualityText != null) qualityText.text = GetQualityText();

            if (settingsModalPanel != null)
            {
                settingsModalPanel.SetActive(false);
            }
        }

        private void OnVolumeSliderChanged(float val)
        {
            IceAudioManager.MasterVolume = val;
            UpdateVolumeText(val);
        }

        private void UpdateVolumeText(float val)
        {
            if (volumeText != null)
            {
                volumeText.text = $"AUDIO VOLUME: {Mathf.RoundToInt(val * 100f)}%";
            }
        }

        private string GetMuteText()
        {
            return IceAudioManager.IsMuted ? "AUDIO: MUTED (OFF)" : "AUDIO: ACTIVE (ON)";
        }

        private string GetQualityText()
        {
            string[] names = QualitySettings.names;
            int idx = QualitySettings.GetQualityLevel();
            string qName = (idx >= 0 && idx < names.Length) ? names[idx].ToUpper() : "MEDIUM";
            return $"GRAPHICS: {qName}";
        }

        private void OnMuteToggleClicked()
        {
            IceAudioManager.IsMuted = !IceAudioManager.IsMuted;
            if (muteText != null) muteText.text = GetMuteText();
        }

        private void OnQualityCycleClicked()
        {
            string[] names = QualitySettings.names;
            currentQualityLevel = (currentQualityLevel + 1) % names.Length;
            QualitySettings.SetQualityLevel(currentQualityLevel, true);
            if (qualityText != null) qualityText.text = GetQualityText();
        }

        public void OnStartClicked()
        {
            StartCoroutine(DoStartTransition());
        }

        private IEnumerator DoStartTransition()
        {
            if (fadeOverlay != null)
            {
                fadeOverlay.raycastTarget = true;
                float elapsed = 0f;
                float duration = 0.8f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    fadeOverlay.color = new Color(0.08f, 0.02f, 0.02f, elapsed / duration);
                    yield return null;
                }
            }

            SceneManager.LoadScene(gameplaySceneName);
        }

        public void OnSettingsClicked()
        {
            if (settingsModalPanel != null)
            {
                settingsModalPanel.SetActive(true);
            }
        }

        public void OnCloseSettingsClicked()
        {
            if (settingsModalPanel != null)
            {
                settingsModalPanel.SetActive(false);
            }
        }

        public void OnQuitClicked()
        {
            Debug.Log("Quitting application...");
            Application.Quit();
        }
    }
}
