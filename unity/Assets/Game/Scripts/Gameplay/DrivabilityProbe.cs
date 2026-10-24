using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Studio.Input;

namespace Studio.Gameplay
{
    /// <summary>
    /// DrivabilityProbe (missão v3.8 M2.3) — teste PERMANENTE de dirigibilidade.
    /// Rota de waypoints cobrindo TODAS as vias (grade 6×6 + N-S do OBD,
    /// passo ~10 m), carro PROMETEO dirigido pelo canal REAL de input
    /// (SetVirtualControl — mesmos botões touch), física real, sem noclip.
    ///
    /// Bloqueio = velocidade horizontal < 0,7 m/s com Gas por > 2 s.
    /// O colisor de contato é classificado:
    ///   LEGITIMO       — obstáculo com renderer ativo a ≤1,5 m do impacto
    ///   INVISIVEL      — collider sem renderer próximo (violação)
    ///   VISIVEL_NA_VIA — objeto visível, mas parado no corredor de via
    ///                    (ex.: poste na pista — classe de bug própria)
    ///
    /// Saída: console [DRV-STATUS]/[DRV-DONE] + Temp/drivability_report.json.
    /// Runner: tools/qa/drivability_check.py (padrão editor vivo).
    ///
    /// OBS: geometria da malha espelha NeonDistrictSceneBuilder (fontes Editor
    /// não podem ser referenciadas por Studio.Gameplay). Guarda de sanidade:
    /// waypoint sem chão (raycast 4 m) é pulado e registrado.
    /// </summary>
    public class DrivabilityProbe : MonoBehaviour
    {
        // ---- malha viária (espelho de NeonDistrictSceneBuilder) ----
        const int Lines = 6;
        const float Pitch = 52f;
        const float StreetW = 12f, AvenueW = 16f;
        const int AvenueJ = 3;
        const float XMin = -147f, XMax = 284f, ZMin = -147f, ZMax = 147f;
        static readonly float[] ObdNs = { 182f, 234f };

        // ---- parâmetros do teste ----
        const float WpStep = 10f;
        const float SettleS = 0.7f;
        const float BlockSpeed = 0.7f;
        const float BlockTime = 2.0f;
        const float WarmupS = 2.0f;       // ignora os primeiros s de aceleração
        const float RecoverSkipM = 20f;   // teleporta N metros à frente do bloqueio
        const float DeviationM = 25f;     // desvio lateral máximo do eixo da via
        const float BudgetS = 900f;       // auto-fim
        const int MaxBlocksPerSeg = 10;

        public static bool OnRoadway(float x, float z)
        {
            var lats = LineCoords();
            for (int j = 0; j < Lines; j++)
            {
                float hw = (j == AvenueJ ? AvenueW : StreetW) * 0.5f;
                if (Mathf.Abs(z - lats[j]) < hw && x > XMin && x < XMax) return true;
            }
            for (int i = 0; i < Lines; i++)
                if (Mathf.Abs(x - lats[i]) < StreetW * 0.5f && z > ZMin && z < ZMax) return true;
            foreach (var sx in ObdNs)
                if (Mathf.Abs(x - sx) < StreetW * 0.5f && z > ZMin && z < ZMax) return true;
            return false;
        }

        static float[] LineCoords()
        {
            var a = new float[Lines];
            for (int i = 0; i < Lines; i++) a[i] = (i - (Lines - 1) * 0.5f) * Pitch;
            return a;
        }

        class Segment
        {
            public string id;
            public Vector3 axis;            // direção de direção
            public List<Vector3> wps = new();
        }

        readonly List<Segment> _segs = new();
        int _seg;
        enum St { NextSeg, Settle, Drive, Done }
        St _st;
        float _t, _elapsed, _driveT, _blockT;
        int _wp, _segBlocks;

        PlayerCarController _car;
        Rigidbody _rb;
        VehicleDamageController _dmg;
        DriveInputAdapter _input;

        // ---- resultados ----
        class Block
        {
            public float[] pos; public string seg, cls, collider, path; public float dist;
        }
        readonly List<Block> _blocks = new();
        readonly List<float[]> _skipped = new();
        readonly List<float[]> _fell = new();
        int _drivenWps;
        public int WaypointCount { get; private set; }
        public int TotalSegments => _segs.Count;
        bool _done;

