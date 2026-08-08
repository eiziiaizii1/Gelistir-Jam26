using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

namespace IceEscape
{
    public class MeteorRainManager : MonoBehaviour
    {
        [Header("Mixed Meteor Rain Settings")]
        [SerializeField] private bool enableMeteorRain = true;
        [SerializeField] private float minSpawnInterval = 1.2f;   // Dynamic random interval (min)
        [SerializeField] private float maxSpawnInterval = 3.0f;   // Dynamic random interval (max)

        [Header("Meteor Targeting Odds")]
        [Range(0f, 1f)] [SerializeField] private float playerPathTargetChance = 0.5f; // 50% ahead on player path
        [Range(0f, 1f)] [SerializeField] private float randomSurroundingChance = 0.3f; // 30% random around track
        // Remaining 20% = Direct Homing Strike

        [Header("Layer Settings")]
        [SerializeField] private LayerMask groundLayer = ~0;

        private Transform playerTransform;
        private float nextSpawnTime;

        private void Start()
        {
            FindPlayer();
            nextSpawnTime = Time.time + 0.5f;
        }

        private void FindPlayer()
        {
            playerTransform = PlayerLocator.FindPlayerTransform();
        }

        private void Update()
        {
            if (playerTransform == null)
            {
                FindPlayer();
            }

            // Test Key: Press 'M' to manually trigger meteor drop
            if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
            {
                SpawnMeteor();
            }

            if (!enableMeteorRain) return;

            if (Time.time >= nextSpawnTime)
            {
                nextSpawnTime = Time.time + Random.Range(minSpawnInterval, maxSpawnInterval);
                SpawnMixedMeteor();
            }
        }

        [ContextMenu("Test Spawn Meteor")]
        public void SpawnMeteor()
        {
            SpawnMixedMeteor();
        }

        private void SpawnMixedMeteor()
        {
            if (playerTransform == null) FindPlayer();

            Vector3 targetPos = Vector3.zero;
            float meteorSize = Random.Range(1.8f, 2.8f);
            float fallSpeed = Random.Range(32f, 45f);

            float roll = Random.value;

            if (playerTransform != null)
            {
                Vector3 forwardDir = playerTransform.forward;
                Rigidbody rb = playerTransform.GetComponent<Rigidbody>();
                if (rb != null && rb.linearVelocity.sqrMagnitude > 0.5f)
                {
                    Vector3 velDir = rb.linearVelocity;
                    velDir.y = 0f;
                    if (velDir.sqrMagnitude > 0.5f) forwardDir = velDir.normalized;
                }

                if (roll < playerPathTargetChance)
                {
                    // 1. Direct Ahead on Player Path (Dodging required!)
                    float distanceAhead = Random.Range(14f, 22f);
                    float sideOffset = Random.Range(-2.2f, 2.2f);
                    targetPos = playerTransform.position + forwardDir * distanceAhead + Vector3.right * sideOffset;
                }
                else if (roll < (playerPathTargetChance + randomSurroundingChance))
                {
                    // 2. Random Surrounding Track Cluster
                    float distanceAhead = Random.Range(8f, 30f);
                    float sideOffset = Random.Range(-6.0f, 6.0f);
                    targetPos = playerTransform.position + forwardDir * distanceAhead + Vector3.right * sideOffset;
                }
                else
                {
                    // 3. Direct Homing Strike (Urgent Homing Drop!)
                    float distanceAhead = Random.Range(6f, 12f);
                    targetPos = playerTransform.position + forwardDir * distanceAhead;
                    fallSpeed = 48f; // Fast drop
                    meteorSize = 2.4f;
                }
            }
            else
            {
                targetPos = new Vector3(Random.Range(-5f, 5f), 5f, Random.Range(-5f, 5f));
            }

            // Raycast down onto slope surface
            RaycastHit hit;
            if (Physics.Raycast(targetPos + Vector3.up * 20f, Vector3.down, out hit, 50f, groundLayer))
            {
                targetPos = hit.point;
            }

            StartCoroutine(DoMeteorDrop(targetPos, meteorSize, fallSpeed));
        }

