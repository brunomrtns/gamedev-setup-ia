using System;
using Studio.Foundation;
using Studio.Input;
using UnityEngine;

namespace Studio.Gameplay
{
    public enum AbyssalWeaponSlot
    {
        PlasmaRifle = 0,
        TacticalKnife = 1
    }

    public enum WeaponActionState
    {
        Idle,
        SwimMoving,
        AimingADS,
        Firing,
        Reloading,
        MeleeSlashing
    }

    /// <summary>
    /// Controlador Tático de Armas Submarinas em Primeira Pessoa (Rifle de Plasma + Faca Tática).
    /// Gerencia estados de Idle, Nado com Sway, Mira Óptica (ADS), Disparo com dispersão balística e traçador,
    /// Recarga de munição e Golpe Corpo a Corpo (Melee).
    /// </summary>
    public class AbyssalWeaponController : MonoBehaviour
    {
        [Header("Weapon Transforms (First-Person Viewmodel)")]
        [SerializeField] private Transform _weaponHolder;
        [SerializeField] private Transform _rifleModel;
        [SerializeField] private Transform _knifeModel;
        [SerializeField] private Transform _muzzlePoint;
        [SerializeField] private LineRenderer _plasmaTracerLine;

        [Header("Plasma Rifle Configuration")]
        [SerializeField] private int _magazineCapacity = 24;
        [SerializeField] private int _currentMagazineAmmo = 24;
        [SerializeField] private int _reserveAmmo = 96;
        [SerializeField] private float _fireCooldown = 0.15f;
        [SerializeField] private float _reloadDuration = 1.4f;
        [SerializeField] private float _rifleDamage = 35.0f;
        [SerializeField] private float _rifleRange = 75.0f;

        [Header("Tactical Knife Configuration")]
        [SerializeField] private float _meleeDamage = 55.0f;
        [SerializeField] private float _meleeRange = 3.5f;
        [SerializeField] private float _meleeCooldown = 0.40f;

        [Header("Viewmodel Positions")]
        [SerializeField] private Vector3 _rifleHipLocalPos = new Vector3(0.20f, -0.20f, 0.44f);
        [SerializeField] private Vector3 _rifleAdsLocalPos = new Vector3(0.00f, -0.135f, 0.38f);
        [SerializeField] private Vector3 _knifeRestLocalPos = new Vector3(-0.20f, -0.22f, 0.38f);
        [SerializeField] private Vector3 _knifeActiveLocalPos = new Vector3(0.18f, -0.16f, 0.40f);

        private AbyssalFirstPersonMotor _motor;
        private AbyssalInputAdapter _subscribedAdapter;

        private AbyssalWeaponSlot _activeSlot = AbyssalWeaponSlot.PlasmaRifle;
        private WeaponActionState _actionState = WeaponActionState.Idle;
        private float _fireTimer;
        private float _reloadTimer;
        private float _meleeTimer;
        private float _tracerDisplayTimer;
        private float _recoilAccumulation;
        private float _swayTimer;
        private int _totalShotsFired;
        private int _totalMeleeAttacks;

        public AbyssalWeaponSlot ActiveSlot => _activeSlot;
        public WeaponActionState ActionState => _actionState;
        public int CurrentMagazineAmmo => _currentMagazineAmmo;
        public int MagazineCapacity => _magazineCapacity;
        public int ReserveAmmo => _reserveAmmo;
        public bool IsReloading => _actionState == WeaponActionState.Reloading;
        public bool IsAimingADS => _motor != null && _motor.IsAimingADS && _activeSlot == AbyssalWeaponSlot.PlasmaRifle && !IsReloading;
        public float CurrentSpreadDegrees => AbyssalMath.ComputeBallisticSpreadDegrees(IsAimingADS, AbyssalMath.HIPFIRE_SPREAD_DEG, AbyssalMath.ADS_SPREAD_DEG, _recoilAccumulation);
        public int TotalShotsFired => _totalShotsFired;
        public int TotalMeleeAttacks => _totalMeleeAttacks;
        public bool IsSubscribedToInput => _subscribedAdapter != null;

        public event Action<int, int, AbyssalWeaponSlot, WeaponActionState> OnWeaponTelemetryChanged;
        public event Action<bool> OnTargetHit; // bool = destroyed target

        private void Awake()
        {
            _motor = GetComponent<AbyssalFirstPersonMotor>();
            EnsureWeaponReferences();
            UpdateWeaponVisibility();
        }

        private void OnEnable()
        {
            EnsureInputSubscribed();
        }

        private void OnDisable()
        {
            UnsubscribeInput();
        }

