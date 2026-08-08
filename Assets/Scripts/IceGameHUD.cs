using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace IceEscape
{
    public class IceGameHUD : MonoBehaviour
    {
        [Header("UI References")]
        private Canvas hudCanvas;
        private Text speedText;
        private Text distanceText;
        private Text meltText;
        private Image meltBarFill;
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
                if (speedText != null) speedText.text = $"HIZ: {speed:F1} m/s";

                float distance = Vector3.Distance(startPosition, playerRigidbody.position);
                if (distanceText != null) distanceText.text = $"MESAFE: {distance:F0} m";
            }

            // Update Melt Meter Bar & Text
            if (meltSource != null && meltBarFill != null && meltText != null)
            {
                float meltRatio = meltSource.CurrentMeltPercent;
                meltBarFill.fillAmount = Mathf.Lerp(meltBarFill.fillAmount, meltRatio, Time.deltaTime * 8f);

                int percentInt = Mathf.RoundToInt(meltRatio * 100f);
                meltText.text = $"BUZ HACMİ: %{percentInt}";

                Color barColor;
                if (meltRatio > 0.5f)
                {
                    barColor = Color.Lerp(new Color(0.9f, 0.6f, 0.1f), new Color(0.1f, 0.9f, 1.0f), (meltRatio - 0.5f) * 2f);
                }
                else
                {
                    barColor = Color.Lerp(new Color(1.0f, 0.2f, 0.1f), new Color(0.9f, 0.6f, 0.1f), meltRatio * 2f);
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
            TriggerScreenFlash(new Color(0.1f, 0.9f, 1.0f), 0.5f);

            if (victoryPanel != null)
            {
                victoryPanel.SetActive(true);
            }
        }

        private void TriggerGameOver()
        {
            isGameOver = true;
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
            }
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
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null) defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 16);

            // Melt Meter Panel (Top Left - Minimal Translucent)
            GameObject meltPanelObj = new GameObject("MeltMeterPanel");
            meltPanelObj.transform.SetParent(canvasObj.transform, false);

            RectTransform meltRect = meltPanelObj.AddComponent<RectTransform>();
            meltRect.anchorMin = new Vector2(0f, 1f);
            meltRect.anchorMax = new Vector2(0f, 1f);
            meltRect.pivot = new Vector2(0f, 1f);
            meltRect.anchoredPosition = new Vector2(20f, -20f);
            meltRect.sizeDelta = new Vector2(220f, 50f);

            Image meltPanelBg = meltPanelObj.AddComponent<Image>();
            meltPanelBg.color = new Color(0.02f, 0.05f, 0.1f, 0.65f);

            GameObject meltTextObj = new GameObject("MeltText");
            meltTextObj.transform.SetParent(meltPanelObj.transform, false);

            RectTransform mtRect = meltTextObj.AddComponent<RectTransform>();
            mtRect.anchorMin = new Vector2(0f, 0.5f);
            mtRect.anchorMax = new Vector2(1f, 1f);
            mtRect.sizeDelta = Vector2.zero;

            meltText = meltTextObj.AddComponent<Text>();
            meltText.font = defaultFont;
            meltText.text = "BUZ HACMİ: %100";
            meltText.alignment = TextAnchor.MiddleCenter;
            meltText.color = new Color(0.85f, 0.95f, 1.0f);
            meltText.fontSize = 13;
            meltText.fontStyle = FontStyle.Bold;

            GameObject barBgObj = new GameObject("BarBackground");
            barBgObj.transform.SetParent(meltPanelObj.transform, false);

            RectTransform barBgRect = barBgObj.AddComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0.05f, 0.12f);
            barBgRect.anchorMax = new Vector2(0.95f, 0.45f);
            barBgRect.sizeDelta = Vector2.zero;
            barBgRect.anchoredPosition = Vector2.zero;

            Image barBgImage = barBgObj.AddComponent<Image>();
            barBgImage.color = new Color(0.1f, 0.1f, 0.18f, 0.8f);

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
            meltBarFill.color = new Color(0.1f, 0.9f, 1.0f);

            // Speedometer Panel (Top Right - Minimal)
            GameObject speedObj = new GameObject("SpeedPanel");
            speedObj.transform.SetParent(canvasObj.transform, false);

            RectTransform speedRect = speedObj.AddComponent<RectTransform>();
            speedRect.anchorMin = new Vector2(1f, 1f);
            speedRect.anchorMax = new Vector2(1f, 1f);
            speedRect.pivot = new Vector2(1f, 1f);
            speedRect.anchoredPosition = new Vector2(-20f, -20f);
            speedRect.sizeDelta = new Vector2(140f, 40f);

            Image speedBg = speedObj.AddComponent<Image>();
            speedBg.color = new Color(0.02f, 0.05f, 0.1f, 0.65f);

            GameObject speedTextObj = new GameObject("SpeedText");
            speedTextObj.transform.SetParent(speedObj.transform, false);

            RectTransform stRect = speedTextObj.AddComponent<RectTransform>();
            stRect.anchorMin = Vector2.zero;
            stRect.anchorMax = Vector2.one;
            stRect.sizeDelta = Vector2.zero;
            stRect.anchoredPosition = Vector2.zero;

            speedText = speedTextObj.AddComponent<Text>();
            speedText.font = defaultFont;
            speedText.text = "HIZ: 0.0 m/s";
            speedText.alignment = TextAnchor.MiddleCenter;
            speedText.color = new Color(0.4f, 0.9f, 1.0f);
            speedText.fontSize = 14;
            speedText.fontStyle = FontStyle.Bold;

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
            // No emoji: the built-in legacy font has no glyphs for them and draws blanks.
            victoryText.text = "TEBRİKLER! CEHENNEMDEN KAÇTIN!\n<size=18>Buz Bloğun Erimeden Portala Ulaştı!</size>";
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
            vBtnText.text = "TEKRAR OYNA";
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
            gameOverText.text = "CEHENNEMDE ERİDİN!\n<size=18>Buz Küpün Tamamen Sıcaklıkta Eridi</size>";
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
