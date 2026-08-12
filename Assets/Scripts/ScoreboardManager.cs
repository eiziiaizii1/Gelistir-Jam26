using System.Collections.Generic;
using UnityEngine;

namespace IceEscape
{
    /// <summary>
    /// Self-contained 2D Screen-Space Arcade Rush Scoreboard.
    /// Modular & single-click toggleable: disable this GameObject and all score tracking/UI completely disappears!
    /// Features: 2D Center-screen Animated Fiery Trick Popups, Top Center Rush Score Header, Air Ticker!
    /// </summary>
    public class ScoreboardManager : MonoBehaviour
    {
        [Header("Score Settings")]
        [SerializeField] private float scorePerMeter = 1.0f;
        [SerializeField] private float highSpeedThreshold = 25f;
        [SerializeField] private float highSpeedBonusRate = 25f;

        [Header("Trick Rewards")]
        [SerializeField] private int slapScore = 150;
        [SerializeField] private int landingScore = 200;
        [SerializeField] private int nearMissScore = 300;

        [Header("GUI Style & Positioning")]
        [SerializeField] private Color mainColor = new Color(1.0f, 0.85f, 0.1f); // Arcade Gold
        [SerializeField] private int fontSize = 36;
        [Tooltip("Overall scoreboard size multiplier. 1 = authored size, 1.25 = a quarter " +
                 "larger. Applied as a GUI matrix scale so score, ticker and trick popups all " +
                 "grow together and stay centred.")]
        [SerializeField] private float uiScale = 1.25f;
        [SerializeField] private Font customFont;

        private float currentScore;
        private float currentMultiplier = 1.0f;
        private float multiplierTimer;

        private Transform playerTransform;
        private Rigidbody playerBody;
        private IceSlideController playerController;

        private Vector3 lastPlayerPos;
        private float totalDistanceTraveled;

        private bool isAirborne;
        private float airTimeCounter;
        private float accumulatedAirScore;
        private float airMultiplier = 1.0f;

        private HashSet<int> nearMissedObstacleIDs = new HashSet<int>();
        private float scorePulseTimer;

        private struct TrickPopup
        {
            public string text;
            public int points;
            public float timeRemaining;
            public Color color;
        }

        private List<TrickPopup> activePopups = new List<TrickPopup>();

        private GUIStyle scoreStyle;
        private GUIStyle trickStyle;
        private GUIStyle labelStyle;
        private GUIStyle airStyle;

        private void OnEnable()
        {
            FindAndSubscribePlayer();
            ResetScore();
        }

        private void OnDisable()
        {
            UnsubscribePlayer();
        }

        private void ResetScore()
        {
            currentScore = 0f;
            currentMultiplier = 1.0f;
            totalDistanceTraveled = 0f;
            nearMissedObstacleIDs.Clear();
            activePopups.Clear();
            scorePulseTimer = 0f;
            isAirborne = false;
            airTimeCounter = 0f;
            accumulatedAirScore = 0f;
            airMultiplier = 1.0f;
        }

        private void FindAndSubscribePlayer()
        {
            UnsubscribePlayer();

            playerBody = PlayerLocator.FindPlayerBody();
            if (playerBody != null)
            {
                playerTransform = playerBody.transform;
                lastPlayerPos = playerTransform.position;
                playerController = playerBody.GetComponent<IceSlideController>();
                if (playerController != null)
                {
                    playerController.Slapped += OnPlayerSlapped;
                    playerController.Landed += OnPlayerLanded;
                }
            }
        }

        private void UnsubscribePlayer()
        {
            if (playerController != null)
            {
                playerController.Slapped -= OnPlayerSlapped;
                playerController.Landed -= OnPlayerLanded;
            }
            playerController = null;
        }

        private void OnPlayerSlapped(Vector3 impulse)
        {
            int pts = Mathf.RoundToInt(slapScore * currentMultiplier);
            AddTrick("💥 FIRE SLAP!", pts, new Color(1.0f, 0.55f, 0.05f));
            BumpMultiplier(0.2f);
        }

        private void OnPlayerLanded(float landingSpeed)
        {
            if (airTimeCounter > 0.3f && accumulatedAirScore > 20f)
            {
                int finalAirPts = Mathf.RoundToInt((landingScore + (accumulatedAirScore * airMultiplier)) * currentMultiplier);
                AddTrick($"🔥 INFERNO TOUCHDOWN!", finalAirPts, new Color(1.0f, 0.25f, 0.05f));
                BumpMultiplier(0.4f);

                PlayerLocator.ShakeCamera(0.35f);
                ObstacleImpactBurst.Spawn(playerTransform.position + Vector3.up * 0.4f, new Color(1.0f, 0.3f, 0.05f), 0.9f);
            }

            isAirborne = false;
            airTimeCounter = 0f;
            accumulatedAirScore = 0f;
            airMultiplier = 1.0f;
        }