        public void EnsureWeaponReferences()
        {
            if (_motor == null) _motor = GetComponent<AbyssalFirstPersonMotor>();

            if (_weaponHolder == null)
            {
                var pivot = _motor != null ? _motor.CameraPivot : transform;
                var holder = pivot.Find("WeaponHolder");
                if (holder != null) _weaponHolder = holder;
            }

            if (_weaponHolder != null)
            {
                if (_rifleModel == null) _rifleModel = _weaponHolder.Find("FPS_PlasmaRifle");
                if (_knifeModel == null) _knifeModel = _weaponHolder.Find("FPS_TacticalKnife");
            }
        }

        public void SetWeaponReferences(Transform holder, Transform rifle, Transform knife, Transform muzzle, LineRenderer tracer)
        {
            _weaponHolder = holder;
            _rifleModel = rifle;
            _knifeModel = knife;
            _muzzlePoint = muzzle;
            _plasmaTracerLine = tracer;
            UpdateWeaponVisibility();
        }

        public void EnsureInputSubscribed()
        {
            var adapter = AbyssalInputAdapter.Instance;
            if (adapter != null && _subscribedAdapter != adapter)
            {
                UnsubscribeInput();
                _subscribedAdapter = adapter;
                _subscribedAdapter.OnFire += HandleFireInput;
                _subscribedAdapter.OnReload += HandleReloadInput;
                _subscribedAdapter.OnMelee += HandleMeleeInput;
                _subscribedAdapter.OnSwitchWeapon += HandleSwitchWeaponInput;
            }
        }

        private void UnsubscribeInput()
        {
            if (_subscribedAdapter != null)
            {
                _subscribedAdapter.OnFire -= HandleFireInput;
                _subscribedAdapter.OnReload -= HandleReloadInput;
                _subscribedAdapter.OnMelee -= HandleMeleeInput;
                _subscribedAdapter.OnSwitchWeapon -= HandleSwitchWeaponInput;
                _subscribedAdapter = null;
            }
        }

        private void Update()
        {
            StepWeapons(Time.deltaTime);
        }

        public void StepWeapons(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            EnsureInputSubscribed();
            EnsureWeaponReferences();

            if (_fireTimer > 0f) _fireTimer = Mathf.Max(0f, _fireTimer - deltaTime);
            if (_meleeTimer > 0f) _meleeTimer = Mathf.Max(0f, _meleeTimer - deltaTime);
            if (_recoilAccumulation > 0f) _recoilAccumulation = Mathf.MoveTowards(_recoilAccumulation, 0f, deltaTime * 4.5f);

            if (_tracerDisplayTimer > 0f)
            {
                _tracerDisplayTimer -= deltaTime;
                if (_tracerDisplayTimer <= 0f && _plasmaTracerLine != null)
                {
                    _plasmaTracerLine.enabled = false;
                }
            }

            // Ciclo de Recarga
            if (_actionState == WeaponActionState.Reloading)
            {
                _reloadTimer -= deltaTime;
                if (_reloadTimer <= 0f)
                {
                    CompleteReload();
                }
            }
            else if (_meleeTimer > 0f)
            {
                _actionState = WeaponActionState.MeleeSlashing;
            }
            else if (_fireTimer > _fireCooldown * 0.5f)
            {
                _actionState = WeaponActionState.Firing;
            }
            else if (IsAimingADS)
            {
                _actionState = WeaponActionState.AimingADS;
            }
            else if (_motor != null && _motor.CurrentSpeed > 0.35f)
            {
                _actionState = WeaponActionState.SwimMoving;
            }
            else
            {
                _actionState = WeaponActionState.Idle;
            }

            UpdateViewmodelPose(deltaTime);
        }

        private void UpdateViewmodelPose(float deltaTime)
        {
            _swayTimer += deltaTime * (_motor != null && _motor.CurrentSpeed > 0.35f ? 5.2f : 1.8f);
            float swayX = Mathf.Cos(_swayTimer) * (IsAimingADS ? 0.002f : 0.010f);
            float swayY = Mathf.Sin(_swayTimer * 2f) * (IsAimingADS ? 0.002f : 0.008f);

            if (_rifleModel != null)
            {
                Vector3 targetPos = IsAimingADS ? _rifleAdsLocalPos : _rifleHipLocalPos;
                if (_actionState == WeaponActionState.Reloading)
                {
                    targetPos += new Vector3(0f, -0.10f, -0.05f);
                }
                // Recuo longitudinal no eixo Z
                float recoilZ = -Mathf.Min(0.06f, _recoilAccumulation * 0.025f);
                targetPos += new Vector3(swayX, swayY, recoilZ);

                _rifleModel.localPosition = Vector3.Lerp(_rifleModel.localPosition, targetPos, deltaTime * 16f);
            }

            if (_knifeModel != null)
            {
                Vector3 knifeTarget = _activeSlot == AbyssalWeaponSlot.TacticalKnife ? _knifeActiveLocalPos : _knifeRestLocalPos;
                if (_actionState == WeaponActionState.MeleeSlashing)
                {
                    knifeTarget = new Vector3(0.05f, -0.10f, 0.58f);
                }
                _knifeModel.localPosition = Vector3.Lerp(_knifeModel.localPosition, knifeTarget + new Vector3(swayX, swayY, 0f), deltaTime * 18f);
            }
        }

