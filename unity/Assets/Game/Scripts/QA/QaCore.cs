using System;
using System.Collections.Generic;
using UnityEngine;

namespace Studio.QA
{
    /// <summary>
    /// Núcleo universal de QA autônomo — independente de jogo/gênero.
    /// Define estados de resultado, modelo de achados, contrato de cenário
    /// (pré-condições → ações → observação → veredito) e sessão serializável.
    /// Adapters por jogo implementam QaScenario e registram detectores.
    /// </summary>
    public enum QaResult
    {
        Pending = 0,
        Pass = 1,          // comportamento esperado comprovado em cenário válido
        Fail = 2,          // comportamento incorreto observado em cenário válido
        Blocked = 3,       // dependência impediu a execução
        Invalid = 4,       // pré-condição essencial não satisfeita → sem conclusão
        Inconclusive = 5   // evidências insuficientes (timeout, ruído)
    }

    public enum QaSeverity { Info, Minor, Major, Critical }
    public enum QaCategory
    {
        Physics, Ai, Rendering, Audio, Ui, State,
        Performance, Gameplay, Stability, Navigation
    }

    public enum PrepStatus { Ready, Working, Blocked }

    /// <summary>Achado deduplicado do detector contínuo ou de um cenário.</summary>
    [Serializable]
    public class QaFinding
    {
        public string id;
        public string category;
        public string severity;
        public float time;
        public string detail;
        public Vector3 pos;
        public string status;       // confirmed | suspect | limited
        public string scenario = "";

        public QaFinding() { }
        public QaFinding(string id, QaCategory cat, QaSeverity sev,
            float t, string detail, Vector3 pos, string status = "suspect")
        {
            this.id = id; category = cat.ToString(); severity = sev.ToString();
            time = t; this.detail = detail; this.pos = pos; this.status = status;
        }
    }

    /// <summary>Registro de um cenário executado (ou abortado) na sessão.</summary>
    [Serializable]
    public class QaScenarioRecord
    {
        public string name;
        public string result;
        public string detail;
        public float duration;
        public float prepTime;
    }

    /// <summary>Snapshot serializável da sessão inteira (JSON → evidence/qa).</summary>
    [Serializable]
    public class QaSession
    {
        public string sessionId;
        public int seed;
        public string scene;
        public string startedUtc;
        public float duration;
        public int visitedSectors;
        public int teleports;
        public int collisions;
        public float fpsAvg;
        public float worstDtMs;
        public int exceptions;
        public List<string> perfSpikes = new();
        public List<QaScenarioRecord> scenarios = new();
        public List<QaFinding> findings = new();
        public List<string> notes = new();
    }

    /// <summary>
    /// Serviços que o host (adapter do jogo) oferece aos cenários:
    /// tempo, flags, log, captura de evidência. Zero acoplamento com gameplay.
    /// </summary>
    public class QaContext
    {
        public MonoBehaviour Host;
        public float Time;
        public int Seed;
        public Func<Transform> Player;
        public Action<QaFinding> Flag;
        public Action<string> Log;
        public Func<string, string> Capture; // nome → path ou null se indisponível
    }

    /// <summary>
    /// Cenário de QA: prepara condições (TryPrepare), age via input/estado real
    /// (Tick), observa resultado e declara veredito (Evaluate/OnTimeout).
    /// Contrato: NUNCA declarar Pass sem observar o comportamento esperado;
    /// NUNCA declarar Fail se a pré-condição essencial não foi satisfeita.
    /// </summary>
    public abstract class QaScenario
    {
        public string Name = "scenario";
        public float PrepTimeout = 25f;
        public float Timeout = 30f;
        public float PrepElapsed;
        public float Elapsed;
        public QaResult Result = QaResult.Pending;
        public string Detail = "";
        public string BlockedReason = "";

        /// <summary>Estabelece pré-condições. Chamado a cada frame até Ready/Blocked.</summary>
        public virtual PrepStatus TryPrepare(QaContext ctx) => PrepStatus.Ready;

        /// <summary>Executa o cenário (inputs reais, espera, verificação parcial).</summary>
        public virtual void Tick(QaContext ctx, float dt) { }

        /// <summary>True quando o cenário terminou de observar (antes do timeout).</summary>
        public virtual bool IsDone(QaContext ctx) => Result != QaResult.Pending;

