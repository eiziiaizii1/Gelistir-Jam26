using UnityEngine;

namespace IceEscape
{
    /// <summary>
    /// A hazard that drops from the sky on a loop, for meteor-style lava rocks.
    ///
    /// Deliberately self-contained rather than spawned by MeteorRainManager: dropping one of
    /// these into the level and moving it is how a designer places a threat at a specific
    /// corner, which the global rain manager cannot express. The two coexist — the manager
    /// still handles ambient rain everywhere else.
    ///
    /// Motion is driven by transform rather than Rigidbody so the fall is exactly repeatable
    /// and a designer can read the timing straight off the inspector.
    /// </summary>
    [RequireComponent(typeof(ObstacleHazard))]
    public class FallingObstacle : MonoBehaviour
    {
        [Header("Drop")]
        [Tooltip("Height above the resting position the rock starts each fall from.")]
        [SerializeField] private float dropHeight = 30f;
        [SerializeField] private float fallSpeed = 34f;
        [Tooltip("Seconds to wait after landing before the next drop. Randomised by " +
                 "respawnJitter so a row of these does not fall in lockstep.")]
        [SerializeField] private float respawnDelay = 3f;
        [SerializeField] private float respawnJitter = 1.5f;
        [Tooltip("Random delay before the first drop, so placed rocks start out of phase.")]
        [SerializeField] private float startJitter = 2f;

        [Header("Telegraph")]
        [Tooltip("Marker laid on the ground under the rock while it falls, so the drop is " +
                 "dodgeable instead of unfair.")]
        [SerializeField] private bool showTargetMarker = true;
        [SerializeField] private Color markerColor = new Color(1f, 0.15f, 0.05f, 0.8f);
        [SerializeField] private LayerMask groundMask = ~0;

        private Vector3 restPosition;
        private float nextDropTime;
        private bool falling;
        private Transform marker;
        private Collider hazardCollider;

        private void Awake()
        {
            restPosition = transform.position;
            hazardCollider = GetComponent<Collider>();
        }

        private void OnEnable()
        {
            nextDropTime = Time.time + Random.Range(0f, startJitter);
            ParkAboveStart();
        }

        private void OnDisable()
        {
            DestroyMarker();
        }

        private void ParkAboveStart()
        {
            falling = false;
            transform.position = restPosition + Vector3.up * dropHeight;
            SetVisible(false);
        }

        private void Update()
        {
            if (!falling)
            {
                if (Time.time >= nextDropTime)
                    BeginFall();

                return;
            }

            transform.position += Vector3.down * (fallSpeed * Time.deltaTime);

            if (transform.position.y <= restPosition.y)
                Land();
        }

        private void BeginFall()
        {
            falling = true;
            transform.position = restPosition + Vector3.up * dropHeight;
            SetVisible(true);

            if (showTargetMarker)
                CreateMarker();
        }

        private void Land()
        {
            transform.position = restPosition;
            falling = false;
            nextDropTime = Time.time + respawnDelay + Random.Range(0f, respawnJitter);

            ObstacleImpactBurst.Spawn(restPosition, markerColor, 1f);

            if (IceAudioManager.Instance != null)
                IceAudioManager.Instance.PlayExplosion();

            PlayerLocator.ShakeCamera(0.25f);

            DestroyMarker();
            ParkAboveStart();
        }

        /// <summary>
        /// Hidden between drops rather than deactivated: disabling the object would stop
        /// this Update and the rock would never come back.
        /// </summary>
        private void SetVisible(bool visible)
        {
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
                renderer.enabled = visible;

            if (hazardCollider != null)
                hazardCollider.enabled = visible;
        }

        private void CreateMarker()
        {
            DestroyMarker();

            Vector3 groundPoint = restPosition;
            if (Physics.Raycast(restPosition + Vector3.up * 2f, Vector3.down,
                                out RaycastHit hit, dropHeight + 10f, groundMask,
                                QueryTriggerInteraction.Ignore))
            {
                groundPoint = hit.point;
            }

            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            obj.name = "FallingObstacleMarker";

            Collider markerCollider = obj.GetComponent<Collider>();
            if (markerCollider != null)
                Object.Destroy(markerCollider);

            obj.transform.position = groundPoint + Vector3.up * 0.06f;
            obj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            obj.transform.localScale = Vector3.one * (transform.localScale.x * 2.2f);

            Renderer renderer = obj.GetComponent<Renderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (renderer != null && shader != null)
            {
                Material material = new Material(shader);
                material.color = markerColor;
                renderer.material = material;
            }

            marker = obj.transform;
        }

        private void DestroyMarker()
        {
            if (marker != null)
                Object.Destroy(marker.gameObject);

            marker = null;
        }
    }
}
