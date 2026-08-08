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

        /// <summary>
        /// Rises, holds at the top, sinks, holds under. The pauses are the whole point for a
        /// tentacle: a constant ping-pong reads as decoration, while a beat spent fully out
        /// makes it a hazard the player has to time a run past.
        /// </summary>
        private void AnimateSubmergeEmerge()
        {
            Vector3 submergedPos = startLocalPosition - new Vector3(0f, submergeDepth, 0f);
            float travelDuration = submergeSpeed > 0.0001f ? 1f / submergeSpeed : 0f;

            submergeTimer += Time.deltaTime;

            // One full cycle: rise, wait out, sink, wait under.
            float riseEnd = travelDuration;
            float topEnd = riseEnd + topPauseDuration;
            float sinkEnd = topEnd + travelDuration;
            float cycle = sinkEnd + bottomPauseDuration;

            if (cycle <= 0.0001f)
                return;

            float t = Mathf.Repeat(submergeTimer, cycle);

            float progress;
            if (t < riseEnd)
                progress = travelDuration > 0f ? t / travelDuration : 1f;
            else if (t < topEnd)
                progress = 1f;
            else if (t < sinkEnd)
                progress = travelDuration > 0f ? 1f - (t - topEnd) / travelDuration : 0f;
            else
                progress = 0f;

            isEmerging = t < topEnd;

            transform.localPosition = Vector3.Lerp(
                submergedPos, startLocalPosition, Mathf.SmoothStep(0f, 1f, progress));
        }

        /// <summary>True while the obstacle is on its way up or standing proud of the ground.</summary>
        public bool IsEmerging => isEmerging;

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
