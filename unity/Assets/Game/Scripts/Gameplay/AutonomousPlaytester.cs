using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Studio.Foundation;
using Studio.Input;
using Studio.QA;

namespace Studio.Gameplay
{
    /// <summary>
    /// Adaptador NEON DISTRICT do núcleo Studio.QA: joga de verdade via os
    /// canais reais de input (SetVirtualControl/SetQaFootMove/TriggerEnterExit
    /// — os mesmos dos botões touch), executa cenários com pré-condições
    /// verificadas, varre invariants de todas as entidades e exporta sessão.
    ///
    /// Cenários (cada um declara pré-condição, ação, observação, veredito):
    ///   PhysicsSimGuard  — regression: simulationMode deve ser FixedUpdate
    ///   DriveAccel/Steer/Brake — powertrain/direção/freio por input real
    ///   ExitEnter        — ciclo sair→andar→entrar pelo pipeline real
    ///   ShotPanic        — AK real (FireHeld) → munição cai → peds fogem
    ///   ScarePanic       — subsistema isolado: ScareAt → pânico
    ///   RunOver          — atropelamento: contato físico → IsDown
    /// </summary>
    public class AutonomousPlaytester : MonoBehaviour
    {
        const float FLIP_LIMIT = 2.5f, SCAN_EVERY = 0.33f, FRAME_EVERY = 8f;
        const float SESSION_BUDGET = 210f;

        // ---------- núcleo QA ----------
        QaContext _ctx;
        readonly FindingLog _findings = new();
        readonly CoverageGrid _coverage = new(8, 160f);
        PerfTracker _perf = new(10f);
        ExceptionWatchdog _exc;
        readonly List<QaScenario> _queue = new();
        QaScenario _active; bool _prepping;
        float _roamUntil;

        // ---------- sessão ----------
        float _elapsed; int _seed = 20261017;
        string _sessionId; bool _ended;
        readonly StringBuilder _log = new();
        public int Seed => _seed;
        public string SessionId => _sessionId;

        // ---------- refs do jogo ----------
        NdRig _rig; TrafficNetwork _net;
        Rigidbody _rb;
        Camera _cam;
        VehicleDamageController _dmg;

        // ---------- roam ----------
        Vector3 _target, _lastPos;
        float _stuckT, _flippedT, _nextFrame, _nextScan;
        int _stuckCount, _teleports, _collisions;
        float _driveUntil, _footUntil; bool _roamFoot;

        // ---------- monitores ----------
        class PedObs { public Vector3 pos; public Quaternion legRot; public Transform leg;
            public float stillT; public bool slidFlagged; }
        readonly Dictionary<int, PedObs> _obs = new();
        readonly Dictionary<int, float> _trafficFlipT = new();
        int _panicTotal, _shots;

        // ==================== ENTRY ====================
        public static AutonomousPlaytester Attach(Transform player)
        {
            var pt = player.gameObject.AddComponent<AutonomousPlaytester>();
            pt.Bootstrap(player);
            return pt;
        }

        void Bootstrap(Transform player)
        {
            _sessionId = System.DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")
                + "_" + (GetInstanceID() & 0xffff).ToString("x4");
            Random.InitState(_seed);
            _rb = player.GetComponent<Rigidbody>();
            _lastPos = player.position;
            _dmg = player.GetComponent<VehicleDamageController>();
            if (_dmg != null) _dmg.IsGodModeActive = true;

            _rig = new NdRig
            {
                car = player.GetComponent<PlayerCarController>(),
                input = DriveInputAdapter.Instance,
                mode = NeonDistrictModeManager.Instance,
                pedSys = PedestrianSystem.Instance,
                traffic = Object.FindFirstObjectByType<TrafficManager>()
            };
            _rig.motor = Object.FindFirstObjectByType<OnFootMotor>(
                FindObjectsInactive.Include);
            if (_rig.motor != null)
                _rig.weapon = _rig.motor.GetComponent<Ak47Weapon>();
            _cam = Camera.main;

            _ctx = new QaContext
            {
                Host = this, Seed = _seed,
                Player = () => ActivePlayer(),
                Flag = f => _findings.Add(f),
                Log = s => _log.AppendLine(s),
                Capture = n => CaptureFrame(n)
            };
            _exc = new ExceptionWatchdog((msg, st) =>
            {
                _findings.Add(new QaFinding("EXCEPTION", QaCategory.Stability,
                    QaSeverity.Critical, _elapsed, msg, Vector3.zero));
                _log.AppendLine($"[QA-EXC] {msg}");
            });

            _net = Object.FindFirstObjectByType<TrafficNetwork>();

            // fila de cenários — ordem: guards → drive → walk → causal → roam
            _queue.Add(new PhysicsSimGuard());
            _queue.Add(new DriveAccel(_rig));
            _queue.Add(new DriveSteer(_rig));
            _queue.Add(new DriveBrake(_rig));
            _queue.Add(new ExitEnterCycle(_rig));
            _queue.Add(new ShotPanic(_rig));
            _queue.Add(new ScarePanic(_rig));
            _queue.Add(new RunOverPed(_rig));
            _roamUntil = 0f;
            _log.AppendLine(
                $"[QA] attach @{player.position} seed={_seed} id={_sessionId}");
        }

        /// <summary>Refs podem spawnar após o attach — re-resolve os que faltam.</summary>
        void RefetchRig()
        {
            if (_rig.input == null) _rig.input = DriveInputAdapter.Instance;
            if (_rig.mode == null) _rig.mode = NeonDistrictModeManager.Instance;
            if (_rig.pedSys == null) _rig.pedSys = PedestrianSystem.Instance;
            if (_rig.traffic == null)
                _rig.traffic = Object.FindFirstObjectByType<TrafficManager>();
            if (_rig.motor == null)
            {
                _rig.motor = Object.FindFirstObjectByType<OnFootMotor>(
                    FindObjectsInactive.Include);
                if (_rig.motor != null)
                    _rig.weapon = _rig.motor.GetComponent<Ak47Weapon>();
            }
            if (_net == null)
                _net = Object.FindFirstObjectByType<TrafficNetwork>();
        }

