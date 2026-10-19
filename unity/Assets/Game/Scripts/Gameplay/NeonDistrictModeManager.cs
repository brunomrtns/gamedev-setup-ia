using Studio.Input;
using UnityEngine;

namespace Studio.Gameplay
{
    /// <summary>
    /// Gerencia os modos Driving/OnFoot do NEON DISTRICT v2.
    /// E sai do carro (cápsula na lateral esquerda); aproximar da porta + E entra.
    /// Câmera blend chase↔FPS em ~0.35s. Contador de latas (CANS).
    /// </summary>
    public class NeonDistrictModeManager : MonoBehaviour
    {
        public enum Mode { Driving, OnFoot }

        [SerializeField] private PlayerCarController _car;
        [SerializeField] private OnFootMotor _motor;
        [SerializeField] private Camera _mainCam;
        [SerializeField] private Camera _fpsCam;
        [SerializeField] private float _enterDistance = 3.0f;
        [SerializeField] private float _blendTime = 0.35f;

        public static NeonDistrictModeManager Instance { get; private set; }

        public Mode Current { get; private set; } = Mode.Driving;
        public int CansHit { get; private set; }
        public int CansTotal { get; set; }
        public event System.Action<Mode> OnModeChanged;
        public event System.Action<int, int> OnCansChanged;

        private float _blendT = 1f;
        private Vector3 _blendFromPos;
        private Quaternion _blendFromRot;

        private void Awake() { Instance = this; }

        private void Update()
        {
            var input = DriveInputAdapter.Instance;
            if (input != null && input.ConsumeEnterExit())
            {
                if (Current == Mode.Driving) ExitCar();
                else TryEnterCar();
            }

            if (Current == Mode.OnFoot) BlendCameraStep();
        }

        /// <summary>Sai do carro: cápsula na lateral esquerda, olha para frente.</summary>
        public void ExitCar()
        {
            if (Current != Mode.Driving || _car == null || _motor == null) return;
            Vector3 exitPos = _car.transform.position - _car.transform.right * 1.6f + Vector3.up * 0.4f;
            _car.ResetToPose(_car.transform.position, _car.transform.rotation); // para o carro
            _motor.transform.position = exitPos;
            _motor.gameObject.SetActive(true);
            _motor.Face(_car.transform.rotation);
            _motor.Activate(_motor.Eye);
            SetMode(Mode.OnFoot);
        }

        /// <summary>Entra no carro se perto o bastante.</summary>
        public void TryEnterCar()
        {
            if (Current != Mode.OnFoot || _car == null) return;
            // Carro explodido nunca mais recebe o player
            var dmg = _car.GetComponent<VehicleDamageController>();
            if (dmg != null && dmg.PermanentlyDisabled) return;
            float d = Vector3.Distance(_motor.transform.position, _car.transform.position);
            if (d > _enterDistance) return;
            _motor.Deactivate();
            _motor.gameObject.SetActive(false);
            SetMode(Mode.Driving);
        }

        private void SetMode(Mode m)
        {
            Current = m;
            _blendT = 0f;
            if (_mainCam != null) { _blendFromPos = _mainCam.transform.position; _blendFromRot = _mainCam.transform.rotation; }
            bool driving = m == Mode.Driving;
            if (_car != null) _car.enabled = driving;
            if (_mainCam != null) _mainCam.enabled = driving;
            if (_fpsCam != null) _fpsCam.enabled = !driving;
            RadioSystem.Instance?.SetDriving(driving);
            if (!driving) Studio.Foundation.ProceduralAudioService.Instance?.SetEngineOff();
            OnModeChanged?.Invoke(m);
        }

        private void BlendCameraStep()
        {
            if (_mainCam == null || _fpsCam == null || _blendT >= 1f) return;
            _blendT += Time.deltaTime / _blendTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_blendT));
            if (t < 1f && _fpsCam.enabled)
            {
                // interpola a câmera principal até a pose da fps cam, então desliga a principal
                _mainCam.transform.position = Vector3.Lerp(_blendFromPos, _fpsCam.transform.position, t);
                _mainCam.transform.rotation = Quaternion.Slerp(_blendFromRot, _fpsCam.transform.rotation, t);
                _mainCam.enabled = true;
            }
            else if (_mainCam.enabled && _fpsCam.enabled)
            {
                _mainCam.enabled = false; // blend completo → fps cam assume
            }
        }

        public void RegisterCanHit()
        {
            CansHit++;
            OnCansChanged?.Invoke(CansHit, CansTotal);
        }

        /// <summary>Vincula refs (chamado pelo builder/inspector).</summary>
        public void Bind(PlayerCarController car, OnFootMotor motor, Camera main, Camera fps)
        {
            _car = car; _motor = motor; _mainCam = main; _fpsCam = fps;
        }
    }
}
