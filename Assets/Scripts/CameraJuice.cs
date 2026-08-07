using UnityEngine;

namespace IceEscape
{
    [RequireComponent(typeof(Camera))]
    public class CameraJuice : MonoBehaviour
    {
        [Header("Target Tracking")]
        [SerializeField] private Rigidbody targetRigidbody;

        [Header("Speed FOV Settings")]
        [SerializeField] private float minFOV = 60f;
        [SerializeField] private float maxFOV = 74f;
        [SerializeField] private float speedForMaxFOV = 20f;
        [SerializeField] private float fovDamping = 4f;

        [Header("Camera Shake Settings")]
        [SerializeField] private float shakeDamping = 5f;

        private Camera cam;
        private Vector3 shakeOffset;
        private float currentShakeIntensity;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        private void Start()
        {
            if (targetRigidbody == null)
            {
                IcePlayerController player = FindFirstObjectByType<IcePlayerController>();
                if (player != null)
                {
                    targetRigidbody = player.GetComponent<Rigidbody>();
                }
            }
        }

        private void Update()
        {
            HandleSpeedFOV();
            HandleCameraShake();
        }

        private void HandleSpeedFOV()
        {
            if (cam == null || targetRigidbody == null) return;

            float speed = targetRigidbody.linearVelocity.magnitude;
            float speedRatio = Mathf.Clamp01(speed / speedForMaxFOV);
            float targetFOV = Mathf.Lerp(minFOV, maxFOV, speedRatio);

            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * fovDamping);
        }

        private void HandleCameraShake()
        {
            if (currentShakeIntensity > 0.01f)
            {
                shakeOffset = Random.insideUnitSphere * currentShakeIntensity;
                currentShakeIntensity = Mathf.Lerp(currentShakeIntensity, 0f, Time.deltaTime * shakeDamping);
            }
            else
            {
                shakeOffset = Vector3.zero;
                currentShakeIntensity = 0f;
            }

            transform.localPosition += shakeOffset;
        }

        public void TriggerShake(float intensity)
        {
            currentShakeIntensity = Mathf.Max(currentShakeIntensity, intensity);
        }
    }
}
