using UnityEngine;

namespace IceEscape
{
    public class IceSquashAndStretch : MonoBehaviour
    {
        [Header("Juice Settings")]
        [SerializeField] private float elasticity = 12f;
        [SerializeField] private float damping = 6f;

        private Vector3 targetScale = Vector3.one;
        private Vector3 currentScaleOffset = Vector3.zero;
        private Vector3 scaleVelocity = Vector3.zero;

        private void Update()
        {
            // Spring elasticity towards zero offset
            currentScaleOffset = Vector3.SmoothDamp(currentScaleOffset, Vector3.zero, ref scaleVelocity, 1f / elasticity, damping);
            transform.localScale = Vector3.one + currentScaleOffset;
        }

        public void ApplySquash(Vector3 squashOffset)
        {
            currentScaleOffset += squashOffset;
        }
    }
}
