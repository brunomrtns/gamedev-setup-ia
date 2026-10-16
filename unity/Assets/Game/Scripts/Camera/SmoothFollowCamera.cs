using UnityEngine;

namespace Studio.Cameras
{
    public class SmoothFollowCamera : MonoBehaviour
    {
        [Header("Target Tracking")]
        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _offset = new Vector3(0f, 3.2f, -6.5f);
        [SerializeField] private float _smoothSpeedX = 8.0f;
        [SerializeField] private float _smoothSpeedY = 10.0f;
        [SerializeField] private float _pitchAngle = 18.0f;

        private float _currentX;
        private float _currentY;

        public void SetTarget(Transform target)
        {
            _target = target;
            if (_target != null)
            {
                _currentX = _target.position.x;
                _currentY = _target.position.y;
            }
        }

        private void Start()
        {
            if (_target != null)
            {
                _currentX = _target.position.x;
                _currentY = _target.position.y;
            }
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            float dt = Time.deltaTime;

            // Damping suave nos eixos laterais e verticais
            _currentX = Mathf.Lerp(_currentX, _target.position.x, _smoothSpeedX * dt);
            _currentY = Mathf.Lerp(_currentY, _target.position.y, _smoothSpeedY * dt);

            // Z acompanha diretamente para manter a velocidade do corredor sem atraso
            float targetZ = _target.position.z + _offset.z;
            float targetX = _currentX + _offset.x;
            float targetY = _currentY + _offset.y;

            transform.position = new Vector3(targetX, targetY, targetZ);
            transform.rotation = Quaternion.Euler(_pitchAngle, 0f, 0f);
        }
    }
}
