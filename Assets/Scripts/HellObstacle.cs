using UnityEngine;

namespace IceEscape
{
    public class HellObstacle : MonoBehaviour
    {
        [Header("Obstacle Settings")]
        [SerializeField] private float meltDamage = 0.12f;      // Melts 12% ice on collision
        [SerializeField] private float bounceForce = 7f;        // Pushback force on hit
        [SerializeField] private float cameraShakeAmount = 0.35f;// Camera shake intensity

        [Header("Explosion / Destruction Settings")]
        [SerializeField] private bool isDestructible = true;    // Destroys on hit at high speed
        [SerializeField] private float destroyImpactSpeed = 4f;  // Speed required to break obstacle
        [SerializeField] private GameObject explosionPrefab;    // Optional particle prefab

        private void OnCollisionEnter(Collision collision)
        {
            HandleImpact(collision.gameObject, collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position);
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleImpact(other.gameObject, other.transform.position);
        }

        private void HandleImpact(GameObject targetObj, Vector3 hitPoint)
        {
            IcePlayerController player = targetObj.GetComponent<IcePlayerController>();
            if (player == null)
            {
                player = targetObj.GetComponentInParent<IcePlayerController>();
            }

            if (player != null)
            {
                Rigidbody rb = player.GetComponent<Rigidbody>();
                float impactSpeed = rb != null ? rb.linearVelocity.magnitude : 5f;

                // 1. Apply Melt Damage
                player.RestoreIce(-meltDamage);

                // 2. Play Explosion SFX
                if (IceAudioManager.Instance != null)
                {
                    IceAudioManager.Instance.PlayExplosion();
                }

                // 3. Screen Flash & Camera Shake
                IceGameHUD hud = FindFirstObjectByType<IceGameHUD>();
                if (hud != null)
                {
                    hud.TriggerScreenFlash(new Color(1.0f, 0.2f, 0.05f), 0.45f);
                }

                CameraFollow camFollow = FindFirstObjectByType<CameraFollow>();
                if (camFollow != null)
                {
                    camFollow.TriggerShake(cameraShakeAmount);
                }

                // 4. Visual Squash Impact Effect
                IceSquashAndStretch squash = player.GetComponentInChildren<IceSquashAndStretch>();
                if (squash != null)
                {
                    squash.ApplySquash(new Vector3(0.35f, -0.45f, 0.35f));
                }

                // 5. Explosion Effect or Bounce Pushback
                if (isDestructible && impactSpeed >= destroyImpactSpeed)
                {
                    CreateFieryExplosion(hitPoint);
                    Destroy(gameObject);
                }
                else
                {
                    if (rb != null)
                    {
                        Vector3 pushDir = (player.transform.position - transform.position).normalized;
                        pushDir.y = 0.3f;
                        rb.AddForce(pushDir.normalized * bounceForce, ForceMode.Impulse);
                    }
                    CreateImpactParticles(hitPoint);
                }
            }
        }

        private void CreateFieryExplosion(Vector3 point)
        {
            GameObject expObj = new GameObject("ObstacleExplosion");
            expObj.transform.position = point;

            ParticleSystem ps = expObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startColor = new Color(1.0f, 0.4f, 0.05f, 1f);
            main.startSize = 0.45f;
            main.startLifetime = 0.7f;
            main.startSpeed = 7f;

            ParticleSystemRenderer psr = ps.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                Material pMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                pMat.color = new Color(1.0f, 0.35f, 0.05f);
                if (pMat.HasProperty("_EmissionColor"))
                {
                    pMat.EnableKeyword("_EMISSION");
                    pMat.SetColor("_EmissionColor", new Color(1.0f, 0.4f, 0.0f));
                }
                psr.sharedMaterial = pMat;
            }

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 40) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;

            Destroy(expObj, 1.2f);
        }

        private void CreateImpactParticles(Vector3 point)
        {
            GameObject pObj = new GameObject("ObstacleImpactParticles");
            pObj.transform.position = point;

            ParticleSystem ps = pObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startColor = new Color(1.0f, 0.3f, 0.1f, 0.9f);
            main.startSize = 0.3f;
            main.startLifetime = 0.5f;
            main.startSpeed = 4f;

            ParticleSystemRenderer psr = ps.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                Material pMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                pMat.color = new Color(1.0f, 0.3f, 0.1f);
                psr.sharedMaterial = pMat;
            }

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 20) });

            Destroy(pObj, 1f);
        }
    }
}