        /// <summary>Chamado ao estourar o timeout — default: Inconclusive.</summary>
        public virtual void OnTimeout(QaContext ctx)
        {
            if (Result == QaResult.Pending)
            { Result = QaResult.Inconclusive; Detail = $"timeout {Timeout:F0}s"; }
        }

        protected void Pass(string detail) { Result = QaResult.Pass; Detail = detail; }
        protected void Fail(string detail) { Result = QaResult.Fail; Detail = detail; }
        protected void Inconclusive(string d) { Result = QaResult.Inconclusive; Detail = d; }
        public QaScenarioRecord Record() => new QaScenarioRecord
        {
            name = Name, result = Result.ToString(), detail = Detail,
            duration = Elapsed, prepTime = PrepElapsed
        };
    }

    /// <summary>Dedup de achados por (id, bucket temporal).</summary>
    public class FindingLog
    {
        private readonly HashSet<string> _seen = new();
        private readonly List<QaFinding> _all = new();
        public IReadOnlyList<QaFinding> All => _all;
        public int Count => _all.Count;

        /// <summary>bucketSecs agrupa repetições do mesmo defeito.</summary>
        public void Add(QaFinding f, float bucketSecs = 20f)
        {
            var k = f.id + ":" + (int)(f.time / bucketSecs) + ":" + f.detail;
            if (_seen.Add(k)) _all.Add(f);
        }
    }

    /// <summary>Grade de cobertura espacial genérica (n setores²).</summary>
    public class CoverageGrid
    {
        private readonly HashSet<int> _visited = new();
        private readonly float _origin, _size;
        private readonly int _n;
        public int Count => _visited.Count;
        public int Total => _n * _n;
        public float Ratio => _visited.Count / (float)(_n * _n);

        public CoverageGrid(int sectors = 8, float worldSpan = 160f)
        { _n = sectors; _origin = -worldSpan; _size = worldSpan * 2f / sectors; }

        public void Track(Vector3 p)
        {
            int cx = Mathf.Clamp((int)((p.x - _origin) / _size), 0, _n - 1);
            int cz = Mathf.Clamp((int)((p.z - _origin) / _size), 0, _n - 1);
            _visited.Add(cx * _n + cz);
        }
    }

    /// <summary>Métricas de frame: warmup, picos com contexto, média.</summary>
    public class PerfTracker
    {
        private readonly float _warmup;
        private readonly List<(float dt, float at, string ctx)> _spikes = new();
        private float _acc; private int _n; private float _worst;
        public float WorstDt => _worst;
        public float AvgFps => _n > 0 ? _n / Mathf.Max(0.001f, _acc) : 0f;
        public IReadOnlyList<(float dt, float at, string ctx)> Spikes => _spikes;

        public PerfTracker(float warmupSecs = 10f) { _warmup = warmupSecs; }

        public void Tick(float unscaledDt, float elapsed, string context)
        {
            _acc += unscaledDt; _n++;
            if (elapsed < _warmup) return;
            if (unscaledDt > _worst) _worst = unscaledDt;
            if (unscaledDt > 0.1f)
            {
                _spikes.Add((unscaledDt, elapsed, context ?? ""));
                _spikes.Sort((a, b) => b.dt.CompareTo(a.dt));
                if (_spikes.Count > 5) _spikes.RemoveAt(_spikes.Count - 1);
            }
        }
    }

    /// <summary>Escuta exceptions do log do Unity (qualquer jogo).</summary>
    public class ExceptionWatchdog : IDisposable
    {
        private readonly HashSet<string> _seen = new();
        private readonly Action<string, string> _onException;
        public int Count { get; private set; }

        public ExceptionWatchdog(Action<string, string> onException)
        {
            _onException = onException;
            Application.logMessageReceived += OnLog;
        }

        private void OnLog(string msg, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error) return;
            var key = msg.Length > 80 ? msg.Substring(0, 80) : msg;
            if (!_seen.Add(key)) return;
            Count++;
            _onException?.Invoke(msg, stack);
        }

        public void Dispose() => Application.logMessageReceived -= OnLog;
    }

    /// <summary>Grava a sessão em JSON (JsonUtility — campos públicos).</summary>
    public static class QaSessionIO
    {
        public static string Write(QaSession s, string absoluteDir)
        {
            System.IO.Directory.CreateDirectory(absoluteDir);
            var path = System.IO.Path.Combine(absoluteDir,
                $"session_{s.sessionId}.json");
            System.IO.File.WriteAllText(path, JsonUtility.ToJson(s, true));
            return path;
        }
    }
}
