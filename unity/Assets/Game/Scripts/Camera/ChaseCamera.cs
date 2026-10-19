using UnityEngine;

namespace Studio.Cameras
{
    /// <summary>
    /// Câmera chase para condução livre (NEON DISTRICT): posição amortecida
    /// atrás do veículo com lag de yaw (realça o drift) e lookAt sobre o capô.
    /// </summary>
    public class ChaseCamera : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _offset = new Vector3(0f, 3.6f, -8f);
        [SerializeField] private float _positionLerp = 5.5f;
        [SerializeField] private float _yawLerp = 4.5f;
        [SerializeField] private float _lookHeight = 1.1f;

        private float _yaw;
        private Vector3 _vel;

        public void SetTarget(Transform target)
        {
            _target = target;
            if (_target != null) _yaw = _target.eulerAngles.y;
        }

        private void Start()
        {
            if (_target != null) _yaw = _target.eulerAngles.y;
        }

        private void LateUpdate()
        {
            if (_target == null) return;
            float dt = Time.deltaTime;

            _yaw = Mathf.LerpAngle(_yaw, _target.eulerAngles.y, _yawLerp * dt);

            var yawRot = Quaternion.Euler(0f, _yaw, 0f);
            var desired = _target.position + yawRot * new Vector3(_offset.x, 0f, _offset.z) + Vector3.up * _offset.y;

            // Anti-clip: spherecast do pivô ao ponto desejado — aproxima a câmera se bloqueada
            var pivot = _target.position + Vector3.up * _lookHeight;
            var dir = desired - pivot;
            float dist = dir.magnitude;
            if (dist > 0.01f && Physics.SphereCast(pivot, 0.25f, dir.normalized, out var hit, dist, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(_target))
                    desired = pivot + dir.normalized * Mathf.Max(0.4f, hit.distance - 0.25f);
            }
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _vel, 1f / _positionLerp);

            // Shake pós-explosão: consome impulsos do bus (Gameplay→Cameras
            // sem dependência cíclica) e decai suavemente
            _shake = Mathf.Max(_shake, Studio.Foundation.CameraShakeBus.Consume() * 0.6f);
            if (_shake > 0.001f)
            {
                transform.position += new Vector3(
                    (Mathf.PerlinNoise(Time.time * 30f, 0f) - 0.5f) * _shake,
                    (Mathf.PerlinNoise(0f, Time.time * 30f) - 0.5f) * _shake, 0f);
                _shake = Mathf.Lerp(_shake, 0f, dt * 5f);
            }

            var lookAt = _target.position + Vector3.up * _lookHeight;
            transform.rotation = Quaternion.LookRotation(lookAt - transform.position, Vector3.up);
        }

        private float _shake;
    }
}
