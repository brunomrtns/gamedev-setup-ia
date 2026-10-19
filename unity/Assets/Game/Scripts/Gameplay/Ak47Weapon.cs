using Studio.Input;
using UnityEngine;

namespace Studio.Gameplay
{
    /// <summary>
    /// AK-47 de primeira pessoa v2.1 — hitscan 120m, 30/90, recarga, ADS
    /// (arma ao centro + FOV -15), recuo com spring, camera kick, sway de
    /// rotação, bob de caminhada, respiração em idle, muzzle flash (quad+luz),
    /// tracer que percorre o trajeto, estojo ejetando e impactos em pool.
    /// </summary>
    public class Ak47Weapon : MonoBehaviour
    {
        [Header("Munição & Dano")]
        [SerializeField] private int _magSize = 30;
        [SerializeField] private int _reserve = 90;
        [SerializeField] private float _range = 120f;
        [SerializeField] private float _fireCooldown = 0.11f;
        [SerializeField] private float _reloadTime = 1.6f;

        [Header("ADS & FOV")]
        [SerializeField] private float _adsFov = 50f;
        [SerializeField] private float _normalFov = 65f;
        [SerializeField] private float _sprintFovBoost = 5f;

        [Header("Poses do rig (espaço do olho)")]
        [SerializeField] private Vector3 _hipPos = new Vector3(0.22f, -0.22f, 0.45f);
        [SerializeField] private Vector3 _adsPos = new Vector3(0f, -0.10f, 0.34f);
        [SerializeField] private Vector3 _sprintPos = new Vector3(0.24f, -0.34f, 0.40f);
        [SerializeField] private Vector3 _reloadDipPos = new Vector3(0.22f, -0.42f, 0.45f);

        [Header("Recuo & Kick")]
        [SerializeField] private float _recoilPosKick = 0.035f;
        [SerializeField] private float _recoilRotKick = 4.5f;
        [SerializeField] private float _camPitchKick = 0.9f;
        [SerializeField] private float _camRollKick = 0.35f;

        [SerializeField] private Transform _holder;
        [SerializeField] private Transform _muzzle;
        [SerializeField] private Camera _cam;
        private Light _flashLight;
        private MeshRenderer _flashQuad;
        private OnFootMotor _motor;
        private int _ammo = 30;
        private float _cooldown, _reloadTimer;
        private bool _reloading;
        private float _recoilPos, _recoilRot, _camKickPitch, _camKickRoll;
        private Vector3 _swayEuler, _swayVel, _bobOffset;
        private float _idleBreathPhase;
        private LineRenderer[] _tracers;
        private float[] _tracerProgress;
        private int _tracerIndex;
        private Transform[] _casings;
        private Vector3[] _casingVel;
        private float[] _casingLife;
        private int _casingIndex;
        private Transform[] _decals;
        private int _decalIndex;
        private float _aimRefresh, _aimDist = 40f;
        private readonly System.Random _rng = new System.Random(1234);

        public int Ammo => _ammo;
        public int Reserve => _reserve;
        public bool IsReloading => _reloading;
        public bool IsAds { get; private set; }
        public event System.Action<int, int> OnAmmoChanged;

        private void Awake() { EnsureBound(); }

        /// <summary>Re-descobre as refs em runtime (Bind() de editor não serializa luz/quad).</summary>
        private void EnsureBound()
        {
            _motor = GetComponent<OnFootMotor>();
            if (_cam == null)
            {
                var fc = transform.Find("Eye/FpsCamera");
                _cam = fc != null ? fc.GetComponent<Camera>() : GetComponentInChildren<Camera>(true);
            }
            if (_holder == null) _holder = transform.Find("Eye/WeaponHolder");
            if (_muzzle == null) _muzzle = transform.Find("Eye/WeaponHolder/Muzzle");
            if (_flashLight == null && _muzzle != null)
                _flashLight = _muzzle.GetComponentInChildren<Light>(true);
            if (_flashQuad == null && _muzzle != null)
                _flashQuad = _muzzle.GetComponentInChildren<MeshRenderer>(true);
            // pools: re-descobre por nome se não serializados
            if (_tracers == null)
            {
                var lrs = GetComponentsInChildren<LineRenderer>(true);
                if (lrs.Length > 0) SetTracerPool(lrs);
            }
            var poolRoot = GameObject.Find("FPPools");
            var search = poolRoot != null ? poolRoot.transform : transform;
            if (_casings == null)
            {
                var list = new System.Collections.Generic.List<Transform>();
                foreach (var t in search.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("Casing_")) list.Add(t);
                if (list.Count > 0) SetCasingPool(list.ToArray());
            }
            if (_decals == null)
            {
                var list = new System.Collections.Generic.List<Transform>();
                foreach (var t in search.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("Decal_")) list.Add(t);
                if (list.Count > 0) SetDecalPool(list.ToArray());
            }
        }

