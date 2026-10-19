using System.Collections.Generic;
using UnityEngine;

namespace Studio.Gameplay
{
    /// <summary>
    /// Bandos de pássaros: circulam em bando acima dos quarteirões; tiros e
    /// explosões disparam revoada (alto + longe do susto) — vida ambiental.
    /// </summary>
    public class BirdSystem : MonoBehaviour
    {
        public static BirdSystem Instance { get; private set; }
        private GameObject[] _prefabs;
        private readonly List<Flock> _flocks = new();

        private void Awake()
        {
            Instance = this;
            _prefabs = Resources.LoadAll<GameObject>("Art/Birds");
        }

        private void Start()
        {
            if (_prefabs == null || _prefabs.Length == 0) { enabled = false; return; }
            var rand = new System.Random(11);
            for (int i = 0; i < 4; i++)
                _flocks.Add(new Flock(this, rand, i));
        }

        /// <summary>Susto: bandos perto do ponto levantam voo.</summary>
        public void ScatterAt(Vector3 pos, float radius = 25f)
        {
            foreach (var f in _flocks) f.TryScatter(pos, radius);
        }

        private class Flock
        {
            private readonly GameObject _go;
            private Vector3 _center;
            private float _radius, _ang, _speed, _height;
            private float _scatterEnd;
            private Vector3 _scatterDir;

            public Flock(BirdSystem sys, System.Random rand, int index)
            {
                var pf = sys._prefabs[rand.Next(sys._prefabs.Length)];
                // rotas VISÍVEIS: sobre a Avenida Principal (z=+26) e a Praça
                // Central — altitude baixa (6–12 m) para o jogador ver os bandos
                var routes = new[]
                {
                    new Vector3(-60f, 0f, 26f), new Vector3(60f, 0f, 26f),
                    new Vector3(0f, 0f, 0f),    new Vector3(-20f, 0f, 26f),
                };
                _center = routes[index % routes.Length]
                    + new Vector3((float)rand.NextDouble() * 8f - 4f, 0f,
                        (float)rand.NextDouble() * 8f - 4f);
                _height = 6f + (float)rand.NextDouble() * 6f;
                _radius = 12f + (float)rand.NextDouble() * 10f;
                _speed = 0.5f + (float)rand.NextDouble() * 0.4f;
                _ang = (float)rand.NextDouble() * 6.28f;
                _go = Object.Instantiate(pf,
                    _center + Vector3.up * _height, Quaternion.identity);
                _go.name = "BirdFlock";
            }

            public void TryScatter(Vector3 danger, float radius)
            {
                if (Vector3.Distance(_go.transform.position, danger) < radius)
                {
                    _scatterEnd = Time.time + 8f;
                    _scatterDir = (_go.transform.position - danger).normalized;
                }
            }

            public void Tick()
            {
                if (_go == null) return;
                _ang += _speed * Time.deltaTime;
                var pos = _center + new Vector3(
                    Mathf.Cos(_ang) * _radius, 0f, Mathf.Sin(_ang) * _radius);
                pos.y = _height + Mathf.Sin(_ang * 2f) * 1.5f;
                if (Time.time < _scatterEnd)
                    pos += _scatterDir * 8f + Vector3.up * 10f; // revoada alta
                var t = _go.transform;
                var next = pos;
                var look = (next - t.position);
                if (look.sqrMagnitude > 0.01f)
                    t.rotation = Quaternion.Slerp(t.rotation,
                        Quaternion.LookRotation(look.normalized), 4f * Time.deltaTime);
                t.position = Vector3.Lerp(t.position, next, Time.deltaTime * (_scatterEnd > Time.time ? 3f : 1f));
            }
        }

        private void Update()
        {
            foreach (var f in _flocks) f.Tick();
        }
    }
}
