using UnityEngine;

namespace IceEscape
{
    /// <summary>
    /// The giant hand that swats the ice block sideways on a horizontal mouse swipe.
    ///
    /// Purely cosmetic: it never touches the Rigidbody. It hangs off IceSlideController's
    /// Slapped event, which fires inside FixedUpdate on the exact frame the lateral impulse
    /// is applied, so the swing and the shove are the same event by construction rather than
    /// two timelines someone has to keep in step.
    ///
    /// The hand comes from the side the force comes from: a swipe that shoves the block
    /// right is drawn as a hand entering from the LEFT and sweeping right through it. Doing
    /// it the other way round would show a hand pushing the block toward itself.
    ///
    /// Everything is parented to the player, because at 40 m/s a hand placed in world space
    /// is metres behind the block by the next frame.
    /// </summary>
    public class SlapHand : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private IceSlideController controller;
        [Tooltip("The hand to swing. Point this at Assets/Prefabs/SlapHand.prefab, or your " +
                 "own model. Its root takes uniform strength scaling, so put the shape on a " +
                 "child and keep the root at scale 1. Left empty, a single-slab placeholder " +
                 "is built in code.")]
        [SerializeField] private GameObject handPrefab;

        [Header("Swipe strength")]
        [Tooltip("Impulse magnitude treated as the weakest visible slap.")]
        [SerializeField] private float minImpulse = 3f;
        [Tooltip("Impulse magnitude that drives a full-strength swing.")]
        [SerializeField] private float maxImpulse = 20f;

        [Header("Geometry")]
        [Tooltip("How far out to the side the hand starts, at full strength.")]
        [SerializeField] private float startDistance = 6f;
        [Tooltip("Distance from the block's centre counted as contact. Must clear the " +
                 "block's radius plus half the hand's thickness, or the hand ends up inside " +
                 "the cube at the moment of the slap.")]
        [SerializeField] private float contactDistance = 1.5f;
        [Tooltip("How far past contact the hand drives before pulling back.")]
        [SerializeField] private float followThrough = 0.5f;
        [SerializeField] private float heightOffset = 0.45f;
        [Tooltip("Degrees the hand rotates through as it sweeps, for a swing arc rather " +
                 "than a flat slide.")]
        [SerializeField] private float swingArc = 55f;

        [Header("Timing")]
        [Tooltip("Approach time from spawn to contact. Small on purpose: the impulse has " +
                 "already been applied, so a long wind-up would read as the hand arriving " +
                 "late for a hit that already happened.")]
        [SerializeField] private float approachTime = 0.045f;
        [SerializeField] private float followTime = 0.07f;
        [SerializeField] private float retractTime = 0.16f;

        [Header("Contact feedback")]
        [SerializeField] private Color impactColor = new Color(0.75f, 0.95f, 1f);
        [Tooltip("Extra shake on contact. Off by default: SlideImpactShake already kicks the " +
                 "camera on the same slap, and a second impulse 45ms behind the first read as " +
                 "one muddy double-bump rather than a crisper hit. The burst and the whoosh " +
                 "carry the contact instead.")]
        [SerializeField] private float contactShake = 0f;
        [SerializeField] private bool playAudio = true;

        [Header("Scale")]
        [Tooltip("Uniform scale applied to the hand root at the weakest visible swipe.")]
        [SerializeField] private float minHandScale = 1.3f;
        [Tooltip("Uniform scale at a full-strength swipe. Sized to read clearly against the " +
                 "block, which is about 0.9 across.")]
        [SerializeField] private float maxHandScale = 2.2f;

        private Transform hand;
        private Renderer[] handRenderers;

        private bool swinging;
        private float timer;
        private float strength;
        private Vector3 pushAxis = Vector3.right;
        private bool contactFired;

        private void Awake()
        {
            if (controller == null)
                controller = GetComponentInParent<IceSlideController>();

            BuildHand();
            SetVisible(false);
        }

        private void OnEnable()
        {
            if (controller != null)
                controller.Slapped += OnSlapped;
        }

        private void OnDisable()
        {
            if (controller != null)
                controller.Slapped -= OnSlapped;
        }

        private void OnSlapped(Vector3 impulse)
        {
            if (hand == null || controller == null)
                return;

            Vector3 across = controller.SlopeRight;
            float along = Vector3.Dot(impulse, across);
            if (Mathf.Abs(along) < 0.0001f)
                return;

            pushAxis = across * Mathf.Sign(along);
            strength = Mathf.Clamp01(Mathf.InverseLerp(minImpulse, maxImpulse, impulse.magnitude));

            // Restart rather than queue: a second swipe mid-swing should show the new slap,
            // not finish playing the old one.
            swinging = true;
            contactFired = false;
            timer = 0f;

            SetVisible(true);
            Place(0f);
        }

        private void LateUpdate()
        {
            if (!swinging)
                return;

            timer += Time.deltaTime;

            float total = approachTime + followTime + retractTime;
            if (timer >= total)
            {
                swinging = false;
                SetVisible(false);
                return;
            }

            if (!contactFired && timer >= approachTime)
            {
                contactFired = true;
                FireContact();
            }

            Place(timer);
        }