        const DriveInputAdapter.DriveControl G = DriveInputAdapter.DriveControl.Gas;
        const DriveInputAdapter.DriveControl B = DriveInputAdapter.DriveControl.Brake;
        const DriveInputAdapter.DriveControl SL = DriveInputAdapter.DriveControl.SteerLeft;
        const DriveInputAdapter.DriveControl SR = DriveInputAdapter.DriveControl.SteerRight;

        void Awake()
        {
            _car = GetComponent<PlayerCarController>();
            _rb = GetComponent<Rigidbody>();
            _dmg = GetComponent<VehicleDamageController>();
            if (_dmg != null) _dmg.IsGodModeActive = true;
            _input = DriveInputAdapter.Instance;
            BuildRoute();
        }

        void BuildRoute()
        {
            var lats = LineCoords();
            for (int j = 0; j < Lines; j++)
            {
                var seg = new Segment { id = $"EW_z{lats[j]:0}", axis = Vector3.right };
                for (float x = -135f; x <= 275f + 0.1f; x += WpStep)
                    seg.wps.Add(new Vector3(x, 0f, lats[j]));
                _segs.Add(seg);
            }
            var ns = new List<float>(lats);
            ns.AddRange(ObdNs);
            foreach (var x in ns)
            {
                var seg = new Segment { id = $"NS_x{x:0}", axis = Vector3.forward };
                for (float z = -125f; z <= 125f + 0.1f; z += WpStep)
                    seg.wps.Add(new Vector3(x, 0f, z));
                _segs.Add(seg);
            }
            foreach (var s in _segs) WaypointCount += s.wps.Count;
        }

        void Update()
        {
            if (_done) return;
            _elapsed += Time.deltaTime;
            if (_elapsed > BudgetS) { Finish("budget"); return; }

            switch (_st)
            {
                case St.NextSeg: NextSegment(); break;
                case St.Settle:
                    _t += Time.deltaTime;
                    if (_rb != null)
                    {
                        _rb.linearVelocity = Vector3.zero;
                        _rb.angularVelocity = Vector3.zero;
                    }
                    if (_t >= SettleS) { _st = St.Drive; _driveT = 0f; _blockT = 0f; }
                    break;
                case St.Drive: DriveStep(); break;
            }
        }

        void NextSegment()
        {
            ClearInput();
            _seg++;
            if (_seg >= _segs.Count) { Finish("completo"); return; }
            var seg = _segs[_seg];
            _segBlocks = 0;
            bool placed = false;
            for (int i = 0; i < seg.wps.Count && !placed; i++)
            {
                _wp = i;
                placed = TeleportTo(seg.wps[i]);
            }
            if (!placed)
            {
                Debug.Log($"[DRV-STATUS] seg {seg.id} SEM CHÃO (pulado)");
                return; // continua em NextSeg — próximo Update avança segmento
            }
            _st = St.Settle;
            _t = 0f;
            Debug.Log($"[DRV-STATUS] seg={_seg + 1}/{_segs.Count} {seg.id} " +
                      $"blocks={_blocks.Count} inv={CountInv()}");
        }

        bool TeleportTo(Vector3 wp)
        {
            if (!Physics.Raycast(wp + Vector3.up * 4f, Vector3.down, out var gHit, 8f))
            {
                _skipped.Add(new[] { wp.x, wp.z });
                return false;
            }
            var pos = new Vector3(wp.x, gHit.point.y + 0.6f, wp.z);
            var seg = _segs[_seg];
            float yaw = seg.axis == Vector3.right ? 90f : 0f;
            // se o carro nasce dentro de um collider, empurra lateralmente
            // (esfera a +1,25 m: livre do chão, mas pega muros/postes 2 m)
            for (int i = 0; i < 5; i++)
            {
                if (!Physics.CheckSphere(pos + Vector3.up * 1.25f, 1.0f,
                        ~0, QueryTriggerInteraction.Ignore) || i == 4)
                {
                    if (_rb != null)
                    {
                        _rb.position = pos;
                        _rb.rotation = Quaternion.Euler(0f, yaw, 0f);
                        _rb.linearVelocity = Vector3.zero;
                        _rb.angularVelocity = Vector3.zero;
                    }
                    transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
                    return true;
                }
                pos += Vector3.Cross(seg.axis, Vector3.up) * (i % 2 == 0 ? 1.6f : -3.2f);
            }
            return true;
        }