        private void BumpMultiplier(float amount)
        {
            currentMultiplier = Mathf.Min(currentMultiplier + amount, 5.0f);
            multiplierTimer = 4.0f;
            scorePulseTimer = 0.3f;
        }

        private void Update()
        {
            if (playerTransform == null || playerBody == null)
            {
                FindAndSubscribePlayer();
                if (playerTransform == null) return;
            }

            float currentSpeed = playerBody.linearVelocity.magnitude;

            // 1. Distance score
            float distStep = Vector3.Distance(playerTransform.position, lastPlayerPos);
            if (distStep > 0.05f && distStep < 20f)
            {
                totalDistanceTraveled += distStep;
                currentScore += distStep * scorePerMeter * currentMultiplier;
            }
            lastPlayerPos = playerTransform.position;

            // 2. High speed bonus
            if (currentSpeed > highSpeedThreshold)
            {
                currentScore += highSpeedBonusRate * Time.deltaTime * currentMultiplier;
            }

            // 3. Live Airborne Accumulator
            bool grounded = Physics.Raycast(playerTransform.position + Vector3.up * 0.5f, Vector3.down, 1.2f);
            if (!grounded)
            {
                isAirborne = true;
                airTimeCounter += Time.deltaTime;

                float speedFactor = Mathf.Max(1.0f, currentSpeed / 20f);
                float rate = (180f * speedFactor) * currentMultiplier;
                accumulatedAirScore += rate * Time.deltaTime;

                airMultiplier = 1.0f + (airTimeCounter * 0.75f) + (currentSpeed / 35f);
            }

            // 4. Multiplier decay
            if (multiplierTimer > 0f)
            {
                multiplierTimer -= Time.deltaTime;
                if (multiplierTimer <= 0f)
                {
                    currentMultiplier = 1.0f;
                }
            }

            if (scorePulseTimer > 0f)
            {
                scorePulseTimer -= Time.deltaTime;
            }

            // 5. Near-Miss obstacle detection
            CheckNearMisses();

            // 6. Update trick popups animation
            for (int i = activePopups.Count - 1; i >= 0; i--)
            {
                TrickPopup p = activePopups[i];
                p.timeRemaining -= Time.deltaTime;
                if (p.timeRemaining <= 0f)
                    activePopups.RemoveAt(i);
                else
                    activePopups[i] = p;
            }
        }

        private void CheckNearMisses()
        {
            Collider[] hits = Physics.OverlapSphere(playerTransform.position, 3.4f);
            foreach (Collider col in hits)
            {
                if (col.transform.IsChildOf(playerTransform)) continue;

                int id = col.GetInstanceID();
                if (nearMissedObstacleIDs.Contains(id)) continue;

                if (col.GetComponent<ObstacleHazard>() != null || col.GetComponent<HellObstacle>() != null || col.name.Contains("Obstacle"))
                {
                    float dist = Vector3.Distance(playerTransform.position, col.ClosestPoint(playerTransform.position));
                    if (dist > 0.5f && dist < 2.6f)
                    {
                        nearMissedObstacleIDs.Add(id);
                        int pts = Mathf.RoundToInt(nearMissScore * currentMultiplier);
                        AddTrick("🎯 DEVIL DODGE!", pts, new Color(1.0f, 0.9f, 0.15f));
                        BumpMultiplier(0.25f);
                    }
                }
            }
        }

        private void AddTrick(string title, int points, Color color)
        {
            currentScore += points;
            scorePulseTimer = 0.25f;
            TrickPopup popup = new TrickPopup
            {
                text = $"{title} +{points}",
                points = points,
                timeRemaining = 1.8f,
                color = color
            };
            activePopups.Add(popup);
        }

