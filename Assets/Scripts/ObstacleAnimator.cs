using UnityEngine;

namespace IceEscape
{
    public enum ObstacleAnimType
    {
        LavaFloat,       // Sağa-sola lavda yüzer gibi sallanma (Floating side-to-side)
        SubmergeEmerge,  // Yerin altına girip çıkma (Pop-up/Submerge in lava)
        SpinningRotator, // Kendi etrafında dönme (Spinning pillar)
        SineWaveCustom   // Özel X, Y, Z frekans hareketleri
    }

    public class ObstacleAnimator : MonoBehaviour
    {
        [Header("Animation Mode")]
        [SerializeField] private ObstacleAnimType animType = ObstacleAnimType.LavaFloat;

        [Header("1. Lava Float Settings (Lavda Sağa-Sola Yüzme)")]
        [SerializeField] private float sideMoveDistance = 2.5f;   // Sağa-sola gitme mesafesi
        [SerializeField] private float sideMoveSpeed = 1.8f;      // Sağa-sola gitme hızı
        [SerializeField] private float bobbingHeight = 0.2f;      // Hafif yukarı-aşağı dalgalanma
        [SerializeField] private float tiltAngle = 8f;            // Lavda yüzerken yana yatma açısı

        [Header("2. Submerge / Pop-Up Settings (Yerin Altına Girip Çıkma)")]
        [SerializeField] private float submergeDepth = 3.0f;      // Yerin ne kadar altına gireceği
        [SerializeField] private float submergeSpeed = 2.5f;      // Çıkma/inme hızı
        [SerializeField] private float topPauseDuration = 1.2f;   // Yukarıda bekleme süresi
        [SerializeField] private float bottomPauseDuration = 1.0f;// Aşağıda bekleme süresi

        [Header("3. Spinning Settings (Dönme)")]
        [SerializeField] private Vector3 rotationAxis = new Vector3(0f, 90f, 0f);

        [Header("4. Custom Sine Settings")]
        [SerializeField] private Vector3 sineAmplitude = new Vector3(2f, 0.5f, 0f);
        [SerializeField] private Vector3 sineFrequency = new Vector3(1.5f, 2f, 0f);

        private Vector3 startLocalPosition;
        private Quaternion startLocalRotation;
        private float submergeTimer;
        private bool isEmerging = true;

        private void Start()
        {
            startLocalPosition = transform.localPosition;
            startLocalRotation = transform.localRotation;
        }

        private void Update()
        {
            switch (animType)
            {
                case ObstacleAnimType.LavaFloat:
                    AnimateLavaFloat();
                    break;

                case ObstacleAnimType.SubmergeEmerge:
                    AnimateSubmergeEmerge();
                    break;

                case ObstacleAnimType.SpinningRotator:
                    AnimateSpinning();
                    break;

                case ObstacleAnimType.SineWaveCustom:
                    AnimateCustomSine();
                    break;
            }
        }

        private void AnimateLavaFloat()
        {
            float time = Time.time * sideMoveSpeed;
            float offsetX = Mathf.Sin(time) * sideMoveDistance;
            float offsetY = Mathf.Cos(time * 2f) * bobbingHeight;

            transform.localPosition = startLocalPosition + new Vector3(offsetX, offsetY, 0f);

            // Floating tilt roll
            float tilt = Mathf.Sin(time) * tiltAngle;
            transform.localRotation = startLocalRotation * Quaternion.Euler(0f, 0f, tilt);
        }

        private void AnimateSubmergeEmerge()
        {
            submergeTimer += Time.deltaTime * submergeSpeed;

            // Ping-pong bounce calculation between submerged depth and top position
            float pingPong = Mathf.PingPong(submergeTimer, 1.0f);

            // Smoothstep curve for natural emergence/submergence
            float smoothPingPong = Mathf.SmoothStep(0f, 1f, pingPong);

            Vector3 submergedPos = startLocalPosition - new Vector3(0f, submergeDepth, 0f);
            transform.localPosition = Vector3.Lerp(submergedPos, startLocalPosition, smoothPingPong);
        }

        private void AnimateSpinning()
        {
            transform.Rotate(rotationAxis * Time.deltaTime, Space.Self);
        }

        private void AnimateCustomSine()
        {
            float offsetX = Mathf.Sin(Time.time * sineFrequency.x) * sineAmplitude.x;
            float offsetY = Mathf.Sin(Time.time * sineFrequency.y) * sineAmplitude.y;
            float offsetZ = Mathf.Sin(Time.time * sineFrequency.z) * sineAmplitude.z;

            transform.localPosition = startLocalPosition + new Vector3(offsetX, offsetY, offsetZ);
        }
    }
}
