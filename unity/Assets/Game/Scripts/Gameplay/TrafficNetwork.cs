using System.Collections.Generic;
using UnityEngine;

namespace Studio.Gameplay
{
    /// <summary>Aresta dirigida da rede de tráfego: polilinha + arestas seguintes.</summary>
    [System.Serializable]
    public class TrafficEdge
    {
        public Vector3[] points;
        public int[] next;

        private float _length = -1f;

        public float Length
        {
            get
            {
                if (_length < 0f)
                {
                    float l = 0f;
                    for (int i = 1; i < points.Length; i++)
                        l += Vector3.Distance(points[i - 1], points[i]);
                    _length = l;
                }
                return _length;
            }
        }

        /// <summary>Ponto sobre a polilinha a uma distância (arc-length) do início.</summary>
        public Vector3 PointAtDistance(float dist)
        {
            if (points.Length == 0) return Vector3.zero;
            if (dist <= 0f) return points[0];
            float acc = 0f;
            for (int i = 1; i < points.Length; i++)
            {
                float seg = Vector3.Distance(points[i - 1], points[i]);
                if (acc + seg >= dist)
                {
                    float t = seg > 0.0001f ? (dist - acc) / seg : 0f;
                    return Vector3.Lerp(points[i - 1], points[i], t);
                }
                acc += seg;
            }
            return points[points.Length - 1];
        }

        /// <summary>Direção normalizada num ponto da polilinha.</summary>
        public Vector3 DirectionAtDistance(float dist)
        {
            if (points.Length < 2) return Vector3.forward;
            float acc = 0f;
            for (int i = 1; i < points.Length; i++)
            {
                float seg = Vector3.Distance(points[i - 1], points[i]);
                if (acc + seg >= dist || i == points.Length - 1)
                {
                    var d = points[i] - points[i - 1];
                    d.y = 0f;
                    return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward;
                }
                acc += seg;
            }
            return Vector3.forward;
        }
    }

    /// <summary>
    /// Rede dirigida de vias do distrito (grafo de arestas com conectores de cruzamento).
    /// Construída deterministicamente pelo Scene Builder via <see cref="TrafficNetworkBuilder"/>.
    /// </summary>
    public class TrafficNetwork : MonoBehaviour
    {
        [SerializeField] private List<TrafficEdge> _edges = new List<TrafficEdge>();

        public int EdgeCount => _edges.Count;
        public IReadOnlyList<TrafficEdge> Edges => _edges;

        public void SetEdges(List<TrafficEdge> edges)
        {
            _edges = edges ?? new List<TrafficEdge>();
        }

        public TrafficEdge GetEdge(int index) => _edges[index];

        /// <summary>Escolhe a próxima aresta usando RNG injetado (determinístico).</summary>
        public int PickNext(int edgeIndex, System.Random rng)
        {
            if (edgeIndex < 0 || edgeIndex >= _edges.Count) return -1;
            var next = _edges[edgeIndex].next;
            if (next == null || next.Length == 0 || rng == null) return -1;
            return next[rng.Next(next.Length)];
        }

        /// <summary>Encontra a pose (posição+direção) na aresta mais próxima de pos.</summary>
        public bool TryGetNearestPose(Vector3 pos, out Vector3 posePos, out Vector3 poseDir)
        {
            float best = float.MaxValue;
            posePos = pos;
            poseDir = Vector3.forward;
            bool found = false;
            for (int i = 0; i < _edges.Count; i++)
            {
                var e = _edges[i];
                if (e.points == null || e.points.Length == 0) continue;
                float len = e.Length;
                for (int s = 0; s <= 4; s++)
                {
                    var p = e.PointAtDistance(len * s / 4f);
                    float d = Vector3.Distance(p, pos);
                    if (d < best)
                    {
                        best = d;
                        found = true;
                        posePos = p;
                        poseDir = e.DirectionAtDistance(len * s / 4f);
                    }
                }
            }
            return found;
        }

        /// <summary>
        /// Encontra (edgeIndex, arcDistance) da projeção mais próxima de pos
        /// sobre todas as polilinhas — usado para re-rotear um veículo após
        /// um impacto físico (ele volta à via mais próxima).
        /// </summary>
        public bool FindNearestEdge(Vector3 pos, out int edgeIndex, out float dist)
        {
            edgeIndex = -1;
            dist = 0f;
            float best = float.MaxValue;
            for (int i = 0; i < _edges.Count; i++)
            {
                var pts = _edges[i].points;
                if (pts == null || pts.Length < 2) continue;
                float acc = 0f;
                for (int s = 1; s < pts.Length; s++)
                {
                    Vector3 a = pts[s - 1], b = pts[s];
                    Vector3 ab = b - a;
                    float segLen = ab.magnitude;
                    float t = segLen > 0.0001f
                        ? Mathf.Clamp01(Vector3.Dot(pos - a, ab) / (segLen * segLen)) : 0f;
                    float d = Vector3.Distance(pos, a + ab * t);
                    if (d < best)
                    {
                        best = d;
                        edgeIndex = i;
                        dist = acc + t * segLen;
                    }
                    acc += segLen;
                }
            }
            return edgeIndex >= 0;
        }
    }

