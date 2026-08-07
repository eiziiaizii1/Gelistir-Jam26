using UnityEngine;

namespace IceEscape
{
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target Settings")]
        [SerializeField] private Transform target;

        [Header("Position Settings")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 6f, -10f);
        [SerializeField] private float smoothSpeed = 8f;

        [Header("Rotation Settings")]
        [SerializeField] private bool lookAtTarget = true;
        [SerializeField] private Vector3 lookAtOffset = new Vector3(0f, 1f, 0f);

        [Header("Speed FOV Settings")]
        [SerializeField] private float minFOV = 60f;
        [SerializeField] private float maxFOV = 74f;
        [SerializeField] private float speedForMaxFOV = 20f;
        [SerializeField] private float fovDamping = 4f;

        [Header("Camera Shake Settings")]
        [SerializeField] private float shakeDamping = 6f;

        private Camera cam;
        private Rigidbody targetRb;
        private Vector3 shakeOffset;
        private float currentShakeIntensity;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        private void Start()
        {
            if (target == null)
            {
                IcePlayerController player = FindFirstObjectByType<IcePlayerController>();
                if (player != null)
                {
                    target = player.transform;
                }
            }

            if (target != null)
            {
                targetRb = target.GetComponent<Rigidbody>();
            }
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            if (targetRb == null)
            {
                targetRb = target.GetComponent<Rigidbody>();
            }

            // 1. Follow Target
            Vector3 desiredPosition = target.position + offset;
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

            // 2. Camera Shake Offset
            if (currentShakeIntensity > 0.001f)
            {
                shakeOffset = Random.insideUnitSphere * currentShakeIntensity;
                currentShakeIntensity = Mathf.Lerp(currentShakeIntensity, 0f, Time.deltaTime * shakeDamping);
            }
            else
            {
                shakeOffset = Vector3.zero;
                currentShakeIntensity = 0f;
            }

            transform.position = smoothedPosition + shakeOffset;

            // 3. Speed-based FOV
            if (cam != null && targetRb != null)
            {
                float speed = targetRb.linearVelocity.magnitude;
                float speedRatio = Mathf.Clamp01(speed / speedForMaxFOV);
                float targetFOV = Mathf.Lerp(minFOV, maxFOV, speedRatio);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * fovDamping);
            }

            // 4. Look At Target
            if (lookAtTarget)
            {
                transform.LookAt(target.position + lookAtOffset);
            }
        }

        public void TriggerShake(float intensity)
        {
            currentShakeIntensity = Mathf.Max(currentShakeIntensity, intensity);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null) targetRb = target.GetComponent<Rigidbody>();
        }
    }
}
