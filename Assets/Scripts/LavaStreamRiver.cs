using UnityEngine;

namespace IceEscape
{
    /// <summary>
    /// Spans horizontally across the track width. When the player slides over this lava river:
    /// 1. Ice melts rapidly (high melt damage per sec).
    /// 2. Player slows down due to molten drag.
    /// 3. Steam sizzle particles and orange burn flash trigger!
    /// Jumping over it clears the hazard cleanly!
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class LavaStreamRiver : MonoBehaviour
    {
        [Header("Lava Hazard Settings")]
        [SerializeField] private float meltDamagePerSec = 0.28f; // Melts 28% ice per second inside lava
        [SerializeField] private float speedDragFactor = 0.94f;   // Slows linearVelocity every frame
        [SerializeField] private float cameraShakeIntensity = 0.2f;

        [Header("Visual Effects")]
        [SerializeField] private Color lavaColor = new Color(1.0f, 0.25f, 0.02f, 0.95f);
        [SerializeField] private Color lavaEmissiveColor = new Color(2.5f, 0.6f, 0.1f, 1.0f);

        private BoxCollider triggerCol;
        private Material lavaMaterial;
        private ParticleSystem steamParticles;
        private float nextFlashTime;

        private void Awake()
        {
            triggerCol = GetComponent<BoxCollider>();
            if (triggerCol != null)
            {
                triggerCol.isTrigger = true;
            }

            SetupVisuals();
            CreateSteamParticles();
        }

        private void SetupVisuals()
        {
            MeshRenderer mr = GetComponent<MeshRenderer>();
            if (mr == null)
            {
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "LavaVisual";
                visual.transform.SetParent(transform, false);
                visual.transform.localPosition = new Vector3(0f, -0.1f, 0f);
                visual.transform.localScale = Vector3.one;

                DestroyImmediate(visual.GetComponent<Collider>());
                mr = visual.GetComponent<MeshRenderer>();
            }

            if (mr != null)
            {
                Shader urpShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                lavaMaterial = new Material(urpShader);
                lavaMaterial.color = lavaColor;
                if (lavaMaterial.HasProperty("_EMISSION"))
                {
                    lavaMaterial.EnableKeyword("_EMISSION");
                    lavaMaterial.SetColor("_EmissionColor", lavaEmissiveColor);
                }
                mr.sharedMaterial = lavaMaterial;
            }
        }

        private void CreateSteamParticles()
        {
            GameObject pObj = new GameObject("LavaSteamParticles");
            pObj.transform.SetParent(transform, false);
            pObj.transform.localPosition = Vector3.up * 0.2f;

            steamParticles = pObj.AddComponent<ParticleSystem>();
            var main = steamParticles.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1.0f, 0.4f, 0.1f, 0.6f), new Color(0.9f, 0.9f, 0.9f, 0.4f));
            main.startSize = 0.6f;
            main.startLifetime = 0.8f;
            main.startSpeed = 1.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystemRenderer psr = pObj.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                Material pMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default"));
                pMat.color = new Color(1.0f, 0.5f, 0.1f, 0.6f);
                psr.sharedMaterial = pMat;
            }

            var emission = steamParticles.emission;
            emission.rateOverTime = 20f;

            var shape = steamParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = transform.lossyScale;
        }

        private void Update()
        {
            // Animate lava texture scrolling effect
            if (lavaMaterial != null && lavaMaterial.HasProperty("_BaseMap"))
            {
                Vector2 offset = new Vector2(Time.time * 0.1f, Time.time * 0.25f);
                lavaMaterial.SetTextureOffset("_BaseMap", offset);
            }
        }

        private void OnTriggerStay(Collider other)
        {
            IMeltSource playerMelt = PlayerLocator.GetMeltSource(other.gameObject);
            if (playerMelt == null) return;

            MonoBehaviour playerMb = playerMelt as MonoBehaviour;
            Rigidbody rb = (playerMb != null) ? playerMb.GetComponentInParent<Rigidbody>() : null;

            // 1. Ice Melt Damage Over Time
            playerMelt.RestoreIce(-meltDamagePerSec * Time.deltaTime);

            // 2. Speed Slowdown / Molten Drag
            if (rb != null && rb.linearVelocity.sqrMagnitude > 0.1f)
            {
                Vector3 vel = rb.linearVelocity;
                vel.x *= speedDragFactor;
                vel.z *= speedDragFactor;
                rb.linearVelocity = vel;
            }

            // 3. Screen Flash & Camera Shake Feedback
            if (Time.time >= nextFlashTime)
            {
                nextFlashTime = Time.time + 0.25f;

                IceGameHUD hud = PlayerLocator.FindHUD();
                if (hud != null)
                {
                    hud.TriggerScreenFlash(new Color(1.0f, 0.25f, 0.05f), 0.35f);
                }

                PlayerLocator.ShakeCamera(cameraShakeIntensity);

                if (IceAudioManager.Instance != null)
                {
                    IceAudioManager.Instance.PlayExplosion();
                }
            }
        }
    }
}