    /// <summary>
    /// Constrói a rede dirigida de tráfego em mão direita para a grade do distrito.
    /// Ruas N-S em xs[i], ruas E-W em zs[j]. Vias têm metades de largura
    /// roadHalf[i/j] e offsets de faixa lane[i/j]. arestas: segmento entre
    /// cruzamentos + conectores (reto/direita/esquerda — sem retorno).
    /// </summary>
    public static class TrafficNetworkBuilder
    {
        /// <summary>
        /// avenueIndex: índice (em zs) da avenida E-W (pode ser -1 = nenhuma).
        /// avenueLaneOffset: offset de faixa da avenida.
        /// </summary>
        public static List<TrafficEdge> BuildGrid(
            float[] xs, float[] zs, float streetHalfWidth, float laneOffset,
            int avenueIndex, float avenueHalfWidth, float avenueLaneOffset)
        {
            int n = xs.Length;
            int m = zs.Length;
            var edges = new List<TrafficEdge>();

            float LaneNS(int i) => laneOffset;
            float LaneEW(int j) => (j == avenueIndex) ? avenueLaneOffset : laneOffset;
            float HalfNS(int i) => streetHalfWidth + 1.5f;
            float HalfEW(int j) => ((j == avenueIndex) ? avenueHalfWidth : streetHalfWidth) + 1.5f;

            // laneEdge[i,j,dir] — índice da aresta de faixa que vai do cruzamento
            // (i,j) ao próximo na direção. dir: 0=N(+Z),1=S(-Z),2=E(+X),3=W(-X).
            int tMax = Mathf.Max(n, m); // assimétrico: alvo é índice do outro eixo
            int[,,,] laneEdge = new int[n, m, 4, tMax]; // [i,j,dir,targetIndex]
            for (int i = 0; i < n; i++)
                for (int j = 0; j < m; j++)
                    for (int d = 0; d < 4; d++)
                        for (int t = 0; t < tMax; t++)
                            laneEdge[i, j, d, t] = -1;

            var queue = new List<(int edge, int endI, int endJ, int heading)>();

            // --- Segmentos N-S ---
            for (int i = 0; i < n; i++)
            {
                float xN = xs[i] + LaneNS(i); // northbound (mão direita)
                float xS = xs[i] - LaneNS(i); // southbound
                for (int j = 0; j < m - 1; j++)
                {
                    // Northbound j -> j+1
                    laneEdge[i, j, 0, j + 1] = edges.Count;
                    edges.Add(new TrafficEdge
                    {
                        points = new[] {
                            new Vector3(xN, 0f, zs[j] + HalfEW(j)),
                            new Vector3(xN, 0f, zs[j + 1] - HalfEW(j + 1)) },
                    });
                    queue.Add((laneEdge[i, j, 0, j + 1], i, j + 1, 0));

                    // Southbound j+1 -> j
                    laneEdge[i, j + 1, 1, j] = edges.Count;
                    edges.Add(new TrafficEdge
                    {
                        points = new[] {
                            new Vector3(xS, 0f, zs[j + 1] - HalfEW(j + 1)),
                            new Vector3(xS, 0f, zs[j] + HalfEW(j)) },
                    });
                    queue.Add((laneEdge[i, j + 1, 1, j], i, j, 1));
                }
            }

            // --- Segmentos E-W ---
            for (int j = 0; j < m; j++)
            {
                float zE = zs[j] - LaneEW(j); // eastbound (mão direita)
                float zW = zs[j] + LaneEW(j); // westbound
                for (int i = 0; i < n - 1; i++)
                {
                    // Eastbound i -> i+1
                    laneEdge[i, j, 2, i + 1] = edges.Count;
                    edges.Add(new TrafficEdge
                    {
                        points = new[] {
                            new Vector3(xs[i] + HalfNS(i), 0f, zE),
                            new Vector3(xs[i + 1] - HalfNS(i + 1), 0f, zE) },
                    });
                    queue.Add((laneEdge[i, j, 2, i + 1], i + 1, j, 2));

                    // Westbound i+1 -> i
                    laneEdge[i + 1, j, 3, i] = edges.Count;
                    edges.Add(new TrafficEdge
                    {
                        points = new[] {
                            new Vector3(xs[i + 1] - HalfNS(i + 1), 0f, zW),
                            new Vector3(xs[i] + HalfNS(i), 0f, zW) },
                    });
                    queue.Add((laneEdge[i + 1, j, 3, i], i, j, 3));
                }
            }

            // --- Conectores nos cruzamentos ---
            // heading: 0=N(+Z),1=S(-Z),2=E(+X),3=W(-X)
            // turnRight/left: novos headings relativos:
            //  N: straight=N,right=E,left=W   S: straight=S,right=W,left=E
            //  E: straight=E,right=S,left=N   W: straight=W,right=N,left=S
            foreach (var (edge, ei, ej, heading) in queue)
            {
                var outs = new List<int>();
                var end = edges[edge].points;
                var p0 = end[end.Length - 1];

                float lI = LaneNS(ei);
                float lJ = LaneEW(ej);
                float hI = HalfNS(ei);
                float hJ = HalfEW(ej);

                // targets possíveis (heading novo, índice alvo, polilinha do conector)
                // straight:
                int[] sh = { 0, 1, 2, 3 };
                int[] rh = { 2, 3, 1, 0 }; // right turn por heading
                int[] lh = { 3, 2, 0, 1 }; // left turn por heading

                // Straight
                int sT = TargetIndex(ei, ej, sh[heading]);
                if (ValidTarget(sT, heading, sh[heading], n, m))
                {
                    var p1 = ExitPoint(ei, ej, sh[heading], sT, xs, zs, lI, lJ, hI, hJ);
                    outs.Add(AddConnector(edges, p0, p1));
                    edges[edges.Count - 1].next = new[] { laneEdge[ei, ej, sh[heading], sT] };
                }

                // Right
                int rT = TargetIndex(ei, ej, rh[heading]);
                if (ValidTarget(rT, heading, rh[heading], n, m))
                {
                    var p1 = ExitPoint(ei, ej, rh[heading], rT, xs, zs, lI, lJ, hI, hJ);
                    outs.Add(AddConnector(edges, p0, p1));
                    edges[edges.Count - 1].next = new[] { laneEdge[ei, ej, rh[heading], rT] };
                }

                // Left
                int lT = TargetIndex(ei, ej, lh[heading]);
                if (ValidTarget(lT, heading, lh[heading], n, m))
                {
                    var p1 = ExitPoint(ei, ej, lh[heading], lT, xs, zs, lI, lJ, hI, hJ);
                    outs.Add(AddConnector(edges, p0, p1));
                    edges[edges.Count - 1].next = new[] { laneEdge[ei, ej, lh[heading], lT] };
                }

                edges[edge].next = outs.ToArray();
            }

            return edges;

            // Índice do cruzamento alvo ao sair de (i,j) na direção newHeading:
            // N->j+1 usa laneEdge[i,j,0,j+1]? Não: o conector desemboca no INÍCIO
            // do lane edge que sai do mesmo cruzamento (i,j) na nova direção.
            // Para N a partir de (i,j): destino j+1. Guardamos laneEdge[i,j,h,t].
            static int TargetIndex(int i, int j, int newHeading)
            {
                return newHeading switch
                {
                    0 => j + 1, // N sai para o cruzamento ao norte
                    1 => j - 1,
                    2 => i + 1,
                    _ => i - 1,
                };
            }

            static bool ValidTarget(int t, int oldHeading, int newHeading, int n, int m)
            {
                return newHeading switch
                {
                    0 or 1 => t >= 0 && t < m,
                    _ => t >= 0 && t < n,
                };
            }

            // Ponto de saída do conector = início da lane edge na nova direção.
            static Vector3 ExitPoint(int i, int j, int newHeading, int t,
                float[] xs, float[] zs, float lI, float lJ, float hI, float hJ)
            {
                return newHeading switch
                {
                    // N: lane x = xs[i]+l, começa em z = zs[j]+hJ
                    0 => new Vector3(xs[i] + lI, 0f, zs[j] + hJ),
                    // S: lane x = xs[i]-l, z = zs[j]-hJ
                    1 => new Vector3(xs[i] - lI, 0f, zs[j] - hJ),
                    // E: lane z = zs[j]-l, x = xs[i]+hI
                    2 => new Vector3(xs[i] + hI, 0f, zs[j] - lJ),
                    // W: lane z = zs[j]+l, x = xs[i]-hI
                    _ => new Vector3(xs[i] - hI, 0f, zs[j] + lJ),
                };
            }

            static int AddConnector(List<TrafficEdge> edges, Vector3 p0, Vector3 p1)
            {
                // Polilinha de 4 pontos: tangentes curtas simulando a curva
                var mid1 = Vector3.Lerp(p0, p1, 0.33f);
                var mid2 = Vector3.Lerp(p0, p1, 0.66f);
                edges.Add(new TrafficEdge { points = new[] { p0, mid1, mid2, p1 } });
                return edges.Count - 1;
            }
        }
    }
}
