using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Studio.Foundation
{
    /// <summary>
    /// Overlay de debug de física — desenha o wireframe de TODOS os colisores
    /// da cena na Game view (editor e build). Verde = estático, ciano = dinâmico.
    /// Auto-injetado em runtime; toggle com F9.
    /// </summary>
    public class ColliderDebugOverlay : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Ensure()
        {
            var go = new GameObject("ColliderDebugOverlay");
            var o = go.AddComponent<ColliderDebugOverlay>();
            var meshObj = new GameObject("ColliderDebugMesh");
            meshObj.transform.SetParent(go.transform, false);
            o._filter = meshObj.AddComponent<MeshFilter>();
            var mr = meshObj.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Hidden/Internal-Colored"));
            mat.SetInt("_ZWrite", 0);
            mat.SetInt("_ZTest", (int)CompareFunction.Always);
            mr.sharedMaterial = mat;
            o._meshObj = meshObj;
            meshObj.SetActive(false);
        }

        private GameObject _meshObj;
        private MeshFilter _filter;
        private bool _visible;
        private float _refreshTimer;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9)) Toggle();
            if (_visible)
            {
                _refreshTimer -= Time.unscaledDeltaTime;
                if (_refreshTimer <= 0f)
                {
                    _refreshTimer = 0.4f; // objetos em movimento (tráfego/player) atualizam
                    Regenerate();
                }
            }
        }

        public void Toggle()
        {
            _visible = !_visible;
            if (_visible) { Regenerate(); _refreshTimer = 0.4f; }
            _meshObj.SetActive(_visible);
        }

        private void Regenerate()
        {
            var cols = FindObjectsByType<Collider>(FindObjectsSortMode.None);
            var verts = new List<Vector3>(cols.Length * 24);
            var colors = new List<Color32>(cols.Length * 24);
            var indices = new List<int>(cols.Length * 24);
            foreach (var c in cols)
            {
                if (!c.enabled) continue;
                var col = c.attachedRigidbody != null
                    ? new Color32(0, 230, 255, 255)   // dinâmico: ciano
                    : new Color32(60, 255, 110, 255); // estático: verde
                AddColliderBox(c, verts, colors, indices, col);
            }
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            _filter.sharedMesh = mesh;
        }

        // 12 arestas de uma caixa; BoxCollider usa centro/tamanho locais transformados,
        // outros tipos caem no AABB de bounds (aproximação suficiente pra debug).
        private static void AddColliderBox(Collider c, List<Vector3> verts, List<Color32> colors,
            List<int> indices, Color32 col)
        {
            var v = new Vector3[8];
            if (c is BoxCollider box)
            {
                var ctr = box.center;
                var h = box.size * 0.5f;
                var m = box.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var p = ctr + new Vector3((i & 1) == 0 ? -h.x : h.x,
                        (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
                    v[i] = m.MultiplyPoint3x4(p);
                }
            }
            else
            {
                var b = c.bounds;
                var ctr = b.center;
                var h = b.extents;
                for (int i = 0; i < 8; i++)
                    v[i] = ctr + new Vector3((i & 1) == 0 ? -h.x : h.x,
                        (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
            }

            int start = verts.Count;
            // arestas: (0,1)(0,2)(1,3)(2,3) face z- · (4,5)(4,6)(5,7)(6,7) face z+ · (0,4)(1,5)(2,6)(3,7) ligações
            int[] e = { 0,1, 0,2, 1,3, 2,3, 4,5, 4,6, 5,7, 6,7, 0,4, 1,5, 2,6, 3,7 };
            foreach (var ei in e) indices.Add(start + ei);
            for (int i = 0; i < 8; i++) { verts.Add(v[i]); colors.Add(col); }
        }
    }
}
