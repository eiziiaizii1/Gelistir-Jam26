using UnityEngine;

namespace IceEscape
{
    [RequireComponent(typeof(TrailRenderer))]
    public class IceTrail : MonoBehaviour
    {
        [Header("Wet Trail Settings")]
        [SerializeField] private float trailTime = 1.2f;
        [SerializeField] private float baseStartWidth = 0.95f;
        [SerializeField] private float endWidth = 0.0f;
        [SerializeField] private float minEmitSpeed = 1.0f;

        [Header("Vibrant Ice Colors")]
        [SerializeField] private Color iceBrightCyan = new Color(0.0f, 0.95f, 1.0f, 0.9f); // Electric Cyan
        [SerializeField] private Color iceDeepBlue = new Color(0.1f, 0.5f, 1.0f, 0.5f);    // Neon Blue

        private TrailRenderer trail;
        private Rigidbody parentRb;
        private ParticleSystem waterSizzleParticles;
        private IMeltSource meltSource;

        private void Awake()
        {
            trail = GetComponent<TrailRenderer>();
        }

        private void Start()
        {
            parentRb = PlayerLocator.FindPlayerBody();
            meltSource = PlayerLocator.FindMeltSource();

            SetupTrail();
            CreateWaterSizzleParticles();
        }

        private void SetupTrail()
        {
            if (trail == null) return;

            trail.time = trailTime;
            trail.startWidth = baseStartWidth;
            trail.endWidth = endWidth;
            trail.autodestruct = false;

            // Vibrant Electric Ice Blue Gradient
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(new Color(0.85f, 0.98f, 1.0f), 0.0f),  // Glowing White Core
                    new GradientColorKey(iceBrightCyan, 0.35f),                 // Electric Cyan
                    new GradientColorKey(iceDeepBlue, 1.0f)                    // Deep Neon Blue
                },
                new GradientAlphaKey[] { 
                    new GradientAlphaKey(0.85f, 0.0f), 
                    new GradientAlphaKey(0.55f, 0.5f),
                    new GradientAlphaKey(0.0f, 1.0f) 
                }
            );
            trail.colorGradient = gradient;

            // Use Unlit Emissive Material so red environment lighting doesn't turn the ice trail red
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            if (unlitShader != null)
            {
                Material unlitMat = new Material(unlitShader);
                unlitMat.SetColor("_BaseColor", iceBrightCyan);
                if (unlitMat.HasProperty("_EMISSION"))
                {
                    unlitMat.EnableKeyword("_EMISSION");
                    unlitMat.SetColor("_EmissionColor", iceBrightCyan * 2.0f);
                }
                trail.material = unlitMat;
            }
        }

        private void CreateWaterSizzleParticles()
        {
            GameObject sizzleObj = new GameObject("WaterSizzleParticles");
            sizzleObj.transform.SetParent(transform, false);
            sizzleObj.transform.localPosition = new Vector3(0f, -0.3f, 0f);

            waterSizzleParticles = sizzleObj.AddComponent<ParticleSystem>();
            ParticleSystemRenderer psr = sizzleObj.GetComponent<ParticleSystemRenderer>();

            var main = waterSizzleParticles.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.98f, 1.0f, 0.9f), iceBrightCyan);
            main.startSize = 0.22f;
            main.startLifetime = 0.55f;
            main.startSpeed = 0.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            Shader unlitShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            if (unlitShader != null && psr != null)
            {
                Material pMat = new Material(unlitShader);
                pMat.SetColor("_BaseColor", iceBrightCyan);
                if (pMat.HasProperty("_EMISSION"))
                {
                    pMat.EnableKeyword("_EMISSION");
                    pMat.SetColor("_EmissionColor", iceBrightCyan * 2.2f);
                }
                psr.sharedMaterial = pMat;
            }

            var emission = waterSizzleParticles.emission;
            emission.rateOverTime = 25f;

            var shape = waterSizzleParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.35f;
        }

        private void Update()
        {
            if (parentRb == null || trail == null) return;

            if (meltSource == null)
            {
                meltSource = PlayerLocator.FindMeltSource();
            }

            float meltRatio = (meltSource != null) ? Mathf.Clamp01(meltSource.CurrentMeltPercent) : 1.0f;
            float speed = parentRb.linearVelocity.magnitude;
            bool isSliding = (speed > minEmitSpeed);

            trail.emitting = isSliding;

            // Dynamic Scaling with Ice Melt Size!
            trail.startWidth = baseStartWidth * meltRatio;

            if (waterSizzleParticles != null)
            {
                var emission = waterSizzleParticles.emission;
                emission.enabled = isSliding;

                var main = waterSizzleParticles.main;
                main.startSize = 0.22f * meltRatio;

                var shape = waterSizzleParticles.shape;
                shape.radius = 0.35f * meltRatio;
            }
        }
    }
}