        private void OnGUI()
        {
            InitStyles();

            // Scale the whole IMGUI pass instead of touching each Rect and font size. The
            // matrix also scales the coordinate space, so every layout calculation below has to
            // use these virtual dimensions - reading Screen.width directly under a scaled
            // matrix would push centred elements off to the right.
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
            float viewWidth = Screen.width / uiScale;
            float viewHeight = Screen.height / uiScale;

            // 1. TOP CENTER - 2D Rush Score Header
            float headerWidth = 500f;
            float headerX = (viewWidth - headerWidth) * 0.5f;

            int currentFontSize = (scorePulseTimer > 0f) ? Mathf.RoundToInt(fontSize * 1.25f) : fontSize;
            scoreStyle.fontSize = currentFontSize;

            string scoreStr = $"{Mathf.RoundToInt(currentScore)} PTS";

            // Outer Fiery Glow Shadows
            scoreStyle.normal.textColor = new Color(0.35f, 0.02f, 0.02f, 0.95f);
            GUI.Label(new Rect(headerX + 3f, 18f, headerWidth, 45f), scoreStr, scoreStyle);

            scoreStyle.normal.textColor = new Color(1.0f, 0.45f, 0.05f, 0.95f);
            GUI.Label(new Rect(headerX + 1.5f, 16.5f, headerWidth, 45f), scoreStr, scoreStyle);

            // Core Gold Center
            scoreStyle.normal.textColor = mainColor;
            GUI.Label(new Rect(headerX, 15f, headerWidth, 45f), scoreStr, scoreStyle);

            // Subheader
            string multText = currentMultiplier > 1.01f ? $"X{currentMultiplier:F1} MULTIPLIER | {Mathf.RoundToInt(totalDistanceTraveled)} METERS" : $"{Mathf.RoundToInt(totalDistanceTraveled)} METER SLIDE";
            labelStyle.normal.textColor = currentMultiplier > 1.01f ? new Color(1.0f, 0.45f, 0.1f) : Color.white;

            GUI.color = Color.black;
            GUI.Label(new Rect(headerX + 1f, 57f, headerWidth, 25f), multText, labelStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(headerX, 56f, headerWidth, 25f), multText, labelStyle);

            // 2. LIVE AIRBORNE TICKER
            if (isAirborne && airTimeCounter > 0.15f)
            {
                float airBoxY = viewHeight * 0.60f;
                int airPts = Mathf.RoundToInt((accumulatedAirScore * airMultiplier) * currentMultiplier);

                string airText = $"VOLCANIC AIR {airTimeCounter:F1}S | +{airPts} PTS";
                float pulseScale = 1.0f + Mathf.Sin(Time.time * 12f) * 0.08f;
                airStyle.fontSize = Mathf.RoundToInt(28 * pulseScale);

                airStyle.normal.textColor = new Color(0.35f, 0f, 0f, 0.95f);
                GUI.Label(new Rect((viewWidth - 500f) * 0.5f + 2f, airBoxY + 2f, 500f, 45f), airText, airStyle);

                airStyle.normal.textColor = new Color(1.0f, 0.4f, 0.05f);
                GUI.Label(new Rect((viewWidth - 500f) * 0.5f, airBoxY, 500f, 45f), airText, airStyle);
            }

            // 3. 2D CENTER SCREEN - Non-Overlapping Animated Trick Popups
            float popupCenterY = viewHeight * 0.30f;
            for (int i = 0; i < activePopups.Count; i++)
            {
                TrickPopup p = activePopups[i];
                float elapsed = 1.8f - p.timeRemaining;
                float alpha = Mathf.Clamp01(p.timeRemaining / 0.4f);

                float scale = (elapsed < 0.2f) ? Mathf.Lerp(1.4f, 1.0f, elapsed / 0.2f) : 1.0f;
                float offsetY = elapsed * 50f;

                int pFontSize = Mathf.RoundToInt(34 * scale);
                trickStyle.fontSize = pFontSize;

                // Stack cleanly vertically so multiple popups never overlap!
                float yPos = popupCenterY - offsetY - ((activePopups.Count - 1 - i) * 55f);
                Rect pRect = new Rect((viewWidth - 550f) * 0.5f, yPos, 550f, 55f);

                // Multi-Layer Shadows for contrast
                trickStyle.normal.textColor = new Color(0.2f, 0.0f, 0.0f, alpha);
                GUI.Label(new Rect(pRect.x + 3f, pRect.y + 3f, pRect.width, pRect.height), p.text, trickStyle);

                trickStyle.normal.textColor = new Color(1.0f, 0.45f, 0.05f, alpha);
                GUI.Label(new Rect(pRect.x + 1.5f, pRect.y + 1.5f, pRect.width, pRect.height), p.text, trickStyle);

                trickStyle.normal.textColor = new Color(p.color.r, p.color.g, p.color.b, alpha);
                GUI.Label(pRect, p.text, trickStyle);
            }

            // GUI.matrix is global state shared by every OnGUI this frame, so put it back.
            GUI.matrix = previousMatrix;
        }

        private void InitStyles()
        {
            if (customFont == null)
            {
#if UNITY_EDITOR
                customFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/ManufacturingConsent-Regular.ttf");
#endif
            }

            if (customFont == null)
            {
                customFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            if (scoreStyle == null)
            {
                scoreStyle = new GUIStyle(GUI.skin.label)
                {
                    font = customFont,
                    fontSize = fontSize,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    font = customFont,
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            if (trickStyle == null)
            {
                trickStyle = new GUIStyle(GUI.skin.label)
                {
                    font = customFont,
                    fontSize = 34,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            if (airStyle == null)
            {
                airStyle = new GUIStyle(GUI.skin.label)
                {
                    font = customFont,
                    fontSize = 28,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }
        }
    }
}
