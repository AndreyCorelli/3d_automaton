using UnityEngine;

namespace ParticleAutomaton
{
    [AddComponentMenu("Particle Automaton/Orbit Camera")]
    public class OrbitCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float distance      = 150f;
        [SerializeField] private float rotateSpeed   = 3f;
        [SerializeField] private float zoomSpeed     = 0.1f;
        [SerializeField] private float minDistance   = 10f;
        [SerializeField] private float maxDistance   = 500f;

        private float _yaw;
        private float _pitch = 20f;

        private void Start()
        {
            Vector3 pivot = target ? target.position : Vector3.zero;
            Vector3 dir   = transform.position - pivot;
            distance = Mathf.Clamp(dir.magnitude, minDistance, maxDistance);
            _yaw     = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            _pitch   = Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg;
        }

        private void LateUpdate()
        {
            // Right-click drag to orbit.
            if (Input.GetMouseButton(1))
            {
                _yaw   += Input.GetAxis("Mouse X") * rotateSpeed;
                _pitch -= Input.GetAxis("Mouse Y") * rotateSpeed;
                _pitch = Mathf.Clamp(_pitch, -89f, 89f);
            }

            // Scroll wheel to zoom.
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            distance *= 1f - scroll * zoomSpeed * 10f;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);

            Vector3    pivot    = target ? target.position : Vector3.zero;
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.position  = pivot + rotation * new Vector3(0f, 0f, -distance);
            transform.LookAt(pivot);
        }
    }
}
