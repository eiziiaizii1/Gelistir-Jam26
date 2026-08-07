using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace IceEscape
{
    public class IceGameHUD : MonoBehaviour
    {
        [Header("UI References")]
        private Canvas hudCanvas;
        private Text instructionsText;
        private Text speedText;
        private Text distanceText;
        private Text meltText;
        private Image meltBarFill;
        private Image screenFlashOverlay;

        private GameObject gameOverPanel;
        private Text gameOverText;

        [Header("Target Tracking")]
        [SerializeField] private IcePlayerController playerController;
        [SerializeField] private Rigidbody playerRigidbody;

        private Vector3 startPosition;
        private float flashAlpha = 0f;
        private Color targetFlashColor = Color.clear;
        private bool isGameOver = false;

        private void Awake()
        {
            CreateHUDIfNeeded();
        }

        private void Start()
        {
            FindPlayerReferences();
            if (playerRigidbody != null)
            {
                startPosition = playerRigidbody.position;
            }
        }

        private void FindPlayerReferences()
        {
            if (playerController == null)
            {
                playerController = FindFirstObjectByType<IcePlayerController>();
            }

            if (playerController != null && playerRigidbody == null)
            {
                playerRigidbody = playerController.GetComponent<Rigidbody>();
                startPosition = playerRigidbody.position;
            }
        }

        private void Update()
        {
            if (playerController == null)
            {
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
            if (playerController != null && meltBarFill != null && meltText != null)
            {
                float meltRatio = playerController.CurrentMeltPercent;
                meltBarFill.fillAmount = Mathf.Lerp(meltBarFill.fillAmount, meltRatio, Time.deltaTime * 8f);

                int percentInt = Mathf.RoundToInt(meltRatio * 100f);
                meltText.text = $"BUZ HACMİ: %{percentInt}";

                // Dynamic Color interpolation: Cyan -> Orange -> Red
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

                // Game Over Check
                if (meltRatio <= 0.01f && !isGameOver)
                {
                    TriggerGameOver();
                }
            }

            // Handle Screen Flash Decay
            if (screenFlashOverlay != null && flashAlpha > 0.001f)
            {
                flashAlpha = Mathf.Lerp(flashAlpha, 0f, Time.deltaTime * 4f);
                screenFlashOverlay.color = new Color(targetFlashColor.r, targetFlashColor.g, targetFlashColor.b, flashAlpha);
            }
        }

        public void TriggerScreenFlash(Color color, float maxAlpha = 0.35f)
        {
            targetFlashColor = color;
            flashAlpha = maxAlpha;
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

            // Create Canvas
            GameObject canvasObj = new GameObject("IceHUDCanvas");
            hudCanvas = canvasObj.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null) defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 16);

            // 0. Screen Flash Overlay Image
            GameObject flashObj = new GameObject("ScreenFlashOverlay");
            flashObj.transform.SetParent(canvasObj.transform, false);

            RectTransform flashRect = flashObj.AddComponent<RectTransform>();
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.sizeDelta = Vector2.zero;

            screenFlashOverlay = flashObj.AddComponent<Image>();
            screenFlashOverlay.color = Color.clear;
            screenFlashOverlay.raycastTarget = false;

            // 1. Create Melt Meter Panel (Top Left)
            GameObject meltPanelObj = new GameObject("MeltMeterPanel");
            meltPanelObj.transform.SetParent(canvasObj.transform, false);

            RectTransform meltRect = meltPanelObj.AddComponent<RectTransform>();
            meltRect.anchorMin = new Vector2(0f, 1f);
            meltRect.anchorMax = new Vector2(0f, 1f);
            meltRect.pivot = new Vector2(0f, 1f);
            meltRect.anchoredPosition = new Vector2(20f, -20f);
            meltRect.sizeDelta = new Vector2(240f, 55f);

            Image meltPanelBg = meltPanelObj.AddComponent<Image>();
            meltPanelBg.color = new Color(0.05f, 0.08f, 0.15f, 0.82f);

            GameObject meltTextObj = new GameObject("MeltText");
            meltTextObj.transform.SetParent(meltPanelObj.transform, false);

            RectTransform mtRect = meltTextObj.AddComponent<RectTransform>();
            mtRect.anchorMin = new Vector2(0f, 0.5f);
            mtRect.anchorMax = new Vector2(1f, 1f);
            mtRect.sizeDelta = Vector2.zero;
            mtRect.anchoredPosition = new Vector2(0f, -2f);

            meltText = meltTextObj.AddComponent<Text>();
            meltText.font = defaultFont;
            meltText.text = "BUZ HACMİ: %100";
            meltText.alignment = TextAnchor.MiddleCenter;
            meltText.color = new Color(0.85f, 0.95f, 1.0f);
            meltText.fontSize = 14;
            meltText.fontStyle = FontStyle.Bold;

            GameObject barBgObj = new GameObject("BarBackground");
            barBgObj.transform.SetParent(meltPanelObj.transform, false);

            RectTransform barBgRect = barBgObj.AddComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0.05f, 0.1f);
            barBgRect.anchorMax = new Vector2(0.95f, 0.45f);
            barBgRect.sizeDelta = Vector2.zero;

            Image barBgImage = barBgObj.AddComponent<Image>();
            barBgImage.color = new Color(0.15f, 0.15f, 0.25f, 0.9f);

            GameObject barFillObj = new GameObject("BarFill");
            barFillObj.transform.SetParent(barBgObj.transform, false);

            RectTransform fillRect = barFillObj.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;

            meltBarFill = barFillObj.AddComponent<Image>();
            meltBarFill.type = Image.Type.Filled;
            meltBarFill.fillMethod = Image.FillMethod.Horizontal;
            meltBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            meltBarFill.color = new Color(0.1f, 0.9f, 1.0f);

            // 2. Distance Traveled Counter (Top Center)
            GameObject distObj = new GameObject("DistancePanel");
            distObj.transform.SetParent(canvasObj.transform, false);

            RectTransform distRect = distObj.AddComponent<RectTransform>();
            distRect.anchorMin = new Vector2(0.5f, 1f);
            distRect.anchorMax = new Vector2(0.5f, 1f);
            distRect.pivot = new Vector2(0.5f, 1f);
            distRect.anchoredPosition = new Vector2(0f, -20f);
            distRect.sizeDelta = new Vector2(180f, 45f);

            Image distBg = distObj.AddComponent<Image>();
            distBg.color = new Color(0.05f, 0.08f, 0.15f, 0.82f);

            GameObject distTextObj = new GameObject("DistanceText");
            distTextObj.transform.SetParent(distObj.transform, false);

            RectTransform dtRect = distTextObj.AddComponent<RectTransform>();
            dtRect.anchorMin = Vector2.zero;
            dtRect.anchorMax = Vector2.one;
            dtRect.sizeDelta = Vector2.zero;

            distanceText = distTextObj.AddComponent<Text>();
            distanceText.font = defaultFont;
            distanceText.text = "MESAFE: 0 m";
            distanceText.alignment = TextAnchor.MiddleCenter;
            distanceText.color = new Color(1.0f, 0.9f, 0.3f);
            distanceText.fontSize = 15;
            distanceText.fontStyle = FontStyle.Bold;

            // 3. Speedometer Panel (Top Right)
            GameObject speedObj = new GameObject("SpeedPanel");
            speedObj.transform.SetParent(canvasObj.transform, false);

            RectTransform speedRect = speedObj.AddComponent<RectTransform>();
            speedRect.anchorMin = new Vector2(1f, 1f);
            speedRect.anchorMax = new Vector2(1f, 1f);
            speedRect.pivot = new Vector2(1f, 1f);
            speedRect.anchoredPosition = new Vector2(-20f, -20f);
            speedRect.sizeDelta = new Vector2(150f, 45f);

            Image speedBg = speedObj.AddComponent<Image>();
            speedBg.color = new Color(0.05f, 0.08f, 0.15f, 0.82f);

            GameObject speedTextObj = new GameObject("SpeedText");
            speedTextObj.transform.SetParent(speedObj.transform, false);

            RectTransform stRect = speedTextObj.AddComponent<RectTransform>();
            stRect.anchorMin = Vector2.zero;
            stRect.anchorMax = Vector2.one;
            stRect.sizeDelta = Vector2.zero;

            speedText = speedTextObj.AddComponent<Text>();
            speedText.font = defaultFont;
            speedText.text = "HIZ: 0.0 m/s";
            speedText.alignment = TextAnchor.MiddleCenter;
            speedText.color = new Color(0.4f, 0.9f, 1.0f);
            speedText.fontSize = 15;
            speedText.fontStyle = FontStyle.Bold;

            // 4. Instructions Panel (Bottom Center)
            GameObject panelObj = new GameObject("InstructionsPanel");
            panelObj.transform.SetParent(canvasObj.transform, false);

            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 30f);
            panelRect.sizeDelta = new Vector2(560f, 55f);

            Image panelBg = panelObj.AddComponent<Image>();
            panelBg.color = new Color(0.05f, 0.08f, 0.15f, 0.82f);

            GameObject textObj = new GameObject("InstructionsText");
            textObj.transform.SetParent(panelObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            instructionsText = textObj.AddComponent<Text>();
            instructionsText.font = defaultFont;
            instructionsText.text = "🧊 <b>CEHENNEMDEN KAÇIŞ:</b> Tıkla & Savur (Şaplak)  |  ⌨️ WASD  |  🦘 Space: Zıpla";
            instructionsText.alignment = TextAnchor.MiddleCenter;
            instructionsText.color = new Color(0.85f, 0.95f, 1.0f);
            instructionsText.fontSize = 15;
            instructionsText.supportRichText = true;

            // 5. Game Over Panel
            gameOverPanel = new GameObject("GameOverPanel");
            gameOverPanel.transform.SetParent(canvasObj.transform, false);

            RectTransform goRect = gameOverPanel.AddComponent<RectTransform>();
            goRect.anchorMin = Vector2.zero;
            goRect.anchorMax = Vector2.one;
            goRect.sizeDelta = Vector2.zero;

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
            gameOverText.text = "🔥 CEHENNEMDE ERİDİN! 🔥\n<size=18>Buz Küpün Tamamen Sıcaklıkta Eridi</size>";
            gameOverText.alignment = TextAnchor.MiddleCenter;
            gameOverText.color = new Color(1.0f, 0.3f, 0.2f);
            gameOverText.fontSize = 28;
            gameOverText.fontStyle = FontStyle.Bold;
            gameOverText.supportRichText = true;

            // Restart Button
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

            Text btnText = btnTextObj.AddComponent<Text>();
            btnText.font = defaultFont;
            btnText.text = "TEKRAR DENE 🔄";
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.color = Color.white;
            btnText.fontSize = 18;
            btnText.fontStyle = FontStyle.Bold;

            gameOverPanel.SetActive(false);
        }
    }
}
