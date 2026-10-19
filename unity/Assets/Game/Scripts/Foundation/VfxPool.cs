using System.Collections.Generic;
using UnityEngine;

namespace Studio.Foundation
{
    /// <summary>Bus de shake de câmera (desacopla Gameplay→Cameras).</summary>
    public static class CameraShakeBus
    {
        private static float _pending;
        public static void Impulse(float i) { _pending = Mathf.Max(_pending, i); }
        public static float Consume() { var v = _pending; _pending = 0f; return v; }
    }

    /// <summary>
    /// Pool de VFX: recicla instâncias de partículas (zero Instantiate em jogo
    /// após o primeiro uso). Cada item é resetado (ParticleSystem.Clear) ao
    /// devolver. Prefab é registrado por chave; expansão sob demanda.
    /// </summary>
    public static class VfxPool
    {
        private static readonly Dictionary<string, GameObject> _prefabs = new();
        private static readonly Dictionary<string, Queue<GameObject>> _free = new();
        private static Transform _root;

        public static void Register(string key, GameObject prefab)
        {
            if (!string.IsNullOrEmpty(key) && prefab != null) _prefabs[key] = prefab;
        }

        public static GameObject Spawn(string key, Vector3 pos, Quaternion rot, float life = 3f)
        {
            if (!_prefabs.TryGetValue(key, out var prefab) || prefab == null) return null;
            if (!_free.TryGetValue(key, out var q)) { q = new Queue<GameObject>(); _free[key] = q; }
            // A fila sobrevive a domain/scene reload e pode conter instâncias
            // já destruídas — go==null (Unity) cobre isso; pula até achar viva.
            GameObject go = null;
            while (q.Count > 0 && go == null) go = q.Dequeue();
            if (go == null) go = Create(key, prefab);
            go.transform.SetPositionAndRotation(pos, rot);
            go.SetActive(true);
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            { ps.Clear(true); ps.Play(true); }
            var auto = go.GetComponent<VfxAutoReturn>();
            if (auto == null) auto = go.AddComponent<VfxAutoReturn>();
            auto.Begin(key, life);
            return go;
        }

        private static GameObject Create(string key, GameObject prefab)
        {
            if (_root == null) _root = new GameObject("_VfxPool").transform;
            var go = Object.Instantiate(prefab, _root);
            go.name = $"{key}_pooled";
            return go;
        }

        public static void Release(string key, GameObject go)
        {
            if (go == null) return;
            go.SetActive(false);
            if (!_free.TryGetValue(key, out var q)) { q = new Queue<GameObject>(); _free[key] = q; }
            q.Enqueue(go);
        }

        /// <summary>
        /// Devolve ao pool quando a vida acaba OU quando o player sai do campo
        /// (&gt;45m) — efeito residual vive enquanto visível, não tem timer fixo.
        /// </summary>
        private class VfxAutoReturn : MonoBehaviour
        {
            private string _key;
            private float _t;
            private float _life = 3f;
            public void Begin(string key, float life) { _key = key; _life = life; _t = 0f; }
            private void Update()
            {
                _t += Time.deltaTime;
                if (_t >= _life) { Release(_key, gameObject); return; }
                // Player longe → recicla cedo (efeito residual sai do campo)
                var cam = Camera.main;
                if (cam != null && _life > 4f &&
                    Vector3.Distance(transform.position, cam.transform.position) > 45f)
                    Release(_key, gameObject);
            }
        }
    }
}
