using UnityEngine;

namespace IceEscape
{
    public class SpeedBoostPad : MonoBehaviour
    {
        [Header("Boost Settings")]
        [SerializeField] private float boostForce = 25f;
        [SerializeField] private Vector3 boostDirection = Vector3.forward;
        [SerializeField] private bool usePadForward = true;

        private void OnTriggerEnter(Collider other)
        {
            Rigidbody rb = other.attachedRigidbody;
            if (rb != null)
            {
                Vector3 dir = usePadForward ? transform.forward : boostDirection;
                rb.AddForce(dir.normalized * boostForce, ForceMode.Impulse);
            }
        }
    }
}
