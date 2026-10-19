using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Studio.Editor
{
    /// <summary>
    /// Biblioteca de malhas procedurais canônicas do estúdio (Quality Gate V2).
    /// Substitui GameObject.CreatePrimitive: gera Mesh assets com UV0, normais e
    /// triângulos em Assets/Game/Art/Meshes e instancia GameObjects com
    /// MeshFilter + MeshRenderer. Dimensões equivalentes às primitivas nativas
    /// (cubo 1m centrado, cilindro r=0.5 h=2, esfera r=0.5, plano 10x10 XZ).
    /// Também instancia prefabs do catálogo modular (Assets/Game/Prefabs/Modular).
    /// </summary>
    public static class ProceduralMeshLibrary
    {
        private const string MESH_DIR = "Assets/Game/Art/Meshes";
        private const string KIT_PREFAB_DIR = "Assets/Game/Prefabs/Modular";

        private static readonly Dictionary<string, Mesh> _cache = new Dictionary<string, Mesh>();

        public static Mesh Box => Get("SM_Proc_Box", BuildBox);
        public static Mesh Cylinder => Get("SM_Proc_Cylinder", BuildCylinder);
        public static Mesh Sphere => Get("SM_Proc_Sphere", BuildUvSphere);
        public static Mesh Capsule => Get("SM_Proc_Capsule", BuildCapsule);
        public static Mesh Octahedron => Get("SM_Proc_Octahedron", BuildOctahedron);
        public static Mesh Torus => Get("SM_Proc_Torus", BuildTorus);
        public static Mesh Plane => Get("SM_Proc_Plane", BuildPlane);

        /// <summary>Equivalente procedural para um PrimitiveType (fallbacks legados).</summary>
        public static Mesh ForPrimitive(PrimitiveType type)
        {
            switch (type)
            {
                case PrimitiveType.Cube: return Box;
                case PrimitiveType.Cylinder: return Cylinder;
                case PrimitiveType.Capsule: return Capsule;
                case PrimitiveType.Plane: return Plane;
                case PrimitiveType.Quad:
                case PrimitiveType.Sphere:
                default: return Sphere;
            }
        }

        /// <summary>
        /// Cria GameObject com MeshFilter + MeshRenderer usando malha do pipeline.
        /// Posição/rotação/escala são LOCAIS ao parent (equivalente a transforms de
        /// filhos dos builders; raízes ficam na origem).
        /// </summary>
        public static GameObject CreatePart(
            string name,
            Mesh mesh,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Material material)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            return go;
        }

        /// <summary>
        /// Instancia um prefab do catálogo modular mantendo o vínculo de prefab
        /// (PrefabInstance na cena). A rotação armazenada no prefab (correção de
        /// eixos -90X) é preservada e composta com o yaw extra opcional.
        /// </summary>
        public static GameObject CreateKitInstance(
            string prefabName,
            string instanceName,
            Transform parent,
            Vector3 position,
            float yawDegrees = 0f)
        {
            string path = $"{KIT_PREFAB_DIR}/{prefabName}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"[ProceduralMeshLibrary] Prefab modular não encontrado: {path}");
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (!string.IsNullOrEmpty(instanceName)) go.name = instanceName;
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = position;
            go.transform.rotation = prefab.transform.rotation * Quaternion.Euler(0f, yawDegrees, 0f);
            return go;
        }

        /// <summary>União dos bounds (world space) de todos os renderers do objeto.</summary>
        public static Bounds ComputeBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        /// <summary>
        /// Escala o objeto uniformemente para atingir a altura alvo (Y) e recalcula
        /// os bounds; opcionalmente alinha a base da malha em baseY.
        /// </summary>
        public static Bounds ScaleToHeight(GameObject go, float targetHeight, float? baseY = null)
        {
            var b = ComputeBounds(go);
            float s = targetHeight / Mathf.Max(0.001f, b.size.y);
            go.transform.localScale = go.transform.localScale * s;
            if (baseY.HasValue)
            {
                b = ComputeBounds(go);
                go.transform.position += new Vector3(0f, baseY.Value - b.min.y, 0f);
                b = ComputeBounds(go);
            }
            return b;
        }

        /// <summary>
        /// Escala o objeto por eixo para ocupar as dimensões alvo (footprint livre
        /// para peças orgânicas como rochas) e alinha a base em baseY.
        /// </summary>
        public static Bounds ScaleToFit(GameObject go, Vector3 targetSize, float baseY)
        {
            var b = ComputeBounds(go);
            var s = go.transform.localScale;
            go.transform.localScale = new Vector3(
                s.x * targetSize.x / Mathf.Max(0.001f, b.size.x),
                s.y * targetSize.y / Mathf.Max(0.001f, b.size.y),
                s.z * targetSize.z / Mathf.Max(0.001f, b.size.z));
            b = ComputeBounds(go);
            go.transform.position += new Vector3(0f, baseY - b.min.y, 0f);
            return ComputeBounds(go);
        }

        private static Mesh Get(string name, Func<Mesh> builder)
        {
            if (_cache.TryGetValue(name, out var cached) && cached != null) return cached;
            string path = $"{MESH_DIR}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = builder();
                mesh.name = name;
                if (!Directory.Exists(MESH_DIR)) Directory.CreateDirectory(MESH_DIR);
                AssetDatabase.CreateAsset(mesh, path);
            }
            _cache[name] = mesh;
            return mesh;
        }

        #region Construtores de Malha

        private static Mesh BuildBox()
        {
            // Cubo 1x1x1 centrado, 24 vértices (4 por face) com UV 0-1 por face.
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            void Face(Vector3 n, Vector3 up, Vector3 right)
            {
                int start = verts.Count;
                Vector3 center = n * 0.5f;
                verts.Add(center - right * 0.5f - up * 0.5f);
                verts.Add(center + right * 0.5f - up * 0.5f);
                verts.Add(center + right * 0.5f + up * 0.5f);
                verts.Add(center - right * 0.5f + up * 0.5f);
                for (int i = 0; i < 4; i++) norms.Add(n);
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(1f, 1f));
                uvs.Add(new Vector2(0f, 1f));
                tris.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }

            Face(Vector3.up, Vector3.forward, Vector3.right);
            Face(Vector3.down, Vector3.back, Vector3.right);
            Face(Vector3.forward, Vector3.up, Vector3.left);
            Face(Vector3.back, Vector3.up, Vector3.right);
            Face(Vector3.right, Vector3.up, Vector3.back);
            Face(Vector3.left, Vector3.up, Vector3.forward);

            var m = new Mesh();
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            return m;
        }

        private static Mesh BuildCylinder()
        {
            // Cilindro r=0.5, altura 2 (Y em [-1,1]) — dimensões do cilindro nativo.
            const int seg = 24;
            const float r = 0.5f;
            const float halfH = 1f;

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // Laterais
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
                var n = new Vector3(x, 0f, z).normalized;
                verts.Add(new Vector3(x, -halfH, z));
                verts.Add(new Vector3(x, halfH, z));
                norms.Add(n);
                norms.Add(n);
                uvs.Add(new Vector2(i / (float)seg, 0f));
                uvs.Add(new Vector2(i / (float)seg, 1f));
            }
            for (int i = 0; i < seg; i++)
            {
                int s = i * 2;
                tris.AddRange(new[] { s, s + 1, s + 3, s, s + 3, s + 2 });
            }

            // Tampas (topo +Y, base -Y)
            void Cap(float y, Vector3 n)
            {
                int c = verts.Count;
                verts.Add(new Vector3(0f, y, 0f));
                norms.Add(n);
                uvs.Add(new Vector2(0.5f, 0.5f));
                int ring = verts.Count;
                for (int i = 0; i <= seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f;
                    float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
                    verts.Add(new Vector3(x, y, z));
                    norms.Add(n);
                    uvs.Add(new Vector2(x + 0.5f, z + 0.5f));
                }
                for (int i = 0; i < seg; i++)
                {
                    if (n.y > 0f) tris.AddRange(new[] { c, ring + i + 1, ring + i });
                    else tris.AddRange(new[] { c, ring + i, ring + i + 1 });
                }
            }
            Cap(halfH, Vector3.up);
            Cap(-halfH, Vector3.down);

            var m = new Mesh();
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            return m;
        }

        private static Mesh BuildUvSphere()
        {
            // Esfera UV r=0.5 (diâmetro 1) — dimensões da esfera nativa.
            const int lon = 24, lat = 16;
            const float r = 0.5f;

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int j = 0; j <= lat; j++)
            {
                float phi = Mathf.PI * j / lat;
                float y = Mathf.Cos(phi) * r;
                float rr = Mathf.Sin(phi) * r;
                for (int i = 0; i <= lon; i++)
                {
                    float theta = 2f * Mathf.PI * i / lon;
                    var p = new Vector3(rr * Mathf.Cos(theta), y, rr * Mathf.Sin(theta));
                    verts.Add(p);
                    norms.Add(p.normalized);
                    uvs.Add(new Vector2(i / (float)lon, 1f - j / (float)lat));
                }
            }
            for (int j = 0; j < lat; j++)
            {
                for (int i = 0; i < lon; i++)
                {
                    int a = j * (lon + 1) + i;
                    int b = a + lon + 1;
                    tris.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
                }
            }

            var m = new Mesh();
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            return m;
        }

        private static Mesh BuildCapsule()
        {
            // Cápsula r=0.5, altura total 2 (corpo cilíndrico 1 + hemisférios 0.5).
            const int seg = 24, hemi = 6;
            const float r = 0.5f, halfCyl = 0.5f;

            var profile = new List<Vector2>(); // (raio, y)
            for (int j = 0; j <= hemi; j++)
            {
                float a = -Mathf.PI * 0.5f + Mathf.PI * 0.5f * j / hemi;
                profile.Add(new Vector2(Mathf.Cos(a) * r, -halfCyl + Mathf.Sin(a) * r));
            }
            for (int j = 0; j <= hemi; j++)
            {
                float a = Mathf.PI * 0.5f * j / hemi;
                profile.Add(new Vector2(Mathf.Cos(a) * r, halfCyl + Mathf.Sin(a) * r));
            }

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int j = 0; j < profile.Count; j++)
            {
                for (int i = 0; i <= seg; i++)
                {
                    float theta = 2f * Mathf.PI * i / seg;
                    float x = profile[j].x * Mathf.Cos(theta);
                    float z = profile[j].x * Mathf.Sin(theta);
                    verts.Add(new Vector3(x, profile[j].y, z));
                    var centerY = Mathf.Clamp(profile[j].y, -halfCyl, halfCyl);
                    norms.Add((new Vector3(x, profile[j].y - centerY, z)).normalized);
                    uvs.Add(new Vector2(i / (float)seg, (profile[j].y + 1f) * 0.5f));
                }
            }
            int ring = seg + 1;
            for (int j = 0; j < profile.Count - 1; j++)
            {
                for (int i = 0; i < seg; i++)
                {
                    int a = j * ring + i;
                    int b = a + ring;
                    tris.AddRange(new[] { a, b, a + 1, b, b + 1, a + 1 });
                }
            }

            var m = new Mesh();
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            return m;
        }

        private static Mesh BuildOctahedron()
        {
            // Octaedro com vértices a +-0.5 nos eixos (inscrito na esfera r=0.5).
            Vector3[] p =
            {
                new Vector3(0.5f, 0f, 0f), new Vector3(-0.5f, 0f, 0f),
                new Vector3(0f, 0.5f, 0f), new Vector3(0f, -0.5f, 0f),
                new Vector3(0f, 0f, 0.5f), new Vector3(0f, 0f, -0.5f),
            };
            int[][] faces =
            {
                new[] { 2, 4, 0 }, new[] { 2, 1, 4 }, new[] { 2, 5, 1 }, new[] { 2, 0, 5 },
                new[] { 3, 0, 4 }, new[] { 3, 4, 1 }, new[] { 3, 1, 5 }, new[] { 3, 5, 0 },
            };

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            foreach (var f in faces)
            {
                int s = verts.Count;
                var a = p[f[0]];
                var b = p[f[1]];
                var c = p[f[2]];
                var n = Vector3.Cross(b - a, c - a).normalized;
                verts.Add(a); verts.Add(b); verts.Add(c);
                for (int i = 0; i < 3; i++) norms.Add(n);
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(0.5f, 1f));
                tris.AddRange(new[] { s, s + 1, s + 2 });
            }

            var m = new Mesh();
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            return m;
        }

        private static Mesh BuildTorus()
        {
            // Toro no plano XY (eixo do furo = +Z). Raio maior 0.375 + tubo 0.125
            // = raio externo 0.5 (diâmetro 1, escalável como as primitivas).
            const int segU = 32, segV = 12;
            const float R = 0.375f, r = 0.125f;

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int j = 0; j <= segV; j++)
            {
                float v = 2f * Mathf.PI * j / segV;
                for (int i = 0; i <= segU; i++)
                {
                    float u = 2f * Mathf.PI * i / segU;
                    float rr = R + r * Mathf.Cos(v);
                    verts.Add(new Vector3(rr * Mathf.Cos(u), rr * Mathf.Sin(u), r * Mathf.Sin(v)));
                    norms.Add(new Vector3(Mathf.Cos(v) * Mathf.Cos(u), Mathf.Cos(v) * Mathf.Sin(u), Mathf.Sin(v)));
                    uvs.Add(new Vector2(i / (float)segU, j / (float)segV));
                }
            }
            int ring = segU + 1;
            for (int j = 0; j < segV; j++)
            {
                for (int i = 0; i < segU; i++)
                {
                    int a = j * ring + i;
                    int b = a + ring;
                    tris.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
                }
            }

            var m = new Mesh();
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            return m;
        }

        private static Mesh BuildPlane()
        {
            // Plano 10x10 no XZ (normal +Y) — dimensões do plano nativo.
            var m = new Mesh();
            m.SetVertices(new List<Vector3>
            {
                new Vector3(-5f, 0f, -5f), new Vector3(5f, 0f, -5f),
                new Vector3(5f, 0f, 5f), new Vector3(-5f, 0f, 5f),
            });
            m.SetNormals(new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            m.SetUVs(0, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
            });
            m.SetTriangles(new List<int> { 0, 2, 1, 0, 3, 2 }, 0);
            return m;
        }

        #endregion
    }
}
