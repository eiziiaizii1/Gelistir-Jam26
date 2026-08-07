using UnityEngine;

public class RotatingCube : MonoBehaviour
{
    [SerializeField] private Vector3 rotationSpeed = new Vector3(30f, 45f, 15f);

    private void Update()
    {
        transform.Rotate(rotationSpeed * Time.deltaTime, Space.Self);
    }
}
