using UnityEngine;

namespace IceEscape
{
    /// <summary>
    /// Positions the end-point volume along the slope so the run length can be tuned from the
    /// inspector instead of by hand-placing a transform. Runs in edit mode, so changing
    /// Finish Distance moves the finish object in the Scene view immediately.
    /// </summary>
    [ExecuteAlways]
    public class FinishLinePlacer : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The end-point volume to position (the FinishZone cube).")]
        [SerializeField] private Transform finishZone;

        [Tooltip("Defines the slope origin and direction. Normally the Ground object.")]
        [SerializeField] private Transform trackRoot;

        [Header("Placement")]
        [Tooltip("Distance along the slope from the track root at which the run ends.")]
        [SerializeField] private float finishDistance = 2242f;

        [Tooltip("Height above the track surface for the volume's center, so it covers the slope.")]
        [SerializeField] private float heightOffset = 78f;

        [Tooltip("Measure Finish Distance from the player's start position instead of the track " +
                 "root, so the value reads as the actual length of the run.")]
        [SerializeField] private bool measureFromPlayerStart;

        [Tooltip("Player transform used when measuring from the player's start position.")]
        [SerializeField] private Transform playerStart;

        /// <summary>The volume this placer drives, for anything that needs the end point.</summary>
        public Transform FinishZone { get { return finishZone; } }

        /// <summary>Distance along the slope at which the run ends, in the configured mode.</summary>
        public float FinishDistance
        {
            get { return finishDistance; }
            set { finishDistance = value; ApplyPlacement(); }
        }

        private void OnEnable()
        {
            ApplyPlacement();
        }

        private void OnValidate()
        {
            ApplyPlacement();
        }

        /// <summary>Moves the finish volume to the configured distance along the slope.</summary>
        public void ApplyPlacement()
        {
            if (finishZone == null || trackRoot == null)
                return;

            float distance = finishDistance;

            if (measureFromPlayerStart && playerStart != null)
            {
                Vector3 slopeForward = trackRoot.rotation * Vector3.forward;
                float startDistance = Vector3.Dot(playerStart.position - trackRoot.position, slopeForward);
                distance += startDistance;
            }

            finishZone.rotation = trackRoot.rotation;
            finishZone.position = trackRoot.position +
                                  (trackRoot.rotation * new Vector3(0f, heightOffset, distance));
        }
    }
}
