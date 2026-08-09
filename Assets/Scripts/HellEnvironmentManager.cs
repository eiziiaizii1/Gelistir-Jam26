using UnityEngine;

namespace IceEscape
{
    /// <summary>
    /// Manages atmospheric Hell environment effects:
    /// - Continuous floating lava embers & ash particles drifting around player
    /// - Dynamic warm fog and directional light color tuning
    /// </summary>
    public class HellEnvironmentManager : MonoBehaviour
    {
        [Header("Ember & Ash Particle System")]
        [SerializeField] private Color emberColorStart = new Color(1.0f, 0.4f, 0.05f, 0.9f); // Fiery Orange
        [SerializeField] private Color emberColorEnd = new Color(1.0f, 0.1f, 0.0f, 0.0f);   // Crimson Fade
        [SerializeField] private int maxEmbers = 350;

        [Header("Atmospheric Fog & Lighting")]
        [SerializeField] private bool overrideFog = true;
        [SerializeField] private Color hellFogColor = new Color(0.28f, 0.04f, 0.03f, 1.0f); // Fiery Deep Red
        [SerializeField] private float fogDensity = 0.022f;

        private ParticleSystem emberParticles;
        private Transform playerTransform;

        private void Start()
        {
            ApplyHellAtmosphere();
            CreateEmberParticleSystem();
        }

        private void ApplyHellAtmosphere()
        {
            if (overrideFog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogColor = hellFogColor;
                RenderSettings.fogDensity = fogDensity;
            }

            // Ensure main directional light is warm burning orange
            Light mainLight = RenderSettings.sun;
            if (mainLight == null)
            {
                Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                foreach (Light l in lights)
                {
                    if (l.type == LightType.Directional)
                    {
                        mainLight = l;
                        break;
                    }
                }
            }

            if (mainLight != null)
            {
                mainLight.color = new Color(1.0f, 0.5f, 0.2f);
                mainLight.intensity = 1.3f;
            }
        }

        private void CreateEmberParticleSystem()
        {
            GameObject particleObj = new GameObject("Hell_EmberAshFX");
            particleObj.transform.SetParent(transform, false);

            emberParticles = particleObj.AddComponent<ParticleSystem>();
            ParticleSystemRenderer psRenderer = particleObj.GetComponent<ParticleSystemRenderer>();

            // Main Settings
            var main = emberParticles.main;
            main.maxParticles = maxEmbers;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 5.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.0f, 4.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.28f);
            main.startColor = new ParticleSystem.MinMaxGradient(emberColorStart, emberColorEnd);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // Emission
            var emission = emberParticles.emission;
            emission.rateOverTime = 60f;

            // Shape (Wide box ahead of camera)
            var shape = emberParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40f, 15f, 40f);

            // Color Over Lifetime
            var colorLife = emberParticles.colorOverLifetime;
            colorLife.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.6f, 0.1f), 0f), new GradientColorKey(new Color(1f, 0.1f, 0f), 0.7f), new GradientColorKey(new Color(0.2f, 0f, 0f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.2f), new GradientAlphaKey(0f, 1f) }
            );
            colorLife.color = grad;

            // Size Over Lifetime (Pulsing embers)
            var sizeLife = emberParticles.sizeOverLifetime;
            sizeLife.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.2f);
            curve.AddKey(0.5f, 1.0f);
            curve.AddKey(1f, 0.1f);
            sizeLife.size = new ParticleSystem.MinMaxCurve(1.0f, curve);

            // Velocity Over Lifetime (Upward heat drift)
            var velocity = emberParticles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = new ParticleSystem.MinMaxCurve(0.8f, 2.5f);
            velocity.z = new ParticleSystem.MinMaxCurve(-1.0f, 1.0f);

            // Material
            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
            if (particleShader != null)
            {
                Material mat = new Material(particleShader);
                mat.SetColor("_BaseColor", Color.white);
                if (mat.HasProperty("_EMISSION"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(1f, 0.5f, 0.1f) * 2.5f);
                }
                psRenderer.sharedMaterial = mat;
            }
        }

        private void Update()
        {
            if (emberParticles == null) return;

            if (playerTransform == null)
            {
                Rigidbody body = PlayerLocator.FindPlayerBody();
                if (body != null) playerTransform = body.transform;
            }

            if (playerTransform != null)
            {
                // Position particle box slightly ahead of player so embers float toward screen
                emberParticles.transform.position = playerTransform.position + playerTransform.forward * 10f + Vector3.up * 2f;
            }
        }
    }
}
