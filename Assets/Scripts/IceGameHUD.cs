using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace IceEscape
{
    public class IceGameHUD : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Font for the whole HUD. Assign it here rather than leaving it to the editor-" +
                 "only asset lookup below, which is compiled out of builds — an unassigned font " +
                 "means the shipped game quietly falls back to Unity's legacy font.")]
        [SerializeField] private Font uiFont;

        [Tooltip("Overall HUD size multiplier. 1 = authored size, 1.25 = a quarter larger. " +
                 "Applied through the CanvasScaler's reference resolution, so panels, bars and " +
                 "text all grow together.")]
        [SerializeField] private float uiScale = 1.25f;

        private Canvas hudCanvas;
        private Text speedText;
        private Text distanceText;
        private Text meltText;
        private Image meltBarFill;
        private Material meltBarMaterial;
        private Image screenFlashOverlay;

        private GameObject gameOverPanel;
        private Text gameOverText;

        private GameObject victoryPanel;
        private Text victoryText;

        [Header("Target Tracking")]
        [Tooltip("Anything implementing IMeltSource, e.g. IceMelt. Auto-found if left empty.")]
        [SerializeField] private MonoBehaviour meltSourceBehaviour;
        [SerializeField] private Rigidbody playerRigidbody;

        private IMeltSource meltSource;
        private Vector3 startPosition;
        private bool hasStartPosition = false;
        private float nextSearchTime = 0f;
        private float flashAlpha = 0f;
        private Color targetFlashColor = Color.clear;
        private bool isGameOver = false;
        private bool isVictory = false;

        private void Awake()
        {
            CreateHUDIfNeeded();
        }

        private void Start()
        {
            FindPlayerReferences();
        }

        /// <summary>
        /// Binds to whatever is driving the character this scene. The HUD only needs an
        /// IMeltSource plus a Rigidbody, so it does not care which movement controller
        /// is in play.
        /// </summary>
        private void FindPlayerReferences()
        {
            if (meltSource == null)
            {
                meltSource = meltSourceBehaviour as IMeltSource;
            }

            if (meltSource == null)
            {
                meltSource = PlayerLocator.FindMeltSource();
                meltSourceBehaviour = meltSource as MonoBehaviour;
            }

            if (playerRigidbody == null)
            {
                playerRigidbody = PlayerLocator.FindPlayerBody();
            }

            if (playerRigidbody != null && !hasStartPosition)
            {
                startPosition = playerRigidbody.position;
                hasStartPosition = true;
            }
        }

        private void Update()
        {
            // Retry on an interval instead of every frame: the scene sweep is not cheap.
            if ((meltSource == null || playerRigidbody == null) && Time.time >= nextSearchTime)
            {
                nextSearchTime = Time.time + 0.5f;
                FindPlayerReferences();
            }

            // Update Speedometer & Distance Counter
            if (playerRigidbody != null)
            {
                float speed = playerRigidbody.linearVelocity.magnitude;
                float kmh = speed * 3.6f; // Arcade KM/H display
                if (speedText != null) 
                {
                    speedText.text = $"⚡ <size=22><b>{kmh:F0}</b></size> <size=12>KM/H</size>";
                }

                float distance = Vector3.Distance(startPosition, playerRigidbody.position);
                if (distanceText != null) distanceText.text = $"{distance:F0} M";
            }

            // Update Melt Meter Bar & Text
            if (meltSource != null && meltBarFill != null && meltText != null)
            {
                float meltRatio = meltSource.CurrentMeltPercent;
                meltBarFill.fillAmount = Mathf.Lerp(meltBarFill.fillAmount, meltRatio, Time.deltaTime * 8f);

                if (meltBarMaterial != null)
                {
                    meltBarMaterial.SetFloat("_FillAmount", meltBarFill.fillAmount);
                }

                int percentInt = Mathf.RoundToInt(meltRatio * 100f);

                if (meltRatio < 0.25f)
                {
                    float warnPulse = Mathf.PingPong(Time.time * 8f, 1f);
                    meltText.text = $"⚠️ ICE CRITICAL: {percentInt}%";
                    meltText.color = Color.Lerp(new Color(1f, 0.2f, 0.2f), Color.white, warnPulse);
                }
                else
                {
                    meltText.text = $"ICE HEALTH: {percentInt}%";
                    meltText.color = new Color(0.85f, 0.96f, 1.0f);
                }

                Color barColor;
                if (meltRatio > 0.5f)
                {
                    barColor = Color.Lerp(new Color(0.95f, 0.7f, 0.1f), new Color(0.15f, 0.95f, 1.0f), (meltRatio - 0.5f) * 2f);
                }
                else
                {
                    barColor = Color.Lerp(new Color(1.0f, 0.15f, 0.1f), new Color(0.95f, 0.7f, 0.1f), meltRatio * 2f);
                }

                meltBarFill.color = barColor;

                if (meltRatio <= 0.01f && !isGameOver && !isVictory)
                {
                    TriggerGameOver();
                }
            }

            // Handle Screen Flash Decay
            if (screenFlashOverlay != null)
            {
                if (flashAlpha > 0.01f)
                {
                    if (!screenFlashOverlay.gameObject.activeSelf) screenFlashOverlay.gameObject.SetActive(true);
                    flashAlpha = Mathf.Lerp(flashAlpha, 0f, Time.deltaTime * 4f);
                    screenFlashOverlay.color = new Color(targetFlashColor.r, targetFlashColor.g, targetFlashColor.b, flashAlpha);
                }
                else
                {
                    if (screenFlashOverlay.gameObject.activeSelf) screenFlashOverlay.gameObject.SetActive(false);
                }
            }
        }

        public void TriggerScreenFlash(Color color, float maxAlpha = 0.35f)
        {
            targetFlashColor = color;
            flashAlpha = maxAlpha;
            if (screenFlashOverlay != null)
            {
                screenFlashOverlay.gameObject.SetActive(true);
                screenFlashOverlay.color = new Color(color.r, color.g, color.b, maxAlpha);
            }
        }

        public void TriggerVictory()
        {
            if (isGameOver || isVictory) return;

            isVictory = true;
            DisablePlayerControls();
            TriggerScreenFlash(new Color(0.1f, 0.9f, 1.0f), 0.5f);

            if (victoryPanel != null)
            {
                victoryPanel.SetActive(true);
            }
        }

        private void TriggerGameOver()
        {
            if (isGameOver) return;
            isGameOver = true;

            // The restart panel is up from here on, so the run must stop responding to the
            // mouse - otherwise the player keeps steering a dead cube behind the panel.
            DisablePlayerControls();

            StartCoroutine(DoSmoothGameOverTransition());
        }

        /// <summary>
        /// Hands control back to the UI by muting player input. The cube keeps its momentum
        /// and coasts to a stop under physics rather than freezing in place.
        /// </summary>
        private void DisablePlayerControls()
        {
            IceSlideController controller = FindFirstObjectByType<IceSlideController>();
            if (controller != null)
            {
                controller.SetControlsEnabled(false);
            }
        }

        private System.Collections.IEnumerator DoSmoothGameOverTransition()
        {
            // 1. Subtle dark red flash
            TriggerScreenFlash(new Color(0.2f, 0.02f, 0.02f), 0.6f);

            // 2. Prepare Game Over Panel CanvasGroup for smooth fade-in
            CanvasGroup cg = null;
            if (gameOverPanel != null)
            {
                cg = gameOverPanel.GetComponent<CanvasGroup>();
                if (cg == null) cg = gameOverPanel.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                gameOverPanel.SetActive(true);
            }

            // 3. Find Volume for Post-Processing lerp
            Volume volume = FindFirstObjectByType<Volume>();
            ColorAdjustments colorAdjustments = null;
            Vignette vignette = null;

            float startSat = 15f;
            float startVig = 0.42f;

            if (volume != null && volume.profile != null)
            {
                if (volume.profile.TryGet<ColorAdjustments>(out colorAdjustments))
                {
                    colorAdjustments.saturation.overrideState = true;
                    startSat = colorAdjustments.saturation.value;
                }

                if (volume.profile.TryGet<Vignette>(out vignette))
                {
                    vignette.intensity.overrideState = true;
                    startVig = vignette.intensity.value;
                }
            }

            // 4. Smooth 1.6 second Lerp for desaturation, vignette, and UI panel fade-in!
            float duration = 1.6f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                if (colorAdjustments != null)
                {
                    colorAdjustments.saturation.value = Mathf.Lerp(startSat, -100f, smoothT);
                }

                if (vignette != null)
                {
                    vignette.intensity.value = Mathf.Lerp(startVig, 0.78f, smoothT);
                }

                if (cg != null)
                {
                    cg.alpha = Mathf.Lerp(0f, 1f, smoothT);
                }

                yield return null;
            }

            if (colorAdjustments != null) colorAdjustments.saturation.value = -100f;
            if (vignette != null) vignette.intensity.value = 0.78f;
            if (cg != null) cg.alpha = 1f;
        }

        public void RestartGame()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.name);
        }

        private void CreateHUDIfNeeded()
        {
            hudCanvas = GetComponentInParent<Canvas>();
            if (hudCanvas != null) return;

            GameObject canvasObj = new GameObject("IceHUDCanvas");
            hudCanvas = canvasObj.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Panel sizes below are authored in 1080p pixels, so scale with the screen
            // instead of leaving the scaler on Constant Pixel Size.
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // Shrinking the reference resolution enlarges everything on the canvas by the same
            // factor. Done here rather than by editing each panel size and font size, so the
            // whole HUD keeps its proportions and stays tunable from one number.
            scaler.referenceResolution = new Vector2(1920f / uiScale, 1080f / uiScale);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // Serialized reference first: AssetDatabase is compiled out of a build, so relying
            // on it alone silently drops the custom font in the shipped game and falls through
            // to LegacyRuntime. The editor lookup stays as a convenience when uiFont is unset.
            Font defaultFont = uiFont;
#if UNITY_EDITOR
            if (defaultFont == null)
                defaultFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/ManufacturingConsent-Regular.ttf");
#endif
            if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null) defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 16);

            // Melt Meter Panel (Top Left - Modern Cyan Glass Card)
            GameObject meltPanelObj = new GameObject("MeltMeterPanel");
            meltPanelObj.transform.SetParent(canvasObj.transform, false);

            RectTransform meltRect = meltPanelObj.AddComponent<RectTransform>();
            meltRect.anchorMin = new Vector2(0f, 1f);
            meltRect.anchorMax = new Vector2(0f, 1f);
            meltRect.pivot = new Vector2(0f, 1f);
            meltRect.anchoredPosition = new Vector2(25f, -25f);
            meltRect.sizeDelta = new Vector2(260f, 58f);

            Image meltPanelBg = meltPanelObj.AddComponent<Image>();
            meltPanelBg.color = new Color(0.03f, 0.08f, 0.16f, 0.85f); // Deep Cyan Glass

            GameObject meltTextObj = new GameObject("MeltText");
            meltTextObj.transform.SetParent(meltPanelObj.transform, false);

            RectTransform mtRect = meltTextObj.AddComponent<RectTransform>();
            mtRect.anchorMin = new Vector2(0f, 0.45f);
            mtRect.anchorMax = new Vector2(1f, 1f);
            mtRect.sizeDelta = Vector2.zero;

            meltText = meltTextObj.AddComponent<Text>();
            meltText.font = defaultFont;
            meltText.text = "❄️ BUZ SAĞLIĞI: %100";
            meltText.alignment = TextAnchor.MiddleCenter;
            meltText.color = new Color(0.85f, 0.95f, 1.0f);
            meltText.fontSize = 14;
            meltText.fontStyle = FontStyle.Bold;
            meltText.supportRichText = true;

            GameObject barBgObj = new GameObject("BarBackground");
            barBgObj.transform.SetParent(meltPanelObj.transform, false);

            RectTransform barBgRect = barBgObj.AddComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0.05f, 0.12f);
            barBgRect.anchorMax = new Vector2(0.95f, 0.42f);
            barBgRect.sizeDelta = Vector2.zero;
            barBgRect.anchoredPosition = Vector2.zero;

            Image barBgImage = barBgObj.AddComponent<Image>();
            barBgImage.color = new Color(0.08f, 0.12f, 0.22f, 0.9f);

            GameObject barFillObj = new GameObject("BarFill");
            barFillObj.transform.SetParent(barBgObj.transform, false);

            RectTransform fillRect = barFillObj.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
            fillRect.anchoredPosition = Vector2.zero;

            meltBarFill = barFillObj.AddComponent<Image>();
            meltBarFill.type = Image.Type.Filled;
            meltBarFill.fillMethod = Image.FillMethod.Horizontal;
            meltBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            meltBarFill.color = Color.white;

            Shader iceShader = Shader.Find("UI/IceHealthBar");
            if (iceShader != null)
            {
                meltBarMaterial = new Material(iceShader);
                meltBarFill.material = meltBarMaterial;
            }

            // Speedometer Panel (Top Right - Arcade Neon Card)
            GameObject speedObj = new GameObject("SpeedPanel");
            speedObj.transform.SetParent(canvasObj.transform, false);

            RectTransform speedRect = speedObj.AddComponent<RectTransform>();
            speedRect.anchorMin = new Vector2(1f, 1f);
            speedRect.anchorMax = new Vector2(1f, 1f);
            speedRect.pivot = new Vector2(1f, 1f);
            speedRect.anchoredPosition = new Vector2(-25f, -25f);
            speedRect.sizeDelta = new Vector2(160f, 52f);

            Image speedBg = speedObj.AddComponent<Image>();
            speedBg.color = new Color(0.03f, 0.08f, 0.16f, 0.85f); // Deep Cyan Glass

            GameObject speedTextObj = new GameObject("SpeedText");
            speedTextObj.transform.SetParent(speedObj.transform, false);

            RectTransform stRect = speedTextObj.AddComponent<RectTransform>();
            stRect.anchorMin = Vector2.zero;
            stRect.anchorMax = Vector2.one;
            stRect.sizeDelta = Vector2.zero;
            stRect.anchoredPosition = Vector2.zero;

            speedText = speedTextObj.AddComponent<Text>();
            speedText.font = defaultFont;
            speedText.text = "⚡ <size=22><b>0</b></size> <size=12>KM/H</size>";
            speedText.alignment = TextAnchor.MiddleCenter;
            speedText.color = new Color(0.2f, 0.95f, 1.0f);
            speedText.fontSize = 14;
            speedText.fontStyle = FontStyle.Bold;
            speedText.supportRichText = true;

            // Screen Flash Overlay (Disabled by default).
            // Created after the readouts so the flash tints them, but before the end-game
            // panels so it never washes over the win/lose screens.
            GameObject flashObj = new GameObject("ScreenFlashOverlay");
            flashObj.transform.SetParent(canvasObj.transform, false);

            RectTransform flashRect = flashObj.AddComponent<RectTransform>();
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.sizeDelta = Vector2.zero;

            screenFlashOverlay = flashObj.AddComponent<Image>();
            screenFlashOverlay.color = Color.clear;
            screenFlashOverlay.raycastTarget = false;
            flashObj.SetActive(false);

            // Victory Panel (Disabled by default)
            victoryPanel = new GameObject("VictoryPanel");
            victoryPanel.transform.SetParent(canvasObj.transform, false);

            RectTransform vRect = victoryPanel.AddComponent<RectTransform>();
            vRect.anchorMin = Vector2.zero;
            vRect.anchorMax = Vector2.one;
            vRect.sizeDelta = Vector2.zero;
            vRect.anchoredPosition = Vector2.zero;

            Image vBg = victoryPanel.AddComponent<Image>();
            vBg.color = new Color(0.02f, 0.1f, 0.15f, 0.92f);

            GameObject vTextObj = new GameObject("VictoryText");
            vTextObj.transform.SetParent(victoryPanel.transform, false);

            RectTransform vtRect = vTextObj.AddComponent<RectTransform>();
            vtRect.anchorMin = new Vector2(0.5f, 0.6f);
            vtRect.anchorMax = new Vector2(0.5f, 0.6f);
            vtRect.sizeDelta = new Vector2(550f, 120f);

            victoryText = vTextObj.AddComponent<Text>();
            victoryText.font = defaultFont;
            victoryText.text = "VICTORY! ESCAPED HELL!\n<size=18>Reached the Portal Before Melting!</size>";
            victoryText.alignment = TextAnchor.MiddleCenter;
            victoryText.color = new Color(0.2f, 0.95f, 1.0f);
            victoryText.fontSize = 28;
            victoryText.fontStyle = FontStyle.Bold;
            victoryText.supportRichText = true;

            GameObject vBtnObj = new GameObject("VictoryRestartButton");
            vBtnObj.transform.SetParent(victoryPanel.transform, false);

            RectTransform vbRect = vBtnObj.AddComponent<RectTransform>();
            vbRect.anchorMin = new Vector2(0.5f, 0.35f);
            vbRect.anchorMax = new Vector2(0.5f, 0.35f);
            vbRect.sizeDelta = new Vector2(220f, 55f);

            Image vbImage = vBtnObj.AddComponent<Image>();
            vbImage.color = new Color(0.1f, 0.8f, 0.95f);

            Button vBtn = vBtnObj.AddComponent<Button>();
            vBtn.targetGraphic = vbImage;
            vBtn.onClick.AddListener(RestartGame);

            GameObject vbTextObj = new GameObject("VBtnText");
            vbTextObj.transform.SetParent(vBtnObj.transform, false);

            RectTransform vbtRect = vbTextObj.AddComponent<RectTransform>();
            vbtRect.anchorMin = Vector2.zero;
            vbtRect.anchorMax = Vector2.one;
            vbtRect.sizeDelta = Vector2.zero;
            vbtRect.anchoredPosition = Vector2.zero;

            Text vBtnText = vbTextObj.AddComponent<Text>();
            vBtnText.font = defaultFont;
            vBtnText.text = "PLAY AGAIN";
            vBtnText.alignment = TextAnchor.MiddleCenter;
            vBtnText.color = Color.white;
            vBtnText.fontSize = 18;
            vBtnText.fontStyle = FontStyle.Bold;

            victoryPanel.SetActive(false);

            // Game Over Panel (Disabled by default)
            gameOverPanel = new GameObject("GameOverPanel");
            gameOverPanel.transform.SetParent(canvasObj.transform, false);

            RectTransform goRect = gameOverPanel.AddComponent<RectTransform>();
            goRect.anchorMin = Vector2.zero;
            goRect.anchorMax = Vector2.one;
            goRect.sizeDelta = Vector2.zero;
            goRect.anchoredPosition = Vector2.zero;

            Image goBg = gameOverPanel.AddComponent<Image>();
            goBg.color = new Color(0.1f, 0.02f, 0.02f, 0.92f);

            GameObject goTextObj = new GameObject("GameOverText");
            goTextObj.transform.SetParent(gameOverPanel.transform, false);

            RectTransform gotRect = goTextObj.AddComponent<RectTransform>();
            gotRect.anchorMin = new Vector2(0.5f, 0.6f);
            gotRect.anchorMax = new Vector2(0.5f, 0.6f);
            gotRect.sizeDelta = new Vector2(500f, 100f);

            gameOverText = goTextObj.AddComponent<Text>();
            gameOverText.font = defaultFont;
            gameOverText.text = "MELTED IN HELL!\n<size=18>Your Ice Cube Melted in the Inferno</size>";
            gameOverText.alignment = TextAnchor.MiddleCenter;
            gameOverText.color = new Color(1.0f, 0.3f, 0.2f);
            gameOverText.fontSize = 28;
            gameOverText.fontStyle = FontStyle.Bold;
            gameOverText.supportRichText = true;

            GameObject btnObj = new GameObject("RestartButton");
            btnObj.transform.SetParent(gameOverPanel.transform, false);

            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.35f);
            btnRect.anchorMax = new Vector2(0.5f, 0.35f);
            btnRect.sizeDelta = new Vector2(220f, 55f);

            Image btnImage = btnObj.AddComponent<Image>();
            btnImage.color = new Color(0.1f, 0.7f, 0.9f);

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImage;
            btn.onClick.AddListener(RestartGame);

            GameObject btnTextObj = new GameObject("BtnText");
            btnTextObj.transform.SetParent(btnObj.transform, false);

            RectTransform btRect = btnTextObj.AddComponent<RectTransform>();
            btRect.anchorMin = Vector2.zero;
            btRect.anchorMax = Vector2.one;
            btRect.sizeDelta = Vector2.zero;
            btRect.anchoredPosition = Vector2.zero;

            Text btnText = btnTextObj.AddComponent<Text>();
            btnText.font = defaultFont;
            btnText.text = "TEKRAR DENE";
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.color = Color.white;
            btnText.fontSize = 18;
            btnText.fontStyle = FontStyle.Bold;

            gameOverPanel.SetActive(false);
        }
    }
}
