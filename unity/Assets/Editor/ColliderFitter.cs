using UnityEngine;

namespace Studio.Editor
{
    /// <summary>
    /// Ajustador canônico de colisores — regra V3 do estúdio.
    ///
    /// Problema: colisores por AABB total do prefab geram caixas gigantes
    /// (poste colide com o braço da luminária, árvore com a copa).
    ///
    /// Solução: perfis por arquétipo. Colisores de props colidem APENAS
    /// onde o player pode encostar (tronco/haste base), nunca em partes
    /// altas que ele passa por baixo. Edifícios seguem AABB completo.
    ///
    /// Uso: ColliderFitter.FitProp(go, "lamp_pole_double") ou FitVehicle(go).
    /// Toda scene builder DEVE usar este utilitário (docs/standards/unity.md).
    /// </summary>
    public static class ColliderFitter
    {
        /// <summary>Perfil de colisor em frações dos bounds do prefab + teto absoluto em metros.</summary>
        public struct ColliderProfile
        {
            /// <summary>Fração de largura X/Z dos bounds.</summary>
            public float xz;
            /// <summary>Teto absoluto X/Z em metros — braço de poste/copa nunca infla a haste.</summary>
            public float xzMax;
            /// <summary>Fração da altura total a partir do chão (1 = altura cheia).</summary>
            public float height;
            /// <summary>Usa o pivô do prefab (tronco/haste no eixo local) em vez do centro dos bounds.</summary>
            public bool originCentered;
            /// <summary>Altura extra de "tampa" para props que precisam cobrir tudo.</summary>
            public float extraTop;

            public static ColliderProfile Full() => new ColliderProfile { xz = 0.92f, xzMax = float.MaxValue, height = 1f, originCentered = false, extraTop = 2f };
        }

        /// <summary>
        /// Resolve o perfil pelo nome do prefab. Ordem importa (mais específico primeiro).
        /// Cada regra retorna fração XZ e fração de altura medida desde a base.
        /// </summary>
        public static ColliderProfile ProfileFor(string prefabName)
        {
            string n = prefabName.ToLowerInvariant();

            // Hastes verticais: colide só a coluna (~0.5m máx), nunca braço/luminária
            if (n.StartsWith("lamp_pole") || n.StartsWith("pole") || n.StartsWith("streetlight")
                || n.Contains("lamp_") || n.StartsWith("traffic_light") || n.StartsWith("street_light"))
                return new ColliderProfile { xz = 0.15f, xzMax = 0.55f, height = 1f, originCentered = true, extraTop = 0f };

            // Árvores: só tronco (~0.6m máx), copa é espaço livre
            if (n.StartsWith("tree") || n.Contains("bush") || n.StartsWith("palm"))
                return new ColliderProfile { xz = 0.15f, xzMax = 0.60f, height = 0.55f, originCentered = true, extraTop = 0f };

            // Postes/colunas finas genéricas
            if (n.StartsWith("sign") || n.StartsWith("traffic_sign") || n.StartsWith("hydrant"))
                return new ColliderProfile { xz = 0.20f, xzMax = 0.45f, height = 1f, originCentered = true, extraTop = 0f };

            // Estátuas/monumentos: base sólida (30% XZ), altura média
            if (n.Contains("statue")) return new ColliderProfile { xz = 0.35f, xzMax = 3.5f, height = 0.8f, originCentered = true, extraTop = 0f };

            // Barreiras baixas — AABB completo (funcionam como parede)
            // hedge, bench, concrete_block, fence, barrier, kerb...
            return ColliderProfile.Full();
        }

        /// <summary>
        /// Cria um colisor AABB como filho de um root sem escala (não herda
        /// a escala do prefab instanciado — evita colisor duplicado/errado).
        /// parentForCollider deve ser o transform do ROOT de cena que agrupa os props
        /// (ou null para criar no nível da cena).
        /// </summary>
        public static GameObject FitProp(GameObject go, string prefabName, Transform colliderParent)
        {
            var profile = ProfileFor(prefabName);
            var b = ComputeBounds(go);
            if (b.size.sqrMagnitude < 0.0001f) return null;

            // Centro XZ do colisor: centro dos bounds, ou o pivô do prefab se o perfil
            // pedir (tronco/haste normalmente fica no pivô local, não no centro da copa).
            var pos = b.center;
            if (profile.originCentered)
            {
                pos.x = go.transform.position.x;
                pos.z = go.transform.position.z;
                pos.y = b.center.y;
            }

            var colObj = new GameObject($"{go.name}_BoundsCollider");
            if (colliderParent != null) colObj.transform.SetParent(colliderParent, false);
            colObj.transform.position = pos;
            colObj.transform.rotation = Quaternion.identity;
            var col = colObj.AddComponent<BoxCollider>();

            float sx = Mathf.Clamp(b.size.x * profile.xz, 0.25f, profile.xzMax);
            float sz = Mathf.Clamp(b.size.z * profile.xz, 0.25f, profile.xzMax);
            float h = b.size.y * profile.height + profile.extraTop;
            // Base do colisor = base dos bounds (colisor "cresce" do chão)
            float baseY = b.min.y;
            float centerY = baseY + h * 0.5f;
            colObj.transform.position = new Vector3(pos.x, centerY, pos.z);
            col.size = new Vector3(sx, h, sz);
            return colObj;
        }