        private IEnumerator DoMeteorDrop(Vector3 targetPos, float meteorScale, float fallSpeed)
        {
            // 1. Target Warning Circle on Ramp Floor
            GameObject targetObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            targetObj.name = "MeteorTargetCircle";
            targetObj.transform.position = targetPos + Vector3.up * 0.05f;
            targetObj.transform.localScale = new Vector3(meteorScale * 1.5f, 0.04f, meteorScale * 1.5f);

            Collider col = targetObj.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            MeshRenderer mr = targetObj.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(1.0f, 0.1f, 0.05f, 0.85f);
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(1.0f, 0.3f, 0.0f));
                }
                mr.sharedMaterial = mat;
            }

            // 2. Fiery Meteor Sphere falling from sky
            GameObject meteor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            meteor.name = "FallingMeteorBody";
            Vector3 skyStartPos = targetPos + Vector3.up * 35f;
            meteor.transform.position = skyStartPos;
            meteor.transform.localScale = Vector3.one * meteorScale;

            Collider meteorCol = meteor.GetComponent<Collider>();
            if (meteorCol != null) meteorCol.enabled = false;

            MeshRenderer meteorMr = meteor.GetComponent<MeshRenderer>();
            if (meteorMr != null)
            {
                Material mMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mMat.color = new Color(1.0f, 0.35f, 0.05f);
                if (mMat.HasProperty("_EmissionColor"))
                {
                    mMat.EnableKeyword("_EMISSION");
                    mMat.SetColor("_EmissionColor", new Color(1.0f, 0.4f, 0.0f));
                }
                meteorMr.sharedMaterial = mMat;
            }

            // Meteor Flame Trail Particles
            GameObject pObj = new GameObject("MeteorFlameTrail");
            pObj.transform.SetParent(meteor.transform, false);

            ParticleSystem ps = pObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startColor = new Color(1.0f, 0.4f, 0.05f, 0.8f);
            main.startSize = meteorScale * 0.25f;
            main.startLifetime = 0.5f;
            main.startSpeed = 2f;

            ParticleSystemRenderer psr = pObj.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                Material pMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                pMat.color = new Color(1.0f, 0.4f, 0.05f);
                psr.sharedMaterial = pMat;
            }

            // Fall animation from sky down to targetPos
            float distance = Vector3.Distance(skyStartPos, targetPos);
            float fallDuration = distance / fallSpeed;
            float elapsed = 0f;

            while (elapsed < fallDuration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / fallDuration;

                if (meteor != null)
                {
                    meteor.transform.position = Vector3.Lerp(skyStartPos, targetPos, progress);
                }

                if (targetObj != null)
                {
                    float pulse = 1.0f + Mathf.Sin(elapsed * 25f) * 0.2f;
                    targetObj.transform.localScale = new Vector3(meteorScale * 1.5f * pulse, 0.04f, meteorScale * 1.5f * pulse);
                }

                yield return null;
            }

            if (targetObj != null) Destroy(targetObj);
            if (meteor != null) Destroy(meteor);

            // 3. Impact Boom Explosion & Lava Crater
            CreateMeteorImpact(targetPos, meteorScale);
        }

        private void CreateMeteorImpact(Vector3 pos, float scale)
        {
            if (IceAudioManager.Instance != null) IceAudioManager.Instance.PlayExplosion();

            PlayerLocator.ShakeCamera(0.3f * scale);

            IceGameHUD hud = PlayerLocator.FindHUD();
            if (hud != null) hud.TriggerScreenFlash(new Color(1.0f, 0.25f, 0.05f), 0.35f);

            // Fiery Particle Explosion
            GameObject expObj = new GameObject("MeteorExplosionParticles");
            expObj.transform.position = pos;

            ParticleSystem ps = expObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startColor = new Color(1.0f, 0.35f, 0.05f, 1f);
            main.startSize = 0.4f * scale;
            main.startLifetime = 0.7f;
            main.startSpeed = 6f * scale;

            ParticleSystemRenderer psr = ps.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                Material pMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                pMat.color = new Color(1.0f, 0.35f, 0.05f);
                psr.sharedMaterial = pMat;
            }

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (int)(30 * scale)) });
            Destroy(expObj, 1.2f);

            // Create Lava Hazard Crater on track
            GameObject crater = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crater.name = "MeteorLavaCrater";
            crater.transform.position = pos;
            crater.transform.localScale = new Vector3(1.5f * scale, 0.15f, 1.5f * scale);

            Collider col = crater.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            MeshRenderer lavaMr = crater.GetComponent<MeshRenderer>();
            if (lavaMr != null)
            {
                Material lavaMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                lavaMat.color = new Color(1.0f, 0.2f, 0.0f, 0.9f);
                if (lavaMat.HasProperty("_EmissionColor"))
                {
                    lavaMat.EnableKeyword("_EMISSION");
                    lavaMat.SetColor("_EmissionColor", new Color(1.0f, 0.35f, 0.0f));
                }
                lavaMr.sharedMaterial = lavaMat;
            }

            crater.AddComponent<LavaHazard>();
            Destroy(crater, 7f);
        }
    }
}
