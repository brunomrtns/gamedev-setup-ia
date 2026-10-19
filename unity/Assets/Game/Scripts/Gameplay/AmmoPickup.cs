using UnityEngine;

namespace Studio.Gameplay
{
    /// <summary>
    /// Pickup de munição neon — gira/pulsa, coletado por proximidade a pé,
    /// respawna após 20s. Builder instancia com visual emissivo.
    /// </summary>
    public class AmmoPickup : MonoBehaviour
    {
        [SerializeField] private int _ammoAmount = 30;
        [SerializeField] private float _respawnTime = 20f;
        [SerializeField] private float _spinSpeed = 90f;

        private float _timer;
        private bool _available = true;
        private Ak47Weapon _weapon;

        private void Update()
        {
            transform.Rotate(0f, _spinSpeed * Time.deltaTime, 0f);
            float pulse = 0.9f + 0.1f * Mathf.Sin(Time.time * 4f);
            transform.localScale = Vector3.one * pulse;

            if (!_available)
            {
                _timer -= Time.deltaTime;
                if (_timer <= 0f) SetVisible(true);
                return;
            }

            var mgr = NeonDistrictModeManager.Instance;
            if (mgr == null || mgr.Current != NeonDistrictModeManager.Mode.OnFoot) return;
            if (_weapon == null) _weapon = FindAnyObjectByType<Ak47Weapon>();
            if (_weapon == null) return;

            var motor = FindAnyObjectByType<OnFootMotor>();
            if (motor != null && Vector3.Distance(motor.transform.position, transform.position) < 1.4f)
            {
                _weapon.AddAmmo(_ammoAmount);
                Foundation.ProceduralAudioService.Instance?.PlayPickup();
                SetVisible(false);
                _timer = _respawnTime;
            }
        }

        private void SetVisible(bool on)
        {
            _available = on;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = on;
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = on;
        }
    }
}