        public void Bind(Transform holder, Transform muzzle, Camera cam)
        {
            _holder = holder;
            _muzzle = muzzle;
            _cam = cam;
            _motor = GetComponent<OnFootMotor>();
            if (_flashLight == null)
            {
                var flash = new GameObject("MuzzleFlashLight");
                flash.transform.SetParent(muzzle, false);
                _flashLight = flash.AddComponent<Light>();
                _flashLight.type = LightType.Point;
                _flashLight.color = new Color(1f, 0.72f, 0.3f);
                _flashLight.intensity = 5f;
                _flashLight.range = 7f;
                _flashLight.enabled = false;
            }
        }

        public void SetFlashQuad(MeshRenderer quad) { _flashQuad = quad; }
        public void SetTracerPool(LineRenderer[] pool)
        {
            _tracers = pool;
            _tracerProgress = new float[pool.Length];
            for (int i = 0; i < pool.Length; i++) _tracerProgress[i] = -1f;
        }

        public void SetCasingPool(Transform[] pool)
        {
            _casings = pool;
            _casingVel = new Vector3[pool.Length];
            _casingLife = new float[pool.Length];
            for (int i = 0; i < pool.Length; i++) _casingLife[i] = -1f;
        }

        public void SetDecalPool(Transform[] pool) { _decals = pool; }

        public void AddAmmo(int amount)
        {
            _reserve += amount;
            OnAmmoChanged?.Invoke(_ammo, _reserve);
        }

        private void Update()
        {
            var input = DriveInputAdapter.Instance;
            float dt = Time.deltaTime;
            _cooldown -= dt;

            bool sprinting = _motor != null && _motor.IsSprinting;
            IsAds = input != null && input.AdsHeld && !_reloading && !sprinting;

            // --- FOV: ADS estreita, sprint alarga ---
            if (_cam != null)
            {
                float fovT = IsAds ? _adsFov : _normalFov + (sprinting ? _sprintFovBoost : 0f);
                _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, fovT, dt * 10f);
            }

            // --- Recuo: springs de volta ao rest ---
            _recoilPos = Mathf.MoveTowards(_recoilPos, 0f, dt * 0.35f);
            _recoilRot = Mathf.MoveTowards(_recoilRot, 0f, dt * 40f);
            _camKickPitch = Mathf.SmoothDamp(_camKickPitch, 0f, ref _swayVel.z, 0.09f);
            _camKickRoll = Mathf.SmoothDamp(_camKickRoll, 0f, ref _swayVel.x, 0.12f);

            // --- Sway: lag de rotação contra o look ---
            Vector2 look = input != null ? input.FootLook : Vector2.zero;
            float swayScale = IsAds ? 0.3f : 1f;
            var swayTarget = new Vector3(-look.y * 0.5f * swayScale, look.x * 0.8f * swayScale, -look.x * 0.4f * swayScale);
            _swayEuler.x = Mathf.SmoothDamp(_swayEuler.x, Mathf.Clamp(swayTarget.x, -3f, 3f), ref _swayVel.x, 0.07f);
            _swayEuler.y = Mathf.SmoothDamp(_swayEuler.y, Mathf.Clamp(swayTarget.y, -4f, 4f), ref _swayVel.y, 0.07f);
            _swayEuler.z = Mathf.SmoothDamp(_swayEuler.z, Mathf.Clamp(swayTarget.z, -3f, 3f), ref _swayVel.z, 0.07f);

            // --- Bob da arma + respiração idle ---
            float bobY = 0f, bobX = 0f;
            if (_motor != null && _motor.IsMoving)
            {
                bobY = Mathf.Sin(_motor.BobPhase * 2f) * 0.012f * swayScale;
                bobX = Mathf.Cos(_motor.BobPhase) * 0.010f * swayScale;
            }
            _idleBreathPhase += dt * 1.6f;
            if (_motor == null || !_motor.IsMoving)
                bobY += Mathf.Sin(_idleBreathPhase) * 0.004f * swayScale;
            _bobOffset = Vector3.Lerp(_bobOffset, new Vector3(bobX, bobY, 0f), dt * 8f);