        /// <summary>
        /// Colisor de carroceria anexado AO PRÓPRIO rigidbody (espaço local).
        /// Cobre a carroceria (rodas/porta-malas), não o teto nem sobras de
        /// para-choque — 80% X/Z, altura 12%→84% dos bounds.
        /// </summary>
        public static void FitVehicleBody(GameObject go)
        {
            var b = ComputeLocalBounds(go, go.transform);
            if (b.size.sqrMagnitude < 0.0001f) return;
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(b.size.x * 0.80f, b.size.y * 0.72f, b.size.z * 0.82f);
            // sobe o colisor: base fica ~12% acima do chão visual do modelo
            col.center = b.center + new Vector3(0f, b.size.y * 0.06f, 0f);
        }

        /// <summary>
        /// Colisor de veículo AABB em root sem escala (para objetos estáticos
        /// ou quando o rigidbody está em outro nó — ex: chassi wrapper).
        /// </summary>
        public static void FitVehicle(GameObject go, Transform colliderParent = null)
        {
            var b = ComputeBounds(go);
            if (b.size.sqrMagnitude < 0.0001f) return;

            var colObj = new GameObject($"{go.name}_BoundsCollider");
            if (colliderParent != null) colObj.transform.SetParent(colliderParent, false);
            // levemente acima do solo (não rasteja na base do bounds)
            float baseY = b.min.y + b.size.y * 0.12f;
            float h = b.size.y * 0.72f;
            colObj.transform.position = new Vector3(b.center.x, baseY + h * 0.5f, b.center.z);
            colObj.transform.rotation = Quaternion.identity;
            var col = colObj.AddComponent<BoxCollider>();
            // encolhe X/Z para compensar spoilers/espelhos
            col.size = new Vector3(b.size.x * 0.80f, h, b.size.z * 0.82f);
        }

        /// <summary>
        /// Colisores compostos de veículo: chassi (comprimento total, parte
        /// baixa) + cabine (trecho médio, parte alta e mais estreita). Mira e
        /// colisão seguem a silhueta — tiro por cima do capô passa por cima.
        /// </summary>
        public static void FitVehicleCompound(GameObject root, Bounds localB)
        {
            Vector3 sz = localB.size;
            bool zLong = sz.z >= sz.x;
            float L = zLong ? sz.z : sz.x;   // comprimento
            float W = zLong ? sz.x : sz.z;   // largura
            float H = sz.y;
            float baseY = localB.min.y + H * 0.06f;

            void AddBox(string n, float lenFrac, float lenCenter, float widFrac,
                float botFrac, float hFrac)
            {
                var c = new GameObject(n).AddComponent<BoxCollider>();
                c.transform.SetParent(root.transform, false);
                float sizeL = L * lenFrac, sizeW = W * widFrac, sizeH = H * hFrac;
                float off = (lenCenter - 0.5f) * L;
                c.size = zLong ? new Vector3(sizeW, sizeH, sizeL) : new Vector3(sizeL, sizeH, sizeW);
                c.center = zLong
                    ? new Vector3(localB.center.x, baseY + H * botFrac + sizeH * 0.5f, localB.center.z + off)
                    : new Vector3(localB.center.x + off, baseY + H * botFrac + sizeH * 0.5f, localB.center.z);
            }

            AddBox("Col_Chassis", 1.0f, 0.5f, 0.82f, 0.0f, 0.45f);
            AddBox("Col_Cabin", 0.42f, 0.52f, 0.68f, 0.42f, 0.52f);
            AddBox("Col_Hood", 0.28f, 0.86f, 0.78f, 0.0f, 0.52f);   // frente — 1.5x dano
            AddBox("Col_Tank", 0.16f, 0.06f, 0.60f, 0.0f, 0.40f);   // traseira/tanque — 2.0x
        }

        /// <summary>
        /// Colisor composto: UMA caixa por renderer significativo (paredes,
        /// rampas, volumes) — resolve prédios com rampa/recorte onde o AABB
        /// único cria colisão invisível em espaço aberto.
        /// </summary>
        public static void FitCompoundBuilding(GameObject go, Transform colliderParent, float minVolume = 0.8f)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            int count = 0;
            foreach (var r in rends)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var b = r.bounds;
                if (b.size.x * b.size.y * b.size.z < minVolume) continue;
                // MeshCollider côncavo: colisão EXATA do modelo (vãos, rampas,
                // recortes respeitados) — custo ok p/ geometria estática low-poly
                var mc = r.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = false;
                count++;
            }
            var group = go; // referência pro fallback abaixo
            // segurança: se nada significativo, cai pro AABB simples
            if (count == 0)
            {
                var b = ComputeBounds(go);
                var colObj = new GameObject($"{go.name}_BoundsCollider");
                colObj.transform.SetParent(group.transform, false);
                colObj.transform.position = b.center;
                var col = colObj.AddComponent<BoxCollider>();
                col.size = b.size;
            }
        }

        /// <summary>Bounds combinados de todos os renderers ativos.</summary>
        public static Bounds ComputeBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }

        /// <summary>Bounds em espaço local de `root` (para colisor dentro do próprio GO).</summary>
        public static Bounds ComputeLocalBounds(GameObject go, Transform root)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
            var b = new Bounds(root.InverseTransformPoint(rends[0].bounds.center), Vector3.zero);
            foreach (var r in rends)
            {
                var rb = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = rb.center + new Vector3((i & 1) == 0 ? -rb.extents.x : rb.extents.x,
                        (i & 2) == 0 ? -rb.extents.y : rb.extents.y,
                        (i & 4) == 0 ? -rb.extents.z : rb.extents.z);
                    b.Encapsulate(root.InverseTransformPoint(c));
                }
            }
            return b;
        }
    }
}
