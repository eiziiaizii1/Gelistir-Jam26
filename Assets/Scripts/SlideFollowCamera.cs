using UnityEngine;

/// <summary>
/// Third-person chase camera for the ice slide. Sits behind the character along its
/// slide direction rather than its transform forward, so the framing stays locked to
/// the downhill line instead of swinging around when the block drifts sideways.
/// </summary>
[RequireComponent(typeof(Camera))]
public class SlideFollowCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private IceSlideController controller;

    [Header("Offset")]
    [SerializeField] private float distance = 9f;
    [SerializeField] private float height = 4f;
    [SerializeField] private float lookAheadDistance = 6f;
    [SerializeField] private float lookHeightOffset = 1.2f;

    [Header("Smoothing")]
    [SerializeField] private float positionSmoothTime = 0.18f;
    [SerializeField] private float directionSmoothing = 4f;
    [SerializeField] private float rotationSmoothing = 8f;

    [Header("Speed feel")]
    [SerializeField] private float speedPullback = 0.06f;
    [SerializeField] private float maxPullback = 4f;

    private Vector3 followDirection = Vector3.forward;
    private Vector3 velocitySmoothing;

    private void Awake()
    {
        if (target != null && controller == null)
            controller = target.GetComponent<IceSlideController>();

        if (controller != null)
            followDirection = controller.SlideDirection;
    }

    private void Start()
    {
        if (target != null)
            SnapToTarget();
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 desiredDirection = controller != null ? controller.SlideDirection : followDirection;
        desiredDirection.y = Mathf.Min(desiredDirection.y, 0f);
        if (desiredDirection.sqrMagnitude < 0.0001f)
            desiredDirection = followDirection;

        followDirection = Vector3.Slerp(
            followDirection,
            desiredDirection.normalized,
            directionSmoothing * Time.deltaTime);

        float speed = controller != null ? controller.Speed : 0f;
        float pullback = Mathf.Min(speed * speedPullback, maxPullback);

        Vector3 desiredPosition = DesiredPosition(distance + pullback);

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref velocitySmoothing,
            positionSmoothTime);

        Vector3 lookPoint = target.position
                            + followDirection * lookAheadDistance
                            + Vector3.up * lookHeightOffset;

        Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            rotationSmoothing * Time.deltaTime);
    }

    private Vector3 DesiredPosition(float backDistance)
    {
        return target.position - followDirection * backDistance + Vector3.up * height;
    }

    /// <summary>Jump straight to the framed position, skipping the smoothing.</summary>
    public void SnapToTarget()
    {
        if (target == null)
            return;

        if (controller != null)
        {
            Vector3 direction = controller.SlideDirection;
            direction.y = Mathf.Min(direction.y, 0f);
            if (direction.sqrMagnitude > 0.0001f)
                followDirection = direction.normalized;
        }

        velocitySmoothing = Vector3.zero;
        transform.position = DesiredPosition(distance);
        transform.LookAt(target.position + Vector3.up * lookHeightOffset, Vector3.up);
    }
}
