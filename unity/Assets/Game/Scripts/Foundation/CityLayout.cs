using UnityEngine;

namespace Studio.Foundation
{
    /// <summary>
    /// Definição única da malha viária do distrito — usada pelo builder
    /// (props) e pelo runtime (pedestres) para nunca confundir rua/calçada.
    /// Grid 6x6 vias, ruas de 12m (avenida j=3 de 16m), quarteirões de 40m.
    /// </summary>
    public static class CityLayout
    {
        public const int LINES = 6;
        public const float PITCH = 52f;
        public const float STREET_W = 12f;
        public const float AVENUE_W = 16f;
        public const int AVENUE_J = 3;
        public const float STREET_HALF = STREET_W * 0.5f;
        public const float AVENUE_HALF = AVENUE_W * 0.5f;
        public const float CITY_SPAN = 284f;

        /// <summary>Centro da via i/j no eixo (x para N-S, z para E-W).</summary>
        public static float Line(int i) => (i - (LINES - 1) * 0.5f) * PITCH;

        /// <summary>Meia-largura da via (avenida E-W é mais larga).</summary>
        public static float HalfWidth(bool avenueEW) =>
            avenueEW ? AVENUE_HALF : STREET_HALF;

        /// <summary>true se a via E-W de índice j é a Avenida Neon.</summary>
        public static bool IsAvenue(int j) => j == AVENUE_J;

        /// <summary>
        /// Offset da calçada a partir do centro da via:
        /// faixa habitável logo após a borda do asfalto (meio da calçada).
        /// </summary>
        public static float SidewalkOffset(bool avenueEW) =>
            HalfWidth(avenueEW) + 2.2f;

        /// <summary>
        /// Distância mínima a um cruzamento pra considerar "miolo de quadra"
        /// (evita spawnar dentro da área de interseção/faixa).
        /// </summary>
        public const float INTERSECTION_MARGIN = 9f;

        /// <summary>
        /// true se a coordenada `t` ao longo de uma via está dentro da banda
        /// de algum cruzamento (perto de outra via transversal).
        /// </summary>
        public static bool InIntersectionBand(float t, float margin = INTERSECTION_MARGIN)
        {
            for (int k = 0; k < LINES; k++)
                if (Mathf.Abs(t - Line(k)) < margin) return true;
            return false;
        }

        /// <summary>
        /// Posição de calçada: eixo 'ns' = calçada da via N-S i (x = line±off,
        /// varia em z); 'ew' = calçada da via E-W j (z = line±off, varia em x).
        /// </summary>
        public static Vector3 SidewalkPoint(bool alongNS, int streetIdx,
            int side, float t)
        {
            bool avenueEW = !alongNS && IsAvenue(streetIdx);
            float off = SidewalkOffset(avenueEW) * Mathf.Sign(side == 0 ? 1 : side);
            float line = Line(streetIdx);
            return alongNS
                ? new Vector3(line + off, 0f, t)
                : new Vector3(t, 0f, line + off);
        }

        /// <summary>Ponto no centro da via (rua) — pra condução.</summary>
        public static Vector3 RoadPoint(bool alongNS, int streetIdx, float t)
        {
            float line = Line(streetIdx);
            return alongNS
                ? new Vector3(line, 0f, t)
                : new Vector3(t, 0f, line);
        }
    }
}