        /// <summary>Alvo de condução: pose de FAIXA (TrafficNetwork), nunca o
        /// centro geométrico da rua — a avenida tem canteiro com postes.</summary>
        Vector3 LaneTarget(Vector3 hint, float along)
        {
            var rough = CityLayout.RoadPoint(Random.value < .5f,
                Random.Range(0, CityLayout.LINES), along);
            if (_net != null && _net.TryGetNearestPose(rough,
                    out var pos, out _))
                return pos;
            return rough;
        }

        Transform ActivePlayer()
        {
            if (_rig.mode != null
                && _rig.mode.Current == NeonDistrictModeManager.Mode.OnFoot
                && _rig.motor != null)
                return _rig.motor.transform;
            return transform;
        }

        // ==================== LOOP ====================
        void Update()
        {
            _elapsed += Time.deltaTime;
            RefetchRig();
            _ctx.Time = _elapsed;
            var p = ActivePlayer().position;
            _coverage.Track(p);

            StepScenarios();
            if (_active == null) Roam(p);
            ScanEntities(p);
            _perf.Tick(Time.unscaledDeltaTime, _elapsed, CurrentContext());
            MaybeCapture(p);

            if (!_ended && _elapsed > SESSION_BUDGET) EndSession();
        }

        string CurrentContext()
            => _active != null ? _active.Name
               : _roamFoot ? "roam_foot" : "roam_drive";

        // ==================== CENÁRIOS ====================
        void StepScenarios()
        {
            if (_active == null)
            {
                if (_queue.Count == 0) return;
                if (_elapsed < _roamUntil) return;  // respiro entre cenários
                _active = _queue[0]; _queue.RemoveAt(0);
                _prepping = true;
                _log.AppendLine($"[QA] ▶ {_active.Name} prep");
            }

            if (_prepping)
            {
                _active.PrepElapsed += Time.deltaTime;
                var st = _active.TryPrepare(_ctx);
                if (st == PrepStatus.Blocked)
                {
                    _active.Result = QaResult.Blocked;
                    RecordScenario();
                    return;
                }
                if (st == PrepStatus.Working)
                {
                    if (_active.PrepElapsed > _active.PrepTimeout)
                    {
                        _active.Result = QaResult.Blocked;
                        _active.BlockedReason = "prep timeout";
                        RecordScenario();
                    }
                    return;
                }
                _prepping = false;
                _log.AppendLine(
                    $"[QA] ▶ {_active.Name} run (prep {_active.PrepElapsed:F1}s)");
            }

            _active.Elapsed += Time.deltaTime;
            _active.Tick(_ctx, Time.deltaTime);
            if (_active.IsDone(_ctx)) RecordScenario();
            else if (_active.Elapsed > _active.Timeout)
            { _active.OnTimeout(_ctx); RecordScenario(); }
        }

        void RecordScenario()
        {
            ClearInputs();
            _log.AppendLine($"[QA] ■ {_active.Name} → {_active.Result}" +
                (_active.Detail.Length > 0 ? $" — {_active.Detail}" : "") +
                (_active.BlockedReason.Length > 0
                    ? $" ({_active.BlockedReason})" : ""));
            _session.scenarios.Add(_active.Record());
            _active = null;
            _roamUntil = _elapsed + 6f;   // deixa o mundo assentar
        }

        readonly QaSession _session = new();

        // ==================== ROAM (input real) ====================
        void Roam(Vector3 p)
        {
            // alternância periódica carro↔a pé (só fora de cenário)
            if (!_roamFoot && _elapsed >= _driveUntil && _elapsed > 30f)
            {
                _roamFoot = true; _footUntil = _elapsed + 15f;
                _rig.input?.TriggerEnterExit();
                _log.AppendLine($"[QA-INFO] sai do carro @{_elapsed:F0}s");
            }

            bool onFoot = _rig.mode != null
                && _rig.mode.Current == NeonDistrictModeManager.Mode.OnFoot;

            // exit falhou (modo continuou Driving) → cancela a fase a pé
            if (_roamFoot && !onFoot)
            { _roamFoot = false; _driveUntil = _elapsed + 60f; }

            if (onFoot) RoamFoot(p);
            else RoamDrive(p);
        }

        void RoamDrive(Vector3 p)
        {
            var to = _target - p; to.y = 0f;
            if (to.magnitude < 8f)
                _target = LaneTarget(p, Random.Range(-140f, 140f));
            DriveInputToward(_target);

            // travado com gas → STUCK → teleport (marcado, não conta nav)
            float moved = Vector3.Distance(p, _lastPos); _lastPos = p;
            if (moved < 0.05f && _elapsed > 3f)
            {
                _stuckT += Time.deltaTime;
                if (_stuckT > 5f)
                {
                    _stuckT = 0f; _stuckCount++;
                    _findings.Add(new QaFinding("STUCK", QaCategory.Navigation,
                        QaSeverity.Major, _elapsed,
                        $"{p.x:F0},{p.z:F0} #{_stuckCount}", p));
                    if (_stuckCount >= 2)
                    {
                        _stuckCount = 0;
                        _target = LaneTarget(p, Random.Range(-100f, 100f));
                        if (_rig.car != null)
                            _rig.car.ResetToPose(_target + Vector3.up * 0.5f,
                                Quaternion.identity);
                        _teleports++;
                        _log.AppendLine("[QA-INFO] un-stick teleport (recovery)");
                    }
                }
            }
            else _stuckT = 0f;

            DetectFlipped(p);
        }

        void RoamFoot(Vector3 p)
        {
            if (_rig.motor == null) return;
            float dCar = Vector3.Distance(p, transform.position);
            if (_elapsed >= _footUntil)
            {
                // fim da janela a pé: volta pro carro e reentra de verdade
                if (dCar < 2.6f)
                {
                    _rig.input?.TriggerEnterExit();
                    _roamFoot = false; _driveUntil = _elapsed + 60f;
                    _rig.input?.SetQaFootMove(Vector2.zero);
                    _log.AppendLine(
                        $"[QA-INFO] volta pro carro @{_elapsed:F0}s");
                    return;
                }
                FootInputToward(transform.position);
                return;
            }
            var to = _target - p; to.y = 0f;
            if (to.magnitude < 3f)
                _target = CityLayout.SidewalkPoint(Random.value < .5f,
                    Random.Range(1, CityLayout.LINES - 1),
                    Random.Range(0, 2) == 0 ? -1 : 1, Random.value * 120f - 60f);
            FootInputToward(_target);
        }