        void DriveStep()
        {
            var seg = _segs[_seg];
            _driveT += Time.deltaTime;
            float dt = Time.deltaTime;

            // avança waypoints alcançados
            while (_wp < seg.wps.Count - 1 &&
                   HorizDist(transform.position, seg.wps[_wp]) < 6f) { _wp++; _drivenWps++; }

            var target = seg.wps[Mathf.Min(_wp + 1, seg.wps.Count - 1)];
            var to = target - transform.position; to.y = 0f;
            float ang = Vector3.SignedAngle(transform.forward, to, Vector3.up);

            _input.SetVirtualControl(SL, ang < -8f);
            _input.SetVirtualControl(SR, ang > 8f);
            _input.SetVirtualControl(G, true);
            _input.SetVirtualControl(B, false);

            // guardas de estado
            if (transform.position.y < -5f)
            {
                _fell.Add(new[] { transform.position.x, transform.position.z });
                Debug.Log($"[DRV-INFO] QUEDA @{transform.position.x:F1},{transform.position.z:F1} seg={seg.id}");
                Recover();
                return;
            }
            if (Vector3.Dot(transform.up, Vector3.up) < 0.2f)
            {
                var b = MakeBlock(transform.position, seg, "CAPOTOU", transform.position, 0f);
                _blocks.Add(b);
                Debug.Log($"[DRV-INFO] capotou @{b.pos[0]:F1},{b.pos[1]:F1} seg={seg.id}");
                Recover();
                return;
            }
            var lateralOff = Mathf.Abs(Vector3.Dot(transform.position - seg.wps[0],
                Vector3.Cross(seg.axis, Vector3.up)));
            if (lateralOff > DeviationM)
            {
                Debug.Log($"[DRV-INFO] desvio {lateralOff:F0}m seg={seg.id} — recover");
                Recover();
                return;
            }

            // detecção de bloqueio
            float hs = HorizSpeed();
            if (_driveT > WarmupS && hs < BlockSpeed)
            {
                _blockT += dt;
                if (_blockT >= BlockTime)
                {
                    _blockT = 0f;
                    RegisterBlock(seg);
                    return;
                }
            }
            else _blockT = 0f;

            // fim de segmento
            if (_wp >= seg.wps.Count - 1 &&
                HorizDist(transform.position, seg.wps[_wp]) < 6f)
            {
                ClearInput();
                _st = St.NextSeg;
            }
        }

        void RegisterBlock(Segment seg)
        {
            _segBlocks++;
            var (cls, colName, colPath, hitPt, dist) = Classify();
            var b = MakeBlock(transform.position, seg, cls, hitPt, dist);
            b.collider = colName; b.path = colPath;
            _blocks.Add(b);
            Debug.Log($"[DRV-BLOCK] {cls} '{colName}' @{b.pos[0]:F1},{b.pos[1]:F1} " +
                      $"seg={seg.id} dist={dist:F1} (#{_blocks.Count})");
            if (_segBlocks >= MaxBlocksPerSeg)
            {
                Debug.Log($"[DRV-STATUS] seg {seg.id} abortado ({MaxBlocksPerSeg}+ blocks)");
                ClearInput();
                _st = St.NextSeg;
                return;
            }
            Recover();
        }

        void Recover()
        {
            ClearInput();
            var seg = _segs[_seg];
            var p = transform.position;
            float along = Vector3.Dot(p - seg.wps[0], seg.axis);
            // pula RecoverSkipM à frente; se não houver chão, segue adiante
            for (float a = along + RecoverSkipM; a < seg.wps.Count * WpStep; a += WpStep)
            {
                var wp = seg.wps[0] + seg.axis * a;
                _wp = Mathf.Clamp(Mathf.RoundToInt(a / WpStep), 0, seg.wps.Count - 1);
                if (TeleportTo(seg.wps[_wp])) { _st = St.Settle; _t = 0f; return; }
            }
            _st = St.NextSeg;
        }

        (string, string, string, Vector3, float) Classify()
        {
            var t = transform;
            foreach (var h in new[] { 0.55f, 1.1f, 1.7f })
            {
                if (Physics.Raycast(t.position + Vector3.up * h, t.forward,
                        out var hit, 8f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform.IsChildOf(t)) continue;
                    bool visible = HasVisualNear(hit.collider, hit.point);
                    bool onRoad = OnRoadway(hit.point.x, hit.point.z);
                    string cls = visible ? (onRoad ? "VISIVEL_NA_VIA" : "LEGITIMO")
                                         : "INVISIVEL";
                    string path = FullPath(hit.collider.transform);
                    return (cls, hit.collider.name, path, hit.point, hit.distance);
                }
            }
            return ("SEM_RAYCAST", "-", "-", transform.position, 0f);
        }