        private void HandleFireInput()
        {
            EnsurePlayStateIfStartMenu();
            if (_activeSlot == AbyssalWeaponSlot.TacticalKnife)
            {
                TryExecuteMeleeSlash();
            }
            else
            {
                TryFirePlasmaRifle();
            }
        }

        private void HandleReloadInput()
        {
            TryStartReload();
        }

        private void HandleMeleeInput()
        {
            EnsurePlayStateIfStartMenu();
            TryExecuteMeleeSlash();
        }

        private void HandleSwitchWeaponInput()
        {
            SwitchWeaponSlot(_activeSlot == AbyssalWeaponSlot.PlasmaRifle
                ? AbyssalWeaponSlot.TacticalKnife
                : AbyssalWeaponSlot.PlasmaRifle);
        }

        private void EnsurePlayStateIfStartMenu()
        {
            var gm = AbyssalGameManager.Instance;
            if (gm != null && gm.CurrentState == GameState.StartMenu)
            {
                gm.StartGame();
            }
        }

        public bool TryFirePlasmaRifle()
        {
            if (_actionState == WeaponActionState.Reloading || _fireTimer > 0f)
            {
                return false;
            }

            if (_currentMagazineAmmo <= 0)
            {
                TryStartReload();
                return false;
            }

            _activeSlot = AbyssalWeaponSlot.PlasmaRifle;
            UpdateWeaponVisibility();

            _currentMagazineAmmo--;
            _totalShotsFired++;
            _fireTimer = _fireCooldown;
            _actionState = WeaponActionState.Firing;

            float spreadDeg = CurrentSpreadDegrees;
            _recoilAccumulation = Mathf.Min(3.5f, _recoilAccumulation + (IsAimingADS ? 0.35f : 0.75f));

            ProceduralAudioService.Instance?.PlayPlasmaShot();

            // Raycast / SphereCast balístico a partir do centro óptico da câmera
            Transform aimOrigin = _motor != null ? _motor.CameraPivot : transform;
            Vector3 origin = aimOrigin.position;
            Vector3 baseForward = aimOrigin.forward;

            // Dispersão determinística leve baseada no índice do tiro
            float angleRad = (_totalShotsFired * 1.618f) * Mathf.PI * 2f;
            float spreadRad = spreadDeg * Mathf.Deg2Rad * 0.35f;
            Vector3 shotDir = (baseForward
                + aimOrigin.right * (Mathf.Cos(angleRad) * spreadRad)
                + aimOrigin.up * (Mathf.Sin(angleRad) * spreadRad)).normalized;

            Vector3 hitPoint = origin + shotDir * _rifleRange;
            AbyssalThreatDrone hitDrone = FindBestDroneAlongRay(origin, shotDir, _rifleRange, out hitPoint);

            if (hitDrone != null)
            {
                bool killed = hitDrone.TakeDamage(_rifleDamage);
                OnTargetHit?.Invoke(killed);
            }

            RenderPlasmaTracer(origin, hitPoint);
            NotifyTelemetry();
            return true;
        }

        public bool TryExecuteMeleeSlash()
        {
            if (_meleeTimer > 0f) return false;

            _meleeTimer = _meleeCooldown;
            _totalMeleeAttacks++;
            _actionState = WeaponActionState.MeleeSlashing;

            if (_knifeModel != null) _knifeModel.gameObject.SetActive(true);
            ProceduralAudioService.Instance?.PlayKnifeSlash();

            Transform aimOrigin = _motor != null ? _motor.CameraPivot : transform;
            Vector3 origin = aimOrigin.position;
            Vector3 forward = aimOrigin.forward;

            // Verificar drones próximos no alcance da faca tática
            var drones = FindObjectsByType<AbyssalThreatDrone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var drone in drones)
            {
                if (drone == null || drone.IsDestroyed) continue;
                Vector3 toDrone = drone.transform.position - origin;
                if (toDrone.magnitude <= _meleeRange && Vector3.Dot(forward, toDrone.normalized) >= 0.25f)
                {
                    bool killed = drone.TakeDamage(_meleeDamage);
                    OnTargetHit?.Invoke(killed);
                }
            }

