using UnityEngine;

namespace IceEscape
{
    public class IcePickup : MonoBehaviour
    {
        [Header("Pickup Settings")]
        [SerializeField] private float restoreAmount = 0.25f; // Restores 25% Ice Volume
        [SerializeField] private float rotationSpeed = 90f;
        [SerializeField] private float floatSpeed = 2f;
        [SerializeField] private float floatAmount = 0.2f;

        private Vector3 startPos;

        private void Start()
        {
            startPos = transform.position;
        }

        private void Update()
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
            float newY = startPos.y + Mathf.Sin(Time.time * floatSpeed) * floatAmount;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }

        private void OnTriggerEnter(Collider other)
        {
            IcePlayerController player = other.GetComponent<IcePlayerController>();
            if (player == null)
            {
                player = other.GetComponentInParent<IcePlayerController>();
            }

            if (player != null)
            {
                player.RestoreIce(restoreAmount);

                IceGameHUD hud = FindFirstObjectByType<IceGameHUD>();
                if (hud != null)
                {
                    hud.TriggerScreenFlash(new Color(0.1f, 0.9f, 1.0f), 0.35f);
                }

                CameraFollow camFollow = FindFirstObjectByType<CameraFollow>();
                if (camFollow != null)
                {
                    camFollow.TriggerShake(0.2f);
                }

                CreateCollectParticles();
                Destroy(gameObject);
            }
        }

        private void CreateCollectParticles()
        {
            GameObject pObj = new GameObject("IceCollectParticles");
            pObj.transform.position = transform.position;

            ParticleSystem ps = pObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startColor = new Color(0.2f, 0.9f, 1.0f, 0.9f);
            main.startSize = 0.35f;
            main.startLifetime = 0.6f;
            main.startSpeed = 3f;

            // Assign URP Unlit / Default Particle material to avoid purple squares!
            ParticleSystemRenderer psr = ps.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                Material pMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                if (pMat.shader == null || pMat.shader.name == "Hidden/InternalErrorShader")
                {
                    pMat = new Material(Shader.Find("Particles/Standard Unlit"));
                }
                pMat.color = new Color(0.2f, 0.9f, 1.0f, 0.9f);
                psr.sharedMaterial = pMat;
            }

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 25) });

            Destroy(pObj, 1f);
        }
    }
}