            // --- Pose do holder (hip/ads/sprint/reload) ---
            if (_holder != null)
            {
                Vector3 targetPos = _reloading ? _reloadDipPos
                    : sprinting ? _sprintPos
                    : IsAds ? _adsPos : _hipPos;
                targetPos += _bobOffset + new Vector3(0f, 0f, -_recoilPos);
                _holder.localPosition = Vector3.Lerp(_holder.localPosition, targetPos, dt * 12f);
                var rotT = Quaternion.Euler(_swayEuler + new Vector3(_reloading ? 18f : _recoilRot, 0f, 0f));
                _holder.localRotation = Quaternion.Slerp(_holder.localRotation, rotT, dt * 10f);
            }

            // --- Kick de câmera aplicado como offset de pitch/roll extra ---
            if (_motor != null && _motor.Eye != null)
            {
                var e = _motor.Eye.localEulerAngles;
                _motor.Eye.localRotation = Quaternion.Euler(e.x + _camKickPitch, 0f, _camKickRoll);
            }

            // --- Convergência do cano: raycast do centro da câmera define o
            // targetPoint; o muzzle aponta geometricamente para ele (padrão
            // physics-and-combat.md — balística converge no retículo) ---
            UpdateAimConvergence(dt);

            // --- Flash 1-frame ---
            if (_flashLight != null && _flashLight.enabled) _flashLight.enabled = false;
            if (_flashQuad != null && _flashQuad.enabled) _flashQuad.enabled = false;

            // --- Reload ---
            if (_reloading)
            {
                _reloadTimer -= dt;
                if (_reloadTimer <= 0f) FinishReload();
                return;
            }
            if (input != null && input.ConsumeReload() && _ammo < _magSize && _reserve > 0)
            {
                _reloading = true;
                _reloadTimer = _reloadTime;
                Foundation.ProceduralAudioService.Instance?.PlayReload();
            }

            // --- Tiro ---
            if (input != null && input.FireHeld && _cooldown <= 0f && !sprinting)
            {
                if (_ammo > 0) Fire();
                else if (input.FirePressed) Foundation.ProceduralAudioService.Instance?.PlayDryFire();
            }

            UpdateTracers(dt);
            UpdateCasings(dt);
        }

        /// <summary>Direção do cano para convergir no targetPoint — pura, testável.</summary>
        public static Vector3 ComputeMuzzleDir(Vector3 muzzlePos, Vector3 targetPoint)
        {
            var d = targetPoint - muzzlePos;
            return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward;
        }