            NotifyTelemetry();
            return true;
        }

        public bool TryStartReload()
        {
            if (_actionState == WeaponActionState.Reloading) return false;
            if (_currentMagazineAmmo >= _magazineCapacity || _reserveAmmo <= 0) return false;

            _actionState = WeaponActionState.Reloading;
            _reloadTimer = _reloadDuration;
            ProceduralAudioService.Instance?.PlayReload();
            NotifyTelemetry();
            return true;
        }

        private void CompleteReload()
        {
            int needed = _magazineCapacity - _currentMagazineAmmo;
            int transfer = Mathf.Min(needed, _reserveAmmo);
            _currentMagazineAmmo += transfer;
            _reserveAmmo -= transfer;
            _actionState = WeaponActionState.Idle;
            NotifyTelemetry();
        }

        public void SwitchWeaponSlot(AbyssalWeaponSlot slot)
        {
            _activeSlot = slot;
            if (_actionState == WeaponActionState.Reloading)
            {
                _actionState = WeaponActionState.Idle;
                _reloadTimer = 0f;
            }
            UpdateWeaponVisibility();
            NotifyTelemetry();
        }

        public void AddReserveAmmo(int amount)
        {
            if (amount <= 0) return;
            _reserveAmmo += amount;
            NotifyTelemetry();
        }

        public void ResetWeapons()
        {
            _activeSlot = AbyssalWeaponSlot.PlasmaRifle;
            _actionState = WeaponActionState.Idle;
            _currentMagazineAmmo = _magazineCapacity;
            _reserveAmmo = 96;
            _fireTimer = 0f;
            _reloadTimer = 0f;
            _meleeTimer = 0f;
            _recoilAccumulation = 0f;
            _totalShotsFired = 0;
            _totalMeleeAttacks = 0;
            UpdateWeaponVisibility();
            NotifyTelemetry();
        }

        private void UpdateWeaponVisibility()
        {
            if (_rifleModel != null)
            {
                _rifleModel.gameObject.SetActive(_activeSlot == AbyssalWeaponSlot.PlasmaRifle);
            }
            if (_knifeModel != null)
            {
                // Faca visível quando equipada ou durante ataque melee rápido
                _knifeModel.gameObject.SetActive(_activeSlot == AbyssalWeaponSlot.TacticalKnife || _actionState == WeaponActionState.MeleeSlashing);
            }
        }

        private AbyssalThreatDrone FindBestDroneAlongRay(Vector3 origin, Vector3 direction, float maxRange, out Vector3 hitPoint)
        {
            hitPoint = origin + direction * maxRange;

            // 1. Physics Raycast / SphereCast padrão
            if (Physics.SphereCast(origin, 0.35f, direction, out RaycastHit hit, maxRange))
            {
                hitPoint = hit.point;
                var drone = hit.collider.GetComponentInParent<AbyssalThreatDrone>();
                if (drone != null && !drone.IsDestroyed)
                {
                    return drone;
                }
            }

            // 2. Checagem geométrica determinística para funcionar inclusive em EditMode sem passo de Physics
            AbyssalThreatDrone bestDrone = null;
            float bestDist = maxRange;
            var drones = FindObjectsByType<AbyssalThreatDrone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var d in drones)
            {
                if (d == null || d.IsDestroyed) continue;
                Vector3 toCenter = d.transform.position - origin;
                float proj = Vector3.Dot(toCenter, direction);
                if (proj <= 0f || proj > bestDist) continue;

                Vector3 closestPoint = origin + direction * proj;
                float perpDist = Vector3.Distance(closestPoint, d.transform.position);
                if (perpDist <= 1.25f)
                {
                    bestDist = proj;
                    bestDrone = d;
                    hitPoint = closestPoint;
                }
            }

            return bestDrone;
        }

        private void RenderPlasmaTracer(Vector3 origin, Vector3 targetPoint)
        {
            if (_plasmaTracerLine == null) return;
            Vector3 startPos = _muzzlePoint != null ? _muzzlePoint.position : (origin + transform.right * 0.18f - transform.up * 0.12f + transform.forward * 0.5f);
            _plasmaTracerLine.enabled = true;
            _plasmaTracerLine.positionCount = 2;
            _plasmaTracerLine.SetPosition(0, startPos);
            _plasmaTracerLine.SetPosition(1, targetPoint);
            _tracerDisplayTimer = 0.08f;
        }

        private void NotifyTelemetry()
        {
            OnWeaponTelemetryChanged?.Invoke(_currentMagazineAmmo, _reserveAmmo, _activeSlot, _actionState);
        }
    }
}