        // ---------- drivers de input real ----------
        float _blockT, _reverseUntil; int _recoveries, _steerSign;

        /// <summary>
        /// Dirige de verdade com detecção de obstáculo: whiskers frontais
        /// (centro + ±22°). Bloqueio persistente → manobra de RÉ real
        /// (input Brake) esterçando pro lado livre, depois retoma a rota.
        /// </summary>
        void DriveInputToward(Vector3 target)
        {
            if (_rig.input == null || _rig.car == null) return;
            var t = _rig.car.transform;
            var to = target - t.position; to.y = 0f;
            const DriveInputAdapter.DriveControl G = DriveInputAdapter.DriveControl.Gas;
            const DriveInputAdapter.DriveControl B = DriveInputAdapter.DriveControl.Brake;
            const DriveInputAdapter.DriveControl SL = DriveInputAdapter.DriveControl.SteerLeft;
            const DriveInputAdapter.DriveControl SR = DriveInputAdapter.DriveControl.SteerRight;

            if (_elapsed < _reverseUntil)
            {
                // ré: Brake integra velocidade negativa; esterça pro lado livre
                _rig.input.SetVirtualControl(G, false);
                _rig.input.SetVirtualControl(B, true);
                _rig.input.SetVirtualControl(SL, _steerSign < 0);
                _rig.input.SetVirtualControl(SR, _steerSign > 0);
                return;
            }

            bool bC = Whisker(t, 0f, out var hC);
            bool bL = Whisker(t, -22f, out var hL);
            bool bR = Whisker(t, 22f, out var hR);
            if ((bC || bL || bR) && to.magnitude > 5f)
            {
                _blockT += Time.deltaTime;
                if (_blockT > 0.9f)
                {
                    _blockT = 0f; _recoveries++;
                    _reverseUntil = _elapsed + 1.4f;
                    // ré pro lado com mais espaço (whisker menos obstruído)
                    _steerSign = hL.distance < hR.distance ? 1 : -1;
                    var hitName = bC ? hC.collider.name
                        : (bL ? hL.collider.name : hR.collider.name);
                    _log.AppendLine("[QA-INFO] obstáculo '" + hitName
                        + "' → ré #" + _recoveries + " @" + _elapsed.ToString("F0") + "s");
                    return;
                }
            }
            else _blockT = 0f;

            float ang = Vector3.SignedAngle(t.forward, to, Vector3.up);
            _rig.input.SetVirtualControl(SL, ang < -9f);
            _rig.input.SetVirtualControl(SR, ang > 9f);
            _rig.input.SetVirtualControl(G, true);
            _rig.input.SetVirtualControl(B, false);
        }

        /// <summary>Raio frontal no para-choque; ignora os colliders do próprio carro.</summary>
        readonly RaycastHit[] _wHits = new RaycastHit[8];
        bool Whisker(Transform t, float angDeg, out RaycastHit hit)
        {
            var dir = Quaternion.Euler(0f, angDeg, 0f) * t.forward;
            int n = Physics.RaycastNonAlloc(t.position + Vector3.up * 0.55f,
                dir, _wHits, 5f, ~0, QueryTriggerInteraction.Ignore);
            hit = default;
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (_wHits[i].transform.IsChildOf(t)) continue; // próprio carro
                if (_wHits[i].distance < best) { best = _wHits[i].distance; hit = _wHits[i]; }
            }
            return best < float.MaxValue;
        }

        void FootInputToward(Vector3 target)
        {
            if (_rig.input == null || _rig.motor == null) return;
            var to = target - _rig.motor.transform.position; to.y = 0f;
            var local = _rig.motor.transform.InverseTransformDirection(
                to.normalized);
            _rig.input.SetQaFootMove(new Vector2(
                Mathf.Clamp(local.x, -1f, 1f), Mathf.Clamp(local.z, -1f, 1f)));
        }

        void ClearInputs()
        {
            _rig.input?.ClearVirtual();
            _rig.input?.SetQaFootMove(Vector2.zero);
        }

        void DetectFlipped(Vector3 p)
        {
            var t = transform;
            if (Vector3.Dot(t.up, Vector3.up) < 0.2f
                && (_rb == null || _rb.linearVelocity.magnitude < 1f))
            {
                _flippedT += Time.deltaTime;
                if (_flippedT > FLIP_LIMIT)
                {
                    _flippedT = 0f;
                    _findings.Add(new QaFinding("PLAYER_FLIPPED",
                        QaCategory.Physics, QaSeverity.Major, _elapsed,
                        "auto-unflip", p, "limited"));
                    if (_rb != null)
                    { _rb.linearVelocity = Vector3.zero;
                      _rb.angularVelocity = Vector3.zero; }
                    t.position += Vector3.up * 1.5f;
                    t.rotation = Quaternion.Euler(0f, t.eulerAngles.y, 0f);
                    _log.AppendLine(
                        $"[QA-INFO] unflip @{p.x:F0},{p.z:F0} (recovery)");
                }
            }
            else _flippedT = 0f;
        }

        // ==================== MONITORES ====================
        void ScanEntities(Vector3 playerPos)
        {
            if (_elapsed < _nextScan) return;
            _nextScan = _elapsed + SCAN_EVERY;

            ScanPeds(playerPos);
            ScanTraffic();
            ScanPlayer(playerPos);
        }