        /// <summary>
        /// Positions the hand for a moment in the swing. Distance is measured out along the
        /// side the slap comes from, so the whole animation is one scalar over time.
        /// </summary>
        private void Place(float t)
        {
            float reach = Mathf.Lerp(startDistance * 0.55f, startDistance, strength);
            float contact = contactDistance;
            float overshoot = contact - followThrough;

            float distance;
            float arc;

            if (t < approachTime)
            {
                float k = approachTime > 0f ? t / approachTime : 1f;
                // Ease in: the hand is already moving when it appears and accelerates in.
                distance = Mathf.Lerp(reach, contact, k * k);
                arc = Mathf.Lerp(-swingArc, 0f, k * k);
            }
            else if (t < approachTime + followTime)
            {
                float k = followTime > 0f ? (t - approachTime) / followTime : 1f;
                distance = Mathf.Lerp(contact, overshoot, k);
                arc = Mathf.Lerp(0f, swingArc * 0.4f, k);
            }
            else
            {
                float k = retractTime > 0f ? (t - approachTime - followTime) / retractTime : 1f;
                distance = Mathf.Lerp(overshoot, reach, Mathf.SmoothStep(0f, 1f, k));
                arc = Mathf.Lerp(swingArc * 0.4f, -swingArc, Mathf.SmoothStep(0f, 1f, k));
            }

            Vector3 origin = transform.position;
            Vector3 worldPos = origin - pushAxis * distance + Vector3.up * heightOffset;

            hand.position = worldPos;

            // Palm faces the block, then rolls through the arc around the vertical axis.
            Quaternion facing = Quaternion.LookRotation(pushAxis, Vector3.up);
            hand.rotation = Quaternion.AngleAxis(arc, Vector3.up) * facing;

            float scale = Mathf.Lerp(minHandScale, maxHandScale, strength);
            hand.localScale = Vector3.one * scale;
        }

        private void FireContact()
        {
            Vector3 point = transform.position - pushAxis * (contactDistance * 0.6f) + Vector3.up * heightOffset;

            ObstacleImpactBurst.Spawn(point, impactColor, Mathf.Lerp(0.35f, 1f, strength));

            if (contactShake > 0f)
                PlayerLocator.ShakeCamera(contactShake * Mathf.Max(strength, 0.4f));

            if (playAudio && IceAudioManager.Instance != null)
                IceAudioManager.Instance.PlaySlapWhoosh();
        }

        private void SetVisible(bool visible)
        {
            if (handRenderers == null)
                return;

            foreach (Renderer renderer in handRenderers)
                if (renderer != null)
                    renderer.enabled = visible;
        }

        private void BuildHand()
        {
            if (handPrefab != null)
            {
                hand = Instantiate(handPrefab).transform;
            }
            else
            {
                hand = BuildPlaceholderHand().transform;
            }

            // Not parented to the player: the player's transform is driven by physics with
            // interpolation, and inheriting that would make the hand jitter against the very
            // block it is supposed to be hitting. Positioned from the player instead.
            hand.SetParent(null, true);
            handRenderers = hand.GetComponentsInChildren<Renderer>(true);
        }

        /// <summary>
        /// Last-resort hand when no prefab is assigned: a single slab, one mesh, one
        /// renderer. Deliberately one shape rather than an assembled palm-and-fingers rig,
        /// so swapping in a real model is a one-field change with nothing left to clean up.
        /// </summary>
        private static GameObject BuildPlaceholderHand()
        {
            GameObject root = new GameObject("SlapHand");

            GameObject shape = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shape.name = "HandShape";
            shape.transform.SetParent(root.transform, false);
            shape.transform.localScale = HandShapeScale;
            StripCollider(shape);

            Renderer renderer = shape.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = CreateHandMaterial();

            return root;
        }

        /// <summary>
        /// Proportions of the placeholder slab: wide and tall, thin along the direction of
        /// travel. Lives on the child so the root is free to take uniform strength scaling.
        /// </summary>
        public static Vector3 HandShapeScale => new Vector3(1.7f, 2.1f, 0.36f);

        /// <summary>Public so the prefab is built from these exact values, not a copy of them.</summary>
        public static Material CreateHandMaterial()
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));

            Color skin = new Color(1f, 0.76f, 0.62f);
            material.color = skin;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", skin);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.25f);

            // Strongly self-lit. The scene is a dark pit lit by red lava and a green sky, and
            // an unlit hand disappears into it exactly when the player needs to read it.
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.SetColor("_EmissionColor", new Color(1.1f, 0.55f, 0.4f));

            return material;
        }

        /// <summary>The hand must never collide: the block's physics owns the impulse.</summary>
        private static void StripCollider(GameObject obj)
        {
            Collider collider = obj.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
        }

        private void OnDestroy()
        {
            if (hand != null)
                Destroy(hand.gameObject);
        }
    }
}
