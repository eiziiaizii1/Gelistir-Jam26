using UnityEngine;
using UnityEngine.UI;

namespace IceEscape
{
    public class IceGameHUD : MonoBehaviour
    {
        [Header("UI References")]
        private Canvas hudCanvas;
        private Text instructionsText;
        private Text speedText;

        [Header("Target Tracking")]
        [SerializeField] private Rigidbody playerRigidbody;

        private void Awake()
        {
            CreateHUDIfNeeded();
        }

        private void Start()
        {
            if (playerRigidbody == null)
            {
                IcePlayerController player = FindFirstObjectByType<IcePlayerController>();
                if (player != null)
                {
                    playerRigidbody = player.GetComponent<Rigidbody>();
                }
            }
        }

        private void Update()
        {
            if (speedText != null && playerRigidbody != null)
            {
                float speed = playerRigidbody.linearVelocity.magnitude;
                speedText.text = $"HIZ: {speed:F1} m/s";
            }
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

            // Create Instructions Panel
            GameObject panelObj = new GameObject("InstructionsPanel");
            panelObj.transform.SetParent(canvasObj.transform, false);

            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 30f);
            panelRect.sizeDelta = new Vector2(500f, 70f);

            Image panelBg = panelObj.AddComponent<Image>();
            panelBg.color = new Color(0.05f, 0.08f, 0.15f, 0.75f);

            // Instructions Text
            GameObject textObj = new GameObject("InstructionsText");
            textObj.transform.SetParent(panelObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            instructionsText = textObj.AddComponent<Text>();
            instructionsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (instructionsText.font == null) instructionsText.font = Font.CreateDynamicFontFromOSFont("Arial", 18);
            instructionsText.text = "🧊 <b>KONTROLLER:</b> Mouse ile Sürükle (Drag)  |  ⌨️ WASD  |  🦘 Space: Zıpla";
            instructionsText.alignment = TextAnchor.MiddleCenter;
            instructionsText.color = new Color(0.85f, 0.95f, 1.0f);
            instructionsText.fontSize = 16;
            instructionsText.supportRichText = true;

            // Speedometer Panel
            GameObject speedObj = new GameObject("SpeedPanel");
            speedObj.transform.SetParent(canvasObj.transform, false);

            RectTransform speedRect = speedObj.AddComponent<RectTransform>();
            speedRect.anchorMin = new Vector2(1f, 1f);
            speedRect.anchorMax = new Vector2(1f, 1f);
            speedRect.pivot = new Vector2(1f, 1f);
            speedRect.anchoredPosition = new Vector2(-20f, -20f);
            speedRect.sizeDelta = new Vector2(160f, 45f);

            Image speedBg = speedObj.AddComponent<Image>();
            speedBg.color = new Color(0.05f, 0.08f, 0.15f, 0.75f);

            GameObject speedTextObj = new GameObject("SpeedText");
            speedTextObj.transform.SetParent(speedObj.transform, false);

            RectTransform stRect = speedTextObj.AddComponent<RectTransform>();
            stRect.anchorMin = Vector2.zero;
            stRect.anchorMax = Vector2.one;
            stRect.sizeDelta = Vector2.zero;

            speedText = speedTextObj.AddComponent<Text>();
            speedText.font = instructionsText.font;
            speedText.text = "HIZ: 0.0 m/s";
            speedText.alignment = TextAnchor.MiddleCenter;
            speedText.color = new Color(0.4f, 0.9f, 1.0f);
            speedText.fontSize = 16;
            speedText.fontStyle = FontStyle.Bold;
        }
    }
}