        static bool HasVisualNear(Collider c, Vector3 point)
        {
            // renderer no próprio GO ou filhos (enabled/ativo)
            foreach (var r in c.GetComponentsInChildren<Renderer>())
                if (r != null && r.enabled && r.gameObject.activeInHierarchy
                    && r.bounds.SqrDistance(point) < 1.5f * 1.5f)
                    return true;
            // renderer no pai imediato (colliders irmãos do visual — padrão
            // _BoundsCollider: collider é IRMÃO do mesh, não filho)
            var p = c.transform.parent;
            if (p != null)
                foreach (var r in p.GetComponentsInChildren<Renderer>())
                    if (r != null && r.enabled && r.gameObject.activeInHierarchy
                        && r.bounds.SqrDistance(point) < 1.5f * 1.5f)
                        return true;
            return false;
        }

        static Block MakeBlock(Vector3 pos, Segment seg, string cls,
            Vector3 hitPt, float dist) => new()
        {
            pos = new[] { pos.x, pos.z },
            seg = seg.id,
            cls = cls,
            collider = "-",
            path = "-",
            dist = dist
        };

        static string FullPath(Transform t)
        {
            var sb = new StringBuilder(t.name);
            var c = t.parent;
            int guard = 0;
            while (c != null && guard++ < 12) { sb.Insert(0, c.name + "/"); c = c.parent; }
            return sb.ToString();
        }

        float HorizSpeed()
        {
            var v = _rb != null ? _rb.linearVelocity : Vector3.zero;
            v.y = 0f;
            return v.magnitude;
        }

        static float HorizDist(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }

        void ClearInput()
        {
            _input?.ClearVirtual();
        }

        int CountInv()
        {
            int n = 0;
            foreach (var b in _blocks) if (b.cls == "INVISIVEL") n++;
            return n;
        }

        void Finish(string reason)
        {
            if (_done) return;
            _done = true;
            _st = St.Done;
            ClearInput();
            var sb = new StringBuilder();
            sb.Append("{\"finished\":\"").Append(reason).Append("\",\"elapsed\":")
              .Append(_elapsed.ToString("0.#",
                  System.Globalization.CultureInfo.InvariantCulture))
              .Append(",\"waypoints\":").Append(WaypointCount)
              .Append(",\"drivenWaypoints\":").Append(_drivenWps)
              .Append(",\"segments\":").Append(TotalSegments)
              .Append(",\"blocksTotal\":").Append(_blocks.Count)
              .Append(",\"invisible\":").Append(CountInv())
              .Append(",\"blocks\":[");
            for (int i = 0; i < _blocks.Count; i++)
            {
                var b = _blocks[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"pos\":[").Append(F(b.pos[0])).Append(',').Append(F(b.pos[1]))
                  .Append("],\"seg\":\"").Append(b.seg).Append("\",\"cls\":\"").Append(b.cls)
                  .Append("\",\"collider\":\"").Append(b.collider).Append("\",\"path\":\"")
                  .Append(b.path).Append("\",\"dist\":").Append(F(b.dist)).Append('}');
            }
            sb.Append("],\"skippedNoGround\":").Append(_skipped.Count)
              .Append(",\"fell\":").Append(_fell.Count).Append('}');

            var json = sb.ToString();
            try
            {
                var dir = Path.Combine(Directory.GetCurrentDirectory(), "Temp");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "drivability_report.json"), json);
            }
            catch (System.Exception e) { Debug.LogWarning("[DRV] io: " + e.Message); }
            Debug.Log("[DRV-DONE] ===DRV_BEGIN===" + json + "===DRV_END===");
            Debug.Log($"[DRV-RESUMO] blocks={_blocks.Count} invisivel={CountInv()} " +
                      $"vivelNaVia={CountClass("VISIVEL_NA_VIA")} " +
                      $"legitimo={CountClass("LEGITIMO")} semRay={CountClass("SEM_RAYCAST")} " +
                      $"semChao={_skipped.Count} quedas={_fell.Count} motivo={reason}");
        }

        int CountClass(string c)
        {
            int n = 0;
            foreach (var b in _blocks) if (b.cls == c) n++;
            return n;
        }

        static string F(float v) => v.ToString("0.##",
            System.Globalization.CultureInfo.InvariantCulture);
    }
}
