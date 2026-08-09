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

        [Header("3. Ground & Particle FX (Tentacle Giriş/Çıkış Efektleri)")]
        [SerializeField] private bool enableGroundFX = true;
        [SerializeField] private float warningLeadTime = 0.7f;     // Geleceğini belli eden uyarı süresi (sn)
        [SerializeField] private Color particleColor = new Color(1.0f, 0.35f, 0.05f, 0.9f); // Lav / Kıvılcım rengi
        [SerializeField] private Vector3 groundOffset = Vector3.zero;

        [Header("Optional Particle Prefabs (Boş bırakılırsa kodla üretilir)")]
        [SerializeField] private GameObject warningParticlePrefab;
        [SerializeField] private GameObject emergeBurstPrefab;
        [SerializeField] private GameObject baseLoopParticlePrefab;
        [SerializeField] private GameObject submergeBurstPrefab;

        [Header("4. Spinning Settings (Dönme)")]
        [SerializeField] private Vector3 rotationAxis = new Vector3(0f, 90f, 0f);

        [Header("5. Custom Sine Settings")]
        [SerializeField] private Vector3 sineAmplitude = new Vector3(2f, 0.5f, 0f);
        [SerializeField] private Vector3 sineFrequency = new Vector3(1.5f, 2f, 0f);

        private Vector3 startLocalPosition;
        private Quaternion startLocalRotation;
        private float submergeTimer;
        private bool isEmerging = true;

        private enum SubmergeState { Hidden, Warning, Emerging, TopPause, Submerging }
        private SubmergeState currentSubmergeState = SubmergeState.Hidden;

        private ParticleSystem activeWarningInstance;
        private ParticleSystem activeBaseLoopInstance;

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

        private void OnDisable()
        {
            CleanupParticleInstances();
        }

        private void OnDestroy()
        {
            CleanupParticleInstances();
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
        /// Rises, holds at the top, sinks, holds under.
        /// Includes warning FX before eruption, burst FX on emerge/submerge, and base ground FX.
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
            SubmergeState newPhase;

            if (t < riseEnd)
            {
                progress = travelDuration > 0f ? t / travelDuration : 1f;
                newPhase = SubmergeState.Emerging;
            }
            else if (t < topEnd)
            {
                progress = 1f;
                newPhase = SubmergeState.TopPause;
            }
            else if (t < sinkEnd)
            {
                progress = travelDuration > 0f ? 1f - (t - topEnd) / travelDuration : 0f;
                newPhase = SubmergeState.Submerging;
            }
            else
            {
                progress = 0f;
                float timeUntilRise = cycle - t;
                if (timeUntilRise <= warningLeadTime)
                    newPhase = SubmergeState.Warning;
                else
                    newPhase = SubmergeState.Hidden;
            }

            isEmerging = (newPhase == SubmergeState.Emerging || newPhase == SubmergeState.TopPause);

            transform.localPosition = Vector3.Lerp(
                submergedPos, startLocalPosition, Mathf.SmoothStep(0f, 1f, progress));

            if (enableGroundFX)
            {
                UpdateSubmergeGroundFX(newPhase);
            }
        }

        /// <summary>True while the obstacle is on its way up or standing proud of the ground.</summary>
        public bool IsEmerging => isEmerging;

        private Vector3 GetGroundPosition()
        {
            if (transform.parent != null)
                return transform.parent.TransformPoint(startLocalPosition + groundOffset);
            return transform.position + groundOffset;
        }

        private void UpdateSubmergeGroundFX(SubmergeState newPhase)
        {
            if (newPhase == currentSubmergeState)
                return;

            SubmergeState prevPhase = currentSubmergeState;
            currentSubmergeState = newPhase;

            Vector3 basePos = GetGroundPosition();

            // 1. Warning Phase (geleceğini belli eder)
            if (newPhase == SubmergeState.Warning)
            {
                StartWarningFX(basePos);
            }
            else if (prevPhase == SubmergeState.Warning && newPhase != SubmergeState.Warning)
            {
                StopWarningFX();
            }

            // 2. Emerge Burst & Base Loop (çıkış anı)
            if (newPhase == SubmergeState.Emerging && prevPhase != SubmergeState.Emerging)
            {
                TriggerEmergeBurst(basePos);
                StartBaseLoopFX(basePos);
            }

            // 3. Submerge Burst (giriş anı)
            if (newPhase == SubmergeState.Submerging && prevPhase != SubmergeState.Submerging)
            {
                TriggerSubmergeBurst(basePos);
            }

            // 4. Hidden (gömüldü)
            if (newPhase == SubmergeState.Hidden)
            {
                StopBaseLoopFX();
                StopWarningFX();
            }
        }

        private void StartWarningFX(Vector3 basePos)
        {
            if (warningParticlePrefab != null)
            {
                if (activeWarningInstance == null)
                {
                    GameObject obj = Instantiate(warningParticlePrefab, basePos, Quaternion.identity);
                    if (transform.parent != null) obj.transform.SetParent(transform.parent, true);
                    activeWarningInstance = obj.GetComponentInChildren<ParticleSystem>();
                }
                if (activeWarningInstance != null) activeWarningInstance.Play();
            }
            else
            {
                if (activeWarningInstance == null)
                {
                    activeWarningInstance = CreateProceduralParticleSystem("Tentacle_WarningFX", basePos, false);
                    ConfigureWarningEmission(activeWarningInstance);
                }
                activeWarningInstance.Play();
            }
        }

        private void StopWarningFX()
        {
            if (activeWarningInstance != null)
            {
                activeWarningInstance.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void StartBaseLoopFX(Vector3 basePos)
        {
            if (baseLoopParticlePrefab != null)
            {
                if (activeBaseLoopInstance == null)
                {
                    GameObject obj = Instantiate(baseLoopParticlePrefab, basePos, Quaternion.identity);
                    if (transform.parent != null) obj.transform.SetParent(transform.parent, true);
                    activeBaseLoopInstance = obj.GetComponentInChildren<ParticleSystem>();
                }
                if (activeBaseLoopInstance != null) activeBaseLoopInstance.Play();
            }
            else
            {
                if (activeBaseLoopInstance == null)
                {
                    activeBaseLoopInstance = CreateProceduralParticleSystem("Tentacle_BaseLoopFX", basePos, false);
                    ConfigureBaseLoopEmission(activeBaseLoopInstance);
                }
                activeBaseLoopInstance.Play();
            }
        }

        private void StopBaseLoopFX()
        {
            if (activeBaseLoopInstance != null)
            {
                activeBaseLoopInstance.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void TriggerEmergeBurst(Vector3 basePos)
        {
            if (emergeBurstPrefab != null)
            {
                GameObject obj = Instantiate(emergeBurstPrefab, basePos, Quaternion.identity);
                if (transform.parent != null) obj.transform.SetParent(transform.parent, true);
                Destroy(obj, 2.5f);
            }
            else
            {
                ParticleSystem ps = CreateProceduralParticleSystem("Tentacle_EmergeBurst", basePos, true);
                ConfigureBurstEmission(ps, 35, 6f, 0.45f);
                Destroy(ps.gameObject, 2f);
            }
        }

        private void TriggerSubmergeBurst(Vector3 basePos)
        {
            if (submergeBurstPrefab != null)
            {
                GameObject obj = Instantiate(submergeBurstPrefab, basePos, Quaternion.identity);
                if (transform.parent != null) obj.transform.SetParent(transform.parent, true);
                Destroy(obj, 2.5f);
            }
            else
            {
                ParticleSystem ps = CreateProceduralParticleSystem("Tentacle_SubmergeBurst", basePos, true);
                ConfigureBurstEmission(ps, 20, 4f, 0.3f);
                Destroy(ps.gameObject, 1.5f);
            }
        }

        private ParticleSystem CreateProceduralParticleSystem(string name, Vector3 position, bool autoDestroy)
        {
            GameObject pObj = new GameObject(name);
            pObj.transform.position = position;
            if (transform.parent != null)
                pObj.transform.SetParent(transform.parent, true);

            ParticleSystem ps = pObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startColor = particleColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            if (autoDestroy)
            {
                main.stopAction = ParticleSystemStopAction.Destroy;
            }

            ParticleSystemRenderer psr = pObj.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    Material mat = new Material(shader);
                    mat.color = particleColor;
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", particleColor * 1.5f);
                    }
                    psr.sharedMaterial = mat;
                }
            }

            return ps;
        }

        private void ConfigureWarningEmission(ParticleSystem ps)
        {
            var main = ps.main;
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.0f);
            main.loop = true;

            var emission = ps.emission;
            emission.rateOverTime = 22f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.65f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
        }

        private void ConfigureBaseLoopEmission(ParticleSystem ps)
        {
            var main = ps.main;
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.loop = true;

            var emission = ps.emission;
            emission.rateOverTime = 14f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.8f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
        }

        private void ConfigureBurstEmission(ParticleSystem ps, int count, float speed, float size)
        {
            var main = ps.main;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.loop = false;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.5f;
        }

        private void CleanupParticleInstances()
        {
            if (activeWarningInstance != null && activeWarningInstance.gameObject != null)
            {
                Destroy(activeWarningInstance.gameObject);
            }
            if (activeBaseLoopInstance != null && activeBaseLoopInstance.gameObject != null)
            {
                Destroy(activeBaseLoopInstance.gameObject);
            }
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