        private void UpdateAimConvergence(float dt)
        {
            if (_cam == null || _muzzle == null) return;
            _aimRefresh -= dt;
            if (_aimRefresh <= 0f)
            {
                _aimRefresh = 0.12f;
                var r = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                _aimDist = Physics.Raycast(r, out var h, _range)
                    ? Mathf.Clamp(h.distance, 5f, _range) : _range;
            }
            var target = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)).GetPoint(_aimDist);
            _muzzle.rotation = Quaternion.LookRotation(ComputeMuzzleDir(_muzzle.position, target));
        }

        private void Fire()
        {
            _ammo--;
            _cooldown = _fireCooldown;
            _recoilPos += _recoilPosKick;
            _recoilRot += _recoilRotKick;
            _camKickPitch += _camPitchKick;
            _camKickRoll += (float)(_rng.NextDouble() - 0.5) * 2f * _camRollKick;
            OnAmmoChanged?.Invoke(_ammo, _reserve);
            var audio = Foundation.ProceduralAudioService.Instance;
            audio?.PlayGunshotReal();
            Studio.Gameplay.PedestrianSystem.Instance?.ScareAt(transform.position, 20f);
            Studio.Gameplay.BirdSystem.Instance?.ScatterAt(transform.position, 25f);

            if (_flashLight != null) _flashLight.enabled = true;
            if (_flashQuad != null)
            {
                _flashQuad.enabled = true;
                _flashQuad.transform.localRotation = Quaternion.Euler(0f, 0f, (float)_rng.NextDouble() * 360f);
            }

            if (_cam == null) return;
            var ray = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 end = ray.origin + ray.direction * _range;
            if (Physics.Raycast(ray, out var hit, _range))
            {
                end = hit.point;
                var ped = hit.collider.GetComponentInParent<Pedestrian>();
                if (ped != null)
                {
                    // Tiro em pedestre: morte imediata + partícula efêmera.
                    // Decal é PROIBIDO aqui — buraco de bala não flutua num corpo.
                    ped.KillByShot(hit.point, ray.direction);
                    Foundation.VfxPool.Spawn("impact_metal", hit.point,
                        Quaternion.LookRotation(hit.normal));
                }
                else
                {
                    // Todos os IShootable da hierarquia respondem (dano + horn etc)
                    var shs = hit.collider.GetComponentsInParent<IShootable>();
                    if (shs != null && shs.Length > 0) { foreach (var sh in shs) sh.OnShot(hit); }
                    else audio?.PlayImpactConcrete();
                    SpawnImpact(hit);
                }
            }
            SpawnTracer(end);
            EjectCasing();
        }

        // --- Traçante: viaja do cano ao alvo em ~80ms ---
        private void SpawnTracer(Vector3 end)
        {
            if (_tracers == null) return;
            int i = _tracerIndex++ % _tracers.Length;
            _tracerProgress[i] = 0f;
            var lr = _tracers[i];
            lr.enabled = true;
            lr.SetPosition(0, _muzzle != null ? _muzzle.position : transform.position);
            lr.SetPosition(1, end);
            lr.gameObject.name = end.ToString(); // endpoint embutido (pool sem estado extra)
            lr.SetPosition(1, end);
        }

        private void UpdateTracers(float dt)
        {
            if (_tracers == null) return;
            for (int i = 0; i < _tracers.Length; i++)
            {
                if (_tracerProgress[i] < 0f) continue;
                _tracerProgress[i] += dt / 0.08f; // ~80ms de vida
                if (_tracerProgress[i] >= 1f)
                {
                    _tracers[i].enabled = false;
                    _tracerProgress[i] = -1f;
                }
            }
        }

        // --- Estojo: ejeção lateral com arco + spin, some em 2s ---
        private void EjectCasing()
        {
            if (_casings == null || _muzzle == null) return;
            int i = _casingIndex++ % _casings.Length;
            var c = _casings[i];
            c.position = _muzzle.position + _muzzle.right * 0.06f;
            c.rotation = Random.rotation;
            _casingVel[i] = _muzzle.right * 1.8f + _muzzle.up * 1.6f + _muzzle.forward * -0.4f;
            _casingLife[i] = 2f;
            c.gameObject.SetActive(true);
            Foundation.ProceduralAudioService.Instance?.PlayCasingTinkle();
        }

        private void UpdateCasings(float dt)
        {
            if (_casings == null) return;
            for (int i = 0; i < _casings.Length; i++)
            {
                if (_casingLife[i] <= 0f) continue;
                _casingLife[i] -= dt;
                if (_casingLife[i] <= 0f) { _casings[i].gameObject.SetActive(false); continue; }
                _casingVel[i] += Vector3.down * 9.8f * dt;
                var c = _casings[i];
                c.position += _casingVel[i] * dt;
                c.Rotate(400f * dt, 300f * dt, 200f * dt, Space.World);
                if (c.position.y < 0.02f) { var p = c.position; p.y = 0.02f; c.position = p; _casingVel[i] *= 0.4f; _casingVel[i].y = Mathf.Abs(_casingVel[i].y) * 0.3f; }
            }
        }

        // --- Impacto: decal orientado pela normal (pool FIFO). Em corpos
        // móveis (carro/prop com Rigidbody) o decal PARENTA no collider —
        // acompanha o movimento físico, nunca fica solto no espaço. ---
        private Transform _decalRoot;
        private void SpawnImpact(RaycastHit hit)
        {
            if (_decals == null) return;
            var d = _decals[_decalIndex++ % _decals.Length];
            if (d == null) return; // slot perdido (ex: carro despawnado levou o decal)
            if (_decalRoot == null) _decalRoot = d.parent;
            d.position = hit.point + hit.normal * 0.02f;
            d.rotation = Quaternion.LookRotation(-hit.normal) * Quaternion.Euler(0f, 0f, (float)_rng.NextDouble() * 360f);
            d.SetParent(hit.collider.attachedRigidbody != null
                ? hit.collider.transform : _decalRoot, true);
            d.gameObject.SetActive(true);
        }

        private void FinishReload()
        {
            _reloading = false;
            int take = Mathf.Min(_magSize - _ammo, _reserve);
            _ammo += take;
            _reserve -= take;
            OnAmmoChanged?.Invoke(_ammo, _reserve);
        }
    }
}
