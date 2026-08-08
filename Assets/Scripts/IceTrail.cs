using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IceEscape
{
    [RequireComponent(typeof(TrailRenderer))]
    public class IceTrail : MonoBehaviour
    {
        [Header("Wet Trail Settings")]
        [SerializeField] private float trailTime = 1.2f;
        [SerializeField] private float startWidth = 0.85f;
        [SerializeField] private float endWidth = 0.0f;
        [SerializeField] private float minEmitSpeed = 1.0f;

        private TrailRenderer trail;
        private Rigidbody parentRb;
        private ParticleSystem waterSizzleParticles;

        private void Awake()
        {
            trail = GetComponent<TrailRenderer>();
        }

        private void Start()
        {
            parentRb = PlayerLocator.FindPlayerBody();

            SetupTrail();
            CreateWaterSizzleParticles();
        }

        private void SetupTrail()
        {
            if (trail == null) return;

            trail.time = trailTime;
            trail.startWidth = startWidth;
            trail.endWidth = endWidth;
            trail.autodestruct = false;

            // Wet Water Track Gradient: Glossy Translucent Cyan -> Water Blue -> Transparent
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(new Color(0.4f, 0.9f, 1.0f), 0.0f), 
                    new GradientColorKey(new Color(0.2f, 0.6f, 0.9f), 0.6f),
                    new GradientColorKey(new Color(0.1f, 0.3f, 0.6f), 1.0f) 
                },
                new GradientAlphaKey[] { 
                    new GradientAlphaKey(0.7f, 0.0f), 
                    new GradientAlphaKey(0.4f, 0.5f),
                    new GradientAlphaKey(0.0f, 1.0f) 
                }
            );
            trail.colorGradient = gradient;

            Material trailMat = AssetDatabaseLoader.GetIceMaterial();
            if (trailMat != null)
            {
                trail.sharedMaterial = trailMat;
            }
        }

        private void CreateWaterSizzleParticles()
        {
            GameObject sizzleObj = new GameObject("WaterSizzleParticles");
            sizzleObj.transform.SetParent(transform, false);
            sizzleObj.transform.localPosition = new Vector3(0f, -0.4f, 0f);

            waterSizzleParticles = sizzleObj.AddComponent<ParticleSystem>();
            var main = waterSizzleParticles.main;
            main.startColor = new Color(0.7f, 0.95f, 1.0f, 0.5f);
            main.startSize = 0.18f;
            main.startLifetime = 0.5f;
            main.startSpeed = 0.4f;

            ParticleSystemRenderer psr = sizzleObj.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                Material pMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                pMat.color = new Color(0.7f, 0.95f, 1.0f, 0.5f);
                psr.sharedMaterial = pMat;
            }

            var emission = waterSizzleParticles.emission;
            emission.rateOverTime = 15f;

            var shape = waterSizzleParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;
        }

        private void Update()
        {
            if (parentRb != null && trail != null)
            {
                float speed = parentRb.linearVelocity.magnitude;
                bool isSliding = (speed > minEmitSpeed);

                trail.emitting = isSliding;

                if (waterSizzleParticles != null)
                {
                    var emission = waterSizzleParticles.emission;
                    emission.enabled = isSliding;
                }
            }
        }
    }

    internal static class AssetDatabaseLoader
    {
        public static Material GetIceMaterial()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/IceMaterial.mat");
#else
            return null;
#endif
        }
    }
}
