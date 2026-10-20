using UnityEngine;

namespace Studio.Foundation
{
    /// <summary>
    /// Regra dia/noite para a iluminação urbana (componente de ambiente, não-gameplay).
    /// - Céu de sol alto: postes/neon/janelas APAGADOS.
    /// - dusk / night / red / qualquer outro: ACESOS.
    /// O jogo roda em dusk permanente, portanto o default é aceso.
    /// </summary>
    public sealed class CityLightingController : MonoBehaviour
    {
        [SerializeField] private string _skyName = "dusk";
        [SerializeField] private float _updateInterval = 1.0f;

        private float _t;
        private bool _lightsOn = true;

        public void SetSky(string skyName) => _skyName = skyName?.ToLowerInvariant() ?? "dusk";

        private bool ShouldBeOn()
        {
            // Sol bem em cima = único estado que apaga. Dusk/night/red/etc = aceso.
            return _skyName != "day" && _skyName != "noon" && _skyName != "high_sun";
        }

        private void Start() => Apply(true);

        private void Update()
        {
            _t += Time.deltaTime;
            if (_t >= _updateInterval) { _t = 0f; Apply(false); }
        }

        private void Apply(bool force)
        {
            bool want = ShouldBeOn();
            if (!force && want == _lightsOn) return;
            _lightsOn = want;

            var renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (var r in renderers)
            {
                var n = r.name;
                if (!n.StartsWith("LampGlow") && !n.StartsWith("LightPool") &&
                    !n.StartsWith("NeonSign") && !n.Contains("Letter_") &&
                    !n.StartsWith("LitWin")) continue;
                r.enabled = want;
            }

            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in lights)
                if (l.name == "LampPointLight") l.enabled = want;
        }
    }
}