        void ScanPeds(Vector3 playerPos)
        {
            var peds = FindObjectsByType<Pedestrian>(FindObjectsSortMode.None);
            int panicNow = 0;
            foreach (var p in peds)
            {
                int id = p.GetInstanceID();
                if (!_obs.TryGetValue(id, out var o))
                { o = new PedObs { pos = p.transform.position };
                  var smr = p.GetComponentInChildren<SkinnedMeshRenderer>();
                  if (smr != null)
                      foreach (var b in smr.bones)
                          if (b != null && b.name.Contains("UpLeg"))
                          { o.leg = b; o.legRot = b.rotation; break; }
                  _obs[id] = o; }

                var pp = p.transform.position;
                var anim = p.GetComponentInChildren<Animator>();

                if (anim != null && !p.IsDown
                    && anim.GetFloat("Speed") > 1.5f) panicNow++;

                // FOOT_SLIDING por-ped: desloca sem a perna da MALHA girar
                if (!p.IsDown && !o.slidFlagged && o.leg != null)
                {
                    float dpos = Vector3.Distance(pp, o.pos);
                    float drot = Quaternion.Angle(o.legRot, o.leg.rotation);
                    o.pos = pp; o.legRot = o.leg.rotation;
                    if (dpos > 0.02f)
                    {
                        o.stillT = drot < 0.6f ? o.stillT + SCAN_EVERY : 0f;
                        if (o.stillT > 4f)
                        {
                            o.slidFlagged = true;
                            _findings.Add(new QaFinding("FOOT_SLIDING",
                                QaCategory.Ai, QaSeverity.Major, _elapsed,
                                $"ped move sem malha animar: {p.name}", pp));
                        }
                    }
                    else o.stillT = 0f;
                }

                // PED_FLOATING: menor osso vs chão real (não bounds)
                if (!p.IsDown && pp.y > -5f)
                {
                    float minY = float.MaxValue;
                    foreach (var smr in p.GetComponentsInChildren<
                        SkinnedMeshRenderer>())
                        foreach (var b in smr.bones)
                            if (b != null && b.position.y < minY)
                                minY = b.position.y;
                    if (minY < float.MaxValue
                        && Physics.Raycast(pp + Vector3.up * 1.5f,
                            Vector3.down, out var hit, 8f)
                        && minY - hit.point.y > 0.45f)
                        _findings.Add(new QaFinding("PED_FLOATING",
                            QaCategory.Physics, QaSeverity.Major, _elapsed,
                            $"bonesMin−ground={minY - hit.point.y:F2}m {p.name}",
                            pp));
                }
                // cadáver flutuando
                if (p.IsDown)
                {
                    var rends = p.GetComponentsInChildren<Renderer>();
                    if (rends.Length > 0
                        && Physics.Raycast(pp + Vector3.up * 2f, Vector3.down,
                            out var hit2, 10f)
                        && rends[0].bounds.min.y - hit2.point.y > 0.35f)
                        _findings.Add(new QaFinding("CORPSE_FLOATING",
                            QaCategory.Physics, QaSeverity.Major, _elapsed,
                            $"{p.name}", pp));
                }
                if (anim == null || anim.runtimeAnimatorController == null)
                    _findings.Add(new QaFinding("PED_NO_ANIM",
                        QaCategory.Ai, QaSeverity.Major, _elapsed,
                        p.name, pp));
            }
            if (panicNow > _panicTotal) _panicTotal = panicNow;
        }

        void ScanTraffic()
        {
            var vs = FindObjectsByType<TrafficVehicle>(
                FindObjectsSortMode.None);
            foreach (var v in vs)
            {
                int id = v.GetInstanceID();
                var vp = v.transform.position;
                // wrecked capotado é aftermath legítimo de crash — só flag
                // capotamento em veículos ainda dirigindo/livres
                bool flipped = v.State != TrafficVehicle.CrashState.Wrecked
                    && Vector3.Dot(v.transform.up, Vector3.up) < 0.35f;
                _trafficFlipT[id] = flipped
                    ? (_trafficFlipT.TryGetValue(id, out var t) ? t : 0f)
                        + SCAN_EVERY
                    : 0f;
                if (_trafficFlipT[id] > 2f)
                {
                    _trafficFlipT[id] = -999f;
                    _findings.Add(new QaFinding("TRAFFIC_FLIPPED",
                        QaCategory.Physics, QaSeverity.Major, _elapsed,
                        $"{v.name} state={v.State}", vp));
                }
                if (vp.y > 4f)
                    _findings.Add(new QaFinding("TRAFFIC_FLYING",
                        QaCategory.Physics, QaSeverity.Critical, _elapsed,
                        $"{v.name} y={vp.y:F1} state={v.State}", vp));
            }
        }

        void ScanPlayer(Vector3 p)
        {
            if (float.IsNaN(p.x) || p.y < -20f)
                _findings.Add(new QaFinding("PLAYER_FALL",
                    QaCategory.Stability, QaSeverity.Critical, _elapsed,
                    $"y={p.y:F1}", p));
            if (p.y > 8f)
                _findings.Add(new QaFinding("PLAYER_FLYING",
                    QaCategory.Physics, QaSeverity.Major, _elapsed,
                    $"y={p.y:F1}", p));
            if (_dmg != null && _dmg.Health < 1f)
                _findings.Add(new QaFinding("GODMODE_FAIL",
                    QaCategory.Gameplay, QaSeverity.Critical, _elapsed,
                    $"hp={_dmg.Health:F0}", p));
        }

        void OnCollisionEnter(Collision c)
        {
            if (c.relativeVelocity.magnitude > 3f
                && c.gameObject.GetComponentInParent<Pedestrian>() == null)
                _collisions++;
        }

        // ==================== CAPTURA ====================
        void MaybeCapture(Vector3 p)
        {
            if (_elapsed <= _nextFrame) return;
            _nextFrame = _elapsed + FRAME_EVERY;
            CaptureFrame($"qa_{(int)_elapsed:D4}");
        }

