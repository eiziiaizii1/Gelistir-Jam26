using UnityEngine;

namespace IceEscape
{
    /// <summary>
    /// Throwaway particle burst for obstacle hits. Built in code rather than shipped as a
    /// prefab so every hazard variant gets the same puff without each one needing a
    /// particle child wired up and kept in sync.
    /// </summary>
    public static class ObstacleImpactBurst
    {
        public static void Spawn(Vector3 position, Color color, float strength)
        {
            strength = Mathf.Clamp01(strength);
            if (strength <= 0.05f)
                return;

            GameObject obj = new GameObject("ObstacleImpactBurst");
            obj.transform.position = position;

            ParticleSystem ps = obj.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = ps.main;
            main.startColor = color;
            main.startSize = Mathf.Lerp(0.2f, 0.5f, strength);
            main.startLifetime = 0.6f;
            main.startSpeed = Mathf.Lerp(3f, 8f, strength);
            main.stopAction = ParticleSystemStopAction.Destroy;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, Mathf.RoundToInt(Mathf.Lerp(10f, 35f, strength))) });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.4f;

            ParticleSystemRenderer renderer = obj.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader != null)
                {
                    Material material = new Material(shader);
                    material.color = color;
                    renderer.material = material;
                }
            }

            Object.Destroy(obj, 2f);
        }
    }
}
