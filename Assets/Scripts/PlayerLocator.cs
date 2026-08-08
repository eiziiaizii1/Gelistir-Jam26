using Unity.Cinemachine;
using UnityEngine;

namespace IceEscape
{
    /// <summary>
    /// One place to answer "where is the player" and "how do I shake the camera".
    ///
    /// Scenes differ in which rig runs the camera (CameraFollow, CameraJuice, or a
    /// Cinemachine vcam with an impulse listener), and a character only has to expose
    /// IMeltSource to be understood here. Callers should not have to know which, so they
    /// come through this class instead of hunting for a concrete type.
    /// </summary>
    public static class PlayerLocator
    {
        /// <summary>Impulse velocity per 1.0 of legacy TriggerShake intensity.</summary>
        private const float ImpulseScale = 3f;

        private static IMeltSource cachedMeltSource;
        private static Rigidbody cachedBody;
        private static IceGameHUD cachedHud;

        // Statics survive play-mode restarts when domain reload is disabled, so clear
        // them explicitly or the second run holds references to destroyed objects.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCaches()
        {
            cachedMeltSource = null;
            cachedBody = null;
            cachedHud = null;
        }

        /// <summary>The thing tracking how much ice is left, whichever controller owns it.</summary>
        public static IMeltSource FindMeltSource()
        {
            if (cachedMeltSource is MonoBehaviour cached && cached != null)
                return cachedMeltSource;

            cachedMeltSource = null;

            foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (behaviour is IMeltSource source)
                {
                    cachedMeltSource = source;
                    break;
                }
            }

            return cachedMeltSource;
        }

        /// <summary>The player's Rigidbody, by tag first and melt source second.</summary>
        public static Rigidbody FindPlayerBody()
        {
            if (cachedBody != null)
                return cachedBody;

            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                cachedBody = playerObj.GetComponent<Rigidbody>();

            if (cachedBody == null && FindMeltSource() is MonoBehaviour source && source != null)
                cachedBody = source.GetComponentInParent<Rigidbody>();

            return cachedBody;
        }

        /// <summary>The player's root transform, or null if there is no player yet.</summary>
        public static Transform FindPlayerTransform()
        {
            Rigidbody body = FindPlayerBody();
            return body != null ? body.transform : null;
        }

        /// <summary>
        /// Resolves the melt source for a collider/collision we just hit. Checks the object
        /// itself then its parents, since the visual mesh is usually a child of the body.
        /// </summary>
        public static IMeltSource GetMeltSource(GameObject obj)
        {
            if (obj == null)
                return null;

            IMeltSource source = obj.GetComponent<IMeltSource>();
            return source ?? obj.GetComponentInParent<IMeltSource>();
        }

        public static IceGameHUD FindHUD()
        {
            if (cachedHud == null)
                cachedHud = Object.FindFirstObjectByType<IceGameHUD>();

            return cachedHud;
        }

        /// <summary>
        /// Shakes whatever camera rig this scene uses. Intensity is in the units the old
        /// CameraFollow.TriggerShake took, so existing call sites keep their tuning.
        /// </summary>
        public static void ShakeCamera(float intensity)
        {
            if (intensity <= 0f)
                return;

            CameraFollow follow = Object.FindFirstObjectByType<CameraFollow>();
            if (follow != null)
            {
                follow.TriggerShake(intensity);
                return;
            }

            CameraJuice juice = Object.FindFirstObjectByType<CameraJuice>();
            if (juice != null)
            {
                juice.TriggerShake(intensity);
                return;
            }

            // Cinemachine rig: any vcam with an impulse listener picks this up.
            CinemachineImpulseSource impulse = Object.FindFirstObjectByType<CinemachineImpulseSource>();
            if (impulse == null)
                return;

            // Bias downward so an impact reads as a hit from above rather than a random sway.
            Vector3 direction = Random.onUnitSphere;
            direction.y = -Mathf.Abs(direction.y);
            impulse.GenerateImpulseWithVelocity(direction.normalized * intensity * ImpulseScale);
        }
    }
}
