using UnityEngine;

namespace IceEscape
{
    [RequireComponent(typeof(TrailRenderer))]
    public class IceTrail : MonoBehaviour
    {
        private TrailRenderer trail;
        private Rigidbody parentRb;

        private void Awake()
        {
            trail = GetComponent<TrailRenderer>();
        }

        private void Start()
        {
            IcePlayerController player = FindFirstObjectByType<IcePlayerController>();
            if (player != null)
            {
                parentRb = player.GetComponent<Rigidbody>();
            }

            SetupTrailMaterial();
        }

        private void SetupTrailMaterial()
        {
            if (trail == null) return;

            trail.time = 0.8f;
            trail.startWidth = 0.7f;
            trail.endWidth = 0.0f;
            trail.autodestruct = false;

            // Gradient: Translucent Light Cyan to Transparent
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.4f, 0.9f, 1.0f), 0.0f), new GradientColorKey(new Color(0.8f, 0.95f, 1.0f), 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.6f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            trail.colorGradient = gradient;

            // Try loading IceMaterial or Default particle shader
            Material trailMat = AssetDatabaseLoader.GetIceMaterial();
            if (trailMat != null)
            {
                trail.sharedMaterial = trailMat;
            }
        }

        private void Update()
        {
            if (parentRb != null && trail != null)
            {
                // Emit trail when moving fast enough
                float speed = parentRb.linearVelocity.magnitude;
                trail.emitting = (speed > 1.5f);
            }
        }
    }

    internal static class AssetDatabaseLoader
    {
        public static Material GetIceMaterial()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/IceMaterial.mat");
#else
            return null;
#endif
        }
    }
}