        string CaptureFrame(string name)
        {
            var cam = ActiveCam();
            if (cam == null) return null;
            try
            {
                var rt = new RenderTexture(640, 360, 16);
                cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(640, 360, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 640, 360), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null; rt.Release();
                var px = tex.GetPixels32();
                int magenta = 0, black = 0;
                for (int i = 0; i < px.Length; i += 64)
                { var c = px[i];
                  if (c.r > 200 && c.b > 200 && c.g < 60) magenta++;
                  if (c.r < 15 && c.g < 15 && c.b < 15) black++; }
                if (magenta > px.Length / 64 * 0.15f)
                    _findings.Add(new QaFinding("MISSING_TEXTURE",
                        QaCategory.Rendering, QaSeverity.Major, _elapsed,
                        $"{magenta}px magenta", Vector3.zero));
                if (black > px.Length / 64 * 0.9f)
                    _findings.Add(new QaFinding("BLACK_SCREEN",
                        QaCategory.Rendering, QaSeverity.Critical, _elapsed,
                        "render quase vazio", Vector3.zero));
                var dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                    Application.dataPath, "../Temp/qa_frames"));
                System.IO.Directory.CreateDirectory(dir);
                var path = System.IO.Path.Combine(dir, name + ".png");
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.Destroy(tex);
                return path;
            }
            catch { return null; }
        }

        Camera ActiveCam()
        {
            if (_rig.mode != null
                && _rig.mode.Current == NeonDistrictModeManager.Mode.OnFoot
                && _rig.motor != null && _rig.motor.EyeCamera != null)
                return _rig.motor.EyeCamera;
            return Camera.main != null && Camera.main.enabled
                ? Camera.main : _cam;
        }

        // ==================== RELATÓRIO / SESSÃO ====================
        public string StatusLine()
            => $"[QA-STATUS] {_sessionId} t={_elapsed:F0}s scenario=" +
               $"{(_active != null ? _active.Name : "roam")} " +
               $"findings={_findings.Count} visited={_coverage.Count}/64";

        public string Report()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# QA Session {_sessionId} — {_elapsed:F0}s " +
                $"seed={_seed}");
            sb.AppendLine($"scene={gameObject.scene.name} " +
                $"visited={_coverage.Count}/64 teleports={_teleports} " +
                $"collisions={_collisions} shots={_shots} " +
                $"panicMax={_panicTotal}");
            sb.AppendLine($"fpsAvg={_perf.AvgFps:F0} " +
                $"worstDt={_perf.WorstDt * 1000f:F0}ms " +
                $"exceptions={(_exc != null ? _exc.Count : 0)} " +
                $"recoveries={_recoveries}");
            sb.AppendLine("## Scenarios");
            foreach (var s in _session.scenarios)
                sb.AppendLine($" - {s.result,-12} {s.name}: {s.detail} " +
                    $"({s.duration:F1}s, prep {s.prepTime:F1}s)");
            if (_active != null)
                sb.AppendLine($" - RUNNING      {_active.Name} " +
                    $"({_active.Elapsed:F1}s)");
            sb.AppendLine($"## Findings ({_findings.Count})");
            foreach (var f in _findings.All)
                sb.AppendLine($" - [{f.severity}/{f.category}] {f.id}" +
                    $"@{f.time:F0}s {f.detail}");
            sb.AppendLine("## Perf spikes (top5, post-warmup)");
            foreach (var sp in _perf.Spikes)
                sb.AppendLine($" - dt={sp.dt * 1000f:F0}ms @{sp.at:F0}s " +
                    $"ctx={sp.ctx}");
            sb.Append(_log);
            return sb.ToString();
        }

        /// <summary>Exporta JSON da sessão → evidence/qa/session_*.json.</summary>
        public string ExportSession()
        {
            _session.sessionId = _sessionId;
            _session.seed = _seed;
            _session.scene = gameObject.scene.name;
            _session.startedUtc = System.DateTime.UtcNow.ToString("o");
            _session.duration = _elapsed;
            _session.visitedSectors = _coverage.Count;
            _session.teleports = _teleports;
            _session.collisions = _collisions;
            _session.fpsAvg = _perf.AvgFps;
            _session.worstDtMs = _perf.WorstDt * 1000f;
            _session.exceptions = _exc != null ? _exc.Count : 0;
            foreach (var sp in _perf.Spikes)
                _session.perfSpikes.Add(
                    $"{sp.dt * 1000f:F0}ms@{sp.at:F0}s {sp.ctx}");
            _session.findings.Clear();
            _session.findings.AddRange(_findings.All);
            var dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                Application.dataPath, "../../evidence/qa"));
            var path = QaSessionIO.Write(_session, dir);
            Debug.Log($"[QA-EXPORT] {path}");
            return path;
        }

        void EndSession()
        {
            _ended = true;
            ClearInputs();
            Debug.Log(Report());
            ExportSession();
            enabled = false;
        }

        void OnDestroy()
        {
            ClearInputs();
            _exc?.Dispose();
        }

        // ==================================================================
        //  RIG + CENÁRIOS (adapter NEON DISTRICT)
        // ==================================================================
        class NdRig
        {
            public PlayerCarController car;
            public DriveInputAdapter input;
            public NeonDistrictModeManager mode;
            public OnFootMotor motor;
            public Ak47Weapon weapon;
            public PedestrianSystem pedSys;
            public TrafficManager traffic;
        }

        static bool Driving(NdRig r) =>
            r.mode == null || r.mode.Current ==
                NeonDistrictModeManager.Mode.Driving;

        static Pedestrian NearestPed(NdRig r, Vector3 pos, out float dist)
        {
            Pedestrian best = null; dist = float.MaxValue;
            foreach (var p in Object.FindObjectsByType<Pedestrian>(
                FindObjectsSortMode.None))
                if (p != null && !p.IsDown)
                {
                    float d = Vector3.Distance(p.transform.position, pos);
                    if (d < dist) { dist = d; best = p; }
                }
            return best;
        }

        // ---------- guard de física (regressão histórica) ----------
        class PhysicsSimGuard : QaScenario
        {
            public PhysicsSimGuard()
            { Name = "PhysicsSimGuard"; Timeout = 2f; }
            public override void Tick(QaContext ctx, float dt)
            {
                if (Physics.simulationMode == SimulationMode.FixedUpdate)
                    Pass("simulationMode=FixedUpdate");
                else
                    Fail($"simulationMode={Physics.simulationMode} — " +
                        "regressão DynamicsManager (carro gira mas não anda)");
            }
        }

        // ---------- aceleração por input real ----------
        class DriveAccel : QaScenario
        {
            readonly NdRig _r; Vector3 _p0; bool _reset, _clearChecked;
            public DriveAccel(NdRig r)
            { _r = r; Name = "DriveAccel"; Timeout = 8f; }
            public override PrepStatus TryPrepare(QaContext ctx)
            {
                if (_r.car == null) return PrepStatus.Blocked;
                if (!Driving(_r)) { _r.input?.TriggerEnterExit();
                    return PrepStatus.Working; }
                if (!_reset)
                {   // setup determinístico: FAIXA da avenida (não o centro —
                    // o canteiro tem postes), direção da pista
                    _reset = true;
                    var rough = CityLayout.RoadPoint(false,
                        CityLayout.AVENUE_J, -100f);
                    var pos = rough; var dir = Vector3.right;
                    var net = Object.FindFirstObjectByType<TrafficNetwork>();
                    if (net != null
                        && net.TryGetNearestPose(rough, out pos, out dir))
                    { }
                    _r.car.ResetToPose(pos + Vector3.up * 0.4f,
                        Quaternion.LookRotation(dir));
                    return PrepStatus.Working;
                }
                if (!_clearChecked)
                {   // via obstruída no setup → Blocked, não Fail do powertrain
                    _clearChecked = true;
                    var t = _r.car.transform;
                    if (Physics.SphereCast(t.position + Vector3.up * 0.6f, 1f,
                            t.forward, out var h, 14f, ~0,
                            QueryTriggerInteraction.Ignore))
                    { BlockedReason = $"via obstruída pós-reset: {h.collider.name}";
                      return PrepStatus.Blocked; }
                }
                _p0 = _r.car.transform.position;
                return PrepStatus.Ready;
            }
            public override void Tick(QaContext ctx, float dt)
            {
                _r.input.SetVirtualControl(
                    DriveInputAdapter.DriveControl.Gas, true);
                if (Elapsed < 4f) return;
                float dist = Vector3.Distance(_r.car.transform.position, _p0);
                float fs = _r.car.ForwardSpeed;
                if (dist > 12f && fs > 8f)
                    Pass($"moveu {dist:F1}m, fwdSpeed={fs:F1}m/s");
                else
                    Fail($"gas 4s → dist={dist:F1}m fwd={fs:F1}m/s " +
                        "(esperado >12m e >8m/s)");
            }
        }

        // ---------- esterço por input real ----------
        class DriveSteer : QaScenario
        {
            readonly NdRig _r; float _yaw0;
            public DriveSteer(NdRig r)
            { _r = r; Name = "DriveSteer"; Timeout = 8f; }
            public override PrepStatus TryPrepare(QaContext ctx)
            {
                if (!Driving(_r) || _r.car == null) return PrepStatus.Blocked;
                _yaw0 = _r.car.transform.eulerAngles.y;
                return PrepStatus.Ready;
            }
            // setup já feito pelo DriveAccel (via livre) — sem reset próprio
            public override void Tick(QaContext ctx, float dt)
            {
                _r.input.SetVirtualControl(
                    DriveInputAdapter.DriveControl.Gas, true);
                _r.input.SetVirtualControl(
                    DriveInputAdapter.DriveControl.SteerLeft, true);
                if (Elapsed < 2.5f) return;
                float dy = Mathf.Abs(Mathf.DeltaAngle(_yaw0,
                    _r.car.transform.eulerAngles.y));
                if (dy > 15f) Pass($"yaw Δ={dy:F0}° em 2.5s");
                else Fail($"steer+gas 2.5s → yaw Δ={dy:F0}° (esperado >15°)");
            }
        }

        // ---------- freio ----------
        class DriveBrake : QaScenario
        {
            readonly NdRig _r;
            public DriveBrake(NdRig r)
            { _r = r; Name = "DriveBrake"; Timeout = 6f; }
            public override PrepStatus TryPrepare(QaContext ctx)
                => Driving(_r) && _r.car != null
                    ? PrepStatus.Ready : PrepStatus.Blocked;
            public override void Tick(QaContext ctx, float dt)
            {
                _r.input.SetVirtualControl(
                    DriveInputAdapter.DriveControl.Brake, true);
                if (Elapsed < 3f) return;
                float fs = Mathf.Abs(_r.car.ForwardSpeed);
                if (fs < 2.5f) Pass($"parou: fwd={fs:F1}m/s");
                else Fail($"brake 3s → ainda {fs:F1}m/s");
            }
        }

        // ---------- sair/andar/entrar pelo pipeline real ----------
        class ExitEnterCycle : QaScenario
        {
            readonly NdRig _r; int _stage; Vector3 _p0; float _stageT;
            public ExitEnterCycle(NdRig r)
            { _r = r; Name = "ExitEnter"; Timeout = 30f; }
            public override PrepStatus TryPrepare(QaContext ctx)
            {
                if (_r.mode == null || _r.motor == null)
                { BlockedReason = "mode manager/motor ausente";
                  return PrepStatus.Blocked; }
                if (!Driving(_r))
                {   // já a pé: caminha até o carro e reentra de verdade
                    var to = _r.car.transform.position
                        - _r.motor.transform.position; to.y = 0f;
                    if (to.magnitude < 2.4f)
                    {
                        _r.input?.SetQaFootMove(Vector2.zero);
                        _r.input?.TriggerEnterExit();
                    }
                    else
                    {
                        var local = _r.motor.transform
                            .InverseTransformDirection(to.normalized);
                        _r.input?.SetQaFootMove(new Vector2(
                            Mathf.Clamp(local.x, -1f, 1f),
                            Mathf.Clamp(local.z, -1f, 1f)));
                    }
                    return PrepStatus.Working;
                }
                // para o carro antes de sair
                _r.input.SetVirtualControl(
                    DriveInputAdapter.DriveControl.Brake, true);
                return Mathf.Abs(_r.car.ForwardSpeed) < 0.8f
                    ? PrepStatus.Ready : PrepStatus.Working;
            }
            public override void Tick(QaContext ctx, float dt)
            {
                _stageT += dt;
                switch (_stage)
                {
                    case 0: // exit
                        _r.input.TriggerEnterExit();
                        _stage = 1; _stageT = 0f;
                        break;
                    case 1:
                        if (!Driving(_r)) { _stage = 2; _stageT = 0f;
                            _p0 = _r.motor.transform.position; }
                        else if (_stageT > 3f)
                            Fail("exit: modo continuou Driving após 3s");
                        break;
                    case 2: // anda pra frente 2s
                        _r.input.SetQaFootMove(new Vector2(0f, 1f));
                        if (_stageT > 2f)
                        {
                            float d = Vector3.Distance(
                                _r.motor.transform.position, _p0);
                            if (d < 1.2f)
                                Fail($"a pé não move (Δ={d:F2}m em 2s)");
                            else { _stage = 3; _stageT = 0f;
                                _r.input.SetQaFootMove(Vector2.zero); }
                        }
                        break;
                    case 3: // volta pro carro e entra
                        var to = _r.car.transform.position
                            - _r.motor.transform.position; to.y = 0f;
                        if (to.magnitude < 2.9f) // enterDistance=3.0 no ModeManager
                        {
                            _r.input.SetQaFootMove(Vector2.zero);
                            _r.input.TriggerEnterExit();
                            _stage = 4; _stageT = 0f;
                        }
                        else if (_stageT > 10f)
                            Fail($"não alcançou o carro (d={to.magnitude:F1}m)");
                        else
                        {
                            var local = _r.motor.transform
                                .InverseTransformDirection(to.normalized);
                            _r.input.SetQaFootMove(new Vector2(
                                Mathf.Clamp(local.x, -1f, 1f),
                                Mathf.Clamp(local.z, -1f, 1f)));
                        }
                        break;
                    case 4:
                        if (Driving(_r)) Pass("ciclo sair→andar→entrar OK");
                        else if (_stageT > 3f)
                            Fail("enter: modo continuou OnFoot após 3s");
                        break;
                }
            }
        }

        // ---------- tiro real → pânico (integração ponta a ponta) ----------
        class ShotPanic : QaScenario
        {
            readonly NdRig _r; Pedestrian _victim; int _ammo0;
            float _fireEnd, _obsEnd; bool _fired;
            readonly List<(Pedestrian p, Vector3 pos)> _near = new();
            public ShotPanic(NdRig r)
            { _r = r; Name = "ShotPanic"; Timeout = 12f; PrepTimeout = 35f; }
            public override PrepStatus TryPrepare(QaContext ctx)
            {
                if (_r.weapon == null)
                { BlockedReason = "Ak47Weapon não encontrada";
                  return PrepStatus.Blocked; }
                if (Driving(_r))
                { _r.input?.TriggerEnterExit(); return PrepStatus.Working; }
                // pré-condição: ped vivo a ≤20m do jogador a pé
                var me = _r.motor.transform.position;
                var ped = NearestPed(_r, me, out float d);
                if (ped == null)
                { BlockedReason = "nenhum pedestre vivo na cena";
                  return PrepStatus.Blocked; }
                if (d > 19f)
                {   // caminha em direção ao ped mais próximo
                    var to = ped.transform.position - me; to.y = 0f;
                    var local = _r.motor.transform.InverseTransformDirection(
                        to.normalized);
                    _r.input.SetQaFootMove(new Vector2(
                        Mathf.Clamp(local.x, -1f, 1f),
                        Mathf.Clamp(local.z, -1f, 1f)));
                    return PrepStatus.Working;
                }
                _r.input.SetQaFootMove(Vector2.zero);
                _victim = ped;
                return PrepStatus.Ready;
            }
            public override void Tick(QaContext ctx, float dt)
            {
                if (!_fired)
                {
                    _ammo0 = _r.weapon.Ammo;
                    foreach (var p in Object.FindObjectsByType<Pedestrian>(
                        FindObjectsSortMode.None))
                        if (p != null && !p.IsDown
                            && Vector3.Distance(p.transform.position,
                                _r.motor.transform.position) < 20f)
                            _near.Add((p, p.transform.position));
                    _r.input.SetVirtualControl(
                        DriveInputAdapter.DriveControl.Fire, true);
                    ((AutonomousPlaytester)ctx.Host)._shots++;
                    _fired = true; _fireEnd = Elapsed + 0.5f;
                    return;
                }
                if (Elapsed < _fireEnd)
                {
                    // etapa 2: o sistema processou o disparo?
                    if (_r.weapon.Ammo < _ammo0)
                    {
                        _r.input.SetVirtualControl(
                            DriveInputAdapter.DriveControl.Fire, false);
                        _obsEnd = Elapsed + 4f;
                    }
                    else if (Elapsed > _fireEnd - 0.1f)
                        Fail($"FireHeld 0.5s → ammo {_ammo0}→{_r.weapon.Ammo} " +
                            "(disparo não processado)");
                    return;
                }
                if (_obsEnd == 0f) _obsEnd = Elapsed + 4f;
                // etapa 3: algum ped elegível fugiu de verdade?
                int fled = 0;
                foreach (var (p, p0) in _near)
                {
                    if (p == null || p.IsDown) continue;
                    var anim = p.GetComponentInChildren<Animator>();
                    float moved = Vector3.Distance(p.transform.position, p0);
                    if (anim != null && anim.GetFloat("Speed") > 1.5f
                        && moved > 0.8f) fled++;
                }
                if (fled > 0)
                { Pass($"{fled}/{_near.Count} peds fugiram após " +
                      $"tiro real (ammo {_ammo0}→{_r.weapon.Ammo})"); return; }
                if (Elapsed > _obsEnd)
                {
                    if (_near.Count == 0)
                        Inconclusive("nenhum ped a ≤20m no momento do tiro");
                    else
                        Fail($"tiro processado (ammo {_ammo0}→" +
                            $"{_r.weapon.Ammo}) mas 0/{_near.Count} peds fugiram");
                }
            }
        }

        // ---------- subsistema isolado: ScareAt → pânico ----------
        class ScarePanic : QaScenario
        {
            readonly NdRig _r; float _obsEnd; bool _scared;
            readonly List<(Pedestrian p, Vector3 pos)> _near = new();
            public ScarePanic(NdRig r)
            { _r = r; Name = "ScarePanic"; Timeout = 8f; PrepTimeout = 30f; }
            public override PrepStatus TryPrepare(QaContext ctx)
            {
                if (_r.pedSys == null)
                { BlockedReason = "PedestrianSystem ausente";
                  return PrepStatus.Blocked; }
                var me = ctx.Player().position;
                var ped = NearestPed(_r, me, out float d);
                if (ped == null)
                { BlockedReason = "nenhum pedestre vivo"; return PrepStatus.Blocked; }
                if (d > 19f)
                {   // navega até o ped (carro ou a pé, pelo input real)
                    var to = ped.transform.position - me; to.y = 0f;
                    if (Driving(_r))
                    {
                        float ang = Vector3.SignedAngle(
                            _r.car.transform.forward, to, Vector3.up);
                        _r.input.SetVirtualControl(
                            DriveInputAdapter.DriveControl.SteerLeft, ang < -9f);
                        _r.input.SetVirtualControl(
                            DriveInputAdapter.DriveControl.SteerRight, ang > 9f);
                        _r.input.SetVirtualControl(
                            DriveInputAdapter.DriveControl.Gas, true);
                    }
                    else if (_r.motor != null)
                    {
                        var local = _r.motor.transform
                            .InverseTransformDirection(to.normalized);
                        _r.input.SetQaFootMove(new Vector2(
                            Mathf.Clamp(local.x, -1f, 1f),
                            Mathf.Clamp(local.z, -1f, 1f)));
                    }
                    return PrepStatus.Working;
                }
                _r.input.SetQaFootMove(Vector2.zero);
                return PrepStatus.Ready;
            }
            public override void Tick(QaContext ctx, float dt)
            {
                var me = ctx.Player().position;
                if (!_scared)
                {
                    foreach (var p in Object.FindObjectsByType<Pedestrian>(
                        FindObjectsSortMode.None))
                        if (p != null && !p.IsDown
                            && Vector3.Distance(p.transform.position, me) < 20f)
                            _near.Add((p, p.transform.position));
                    _r.pedSys.ScareAt(me, 20f);
                    _scared = true; _obsEnd = Elapsed + 3f;
                    return;
                }
                int fled = 0;
                foreach (var (p, p0) in _near)
                {
                    if (p == null || p.IsDown) continue;
                    var anim = p.GetComponentInChildren<Animator>();
                    if (anim != null && anim.GetFloat("Speed") > 1.5f
                        && Vector3.Distance(p.transform.position, p0) > 0.8f)
                        fled++;
                }
                if (fled > 0) Pass($"{fled}/{_near.Count} peds fugiram " +
                    "após ScareAt");
                else if (Elapsed > _obsEnd)
                {
                    if (_near.Count == 0)
                        Inconclusive("nenhum ped a ≤20m ao assustar");
                    else
                        Fail($"ScareAt executado, 0/{_near.Count} peds fugiram");
                }
            }
        }

        // ---------- atropelamento: contato real → IsDown ----------
        class RunOverPed : QaScenario
        {
            readonly NdRig _r; Pedestrian _victim; float _contactT; bool _reset;
            public RunOverPed(NdRig r)
            { _r = r; Name = "RunOver"; Timeout = 28f; PrepTimeout = 20f; }
            public override PrepStatus TryPrepare(QaContext ctx)
            {
                if (!Driving(_r)) { _r.input?.TriggerEnterExit();
                    return PrepStatus.Working; }
                _victim = NearestPed(_r, _r.car.transform.position,
                    out float d);
                if (_victim == null)
                { BlockedReason = "nenhum pedestre vivo";
                  return PrepStatus.Blocked; }
                if (!_reset)
                {   // setup: posiciona o carro na via mais próxima do ped,
                    // a ~28m, de frente — atropelamento ainda é gameplay real
                    _reset = true;
                    var vp = _victim.transform.position;
                    var net = Object.FindFirstObjectByType<TrafficNetwork>();
                    Vector3 pos = vp, dir = Vector3.forward;
                    if (net != null)
                        net.TryGetNearestPose(vp, out pos, out dir);
                    // recua ~28m contra a direção da via
                    var spawn = pos - dir * 28f;
                    _r.car.ResetToPose(spawn + Vector3.up * 0.4f,
                        Quaternion.LookRotation(dir));
                    return PrepStatus.Working;
                }
                return PrepStatus.Ready;
            }
            public override void Tick(QaContext ctx, float dt)
            {
                if (_victim == null || _victim.IsDown)
                { Pass($"ped atingido e caiu ({_victim?.name})"); return; }
                var target = _victim.transform.position;
                var to = target - _r.car.transform.position; to.y = 0f;
                float ang = Vector3.SignedAngle(_r.car.transform.forward,
                    to, Vector3.up);
                _r.input.SetVirtualControl(
                    DriveInputAdapter.DriveControl.SteerLeft, ang < -9f);
                _r.input.SetVirtualControl(
                    DriveInputAdapter.DriveControl.SteerRight, ang > 9f);
                _r.input.SetVirtualControl(
                    DriveInputAdapter.DriveControl.Gas, true);
                // contato físico sem queda = falha real
                float d = to.magnitude;
                float spd = _r.car.GetComponent<Rigidbody>()
                    .linearVelocity.magnitude;
                if (d < 2.4f && spd > 2.5f) _contactT += dt;
                else _contactT = 0f;
                if (_contactT > 1.5f)
                    Fail($"contato a {spd:F0}m/s por 1.5s e ped segue vivo");
            }
            public override void OnTimeout(QaContext ctx)
                => Inconclusive("não alcançou o pedestre em 28s");
        }
    }
}
