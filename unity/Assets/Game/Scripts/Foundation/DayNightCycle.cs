using UnityEngine;

namespace Studio.Foundation
{
    /// <summary>
    /// Ciclo entardecer→noite cinematográfico: gira o sol, escurece o skybox,
    /// e liga luzes de rua/faróis/letreiros quando cai abaixo do limiar noturno.
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        [SerializeField] private float _cycleMinutes = 4f; // dusk→night rápido
        [Range(0f,1f)] public float Time01 = 0f;           // 0=entardecer 1=noite
        [SerializeField] private Light _sun;
        [SerializeField] private Material _sky;
        [SerializeField] private Color _duskTint = new Color(1f, 0.72f, 0.55f);
        [SerializeField] private Color _nightTint = new Color(0.10f, 0.12f, 0.24f);
        [SerializeField] private float _duskAmb = 1.1f, _nightAmb = 0.35f;
        [SerializeField] private float _duskExp = 1.3f, _nightExp = 0.8f;

        private readonly System.Collections.Generic.List<Light> _nightLights = new();
        private Renderer[] _neonSigns;
        public bool IsNight => Time01 > 0.55f;

        public void Setup(Light sun, Material sky, System.Collections.Generic.List<Light> lights)
        {
            _sun = sun; _sky = sky;
            if (lights != null) _nightLights.AddRange(lights);
        }

        private void Update()
        {
            Time01 = Mathf.Repeat(Time01 + Time.deltaTime / (_cycleMinutes * 60f), 1f);
            float t = Mathf.SmoothStep(0f, 1f, Time01);

            // sol gira de ~20° (tarde) pra -15° (noite — abaixo do horizonte)
            if (_sun != null)
            {
                _sun.transform.rotation = Quaternion.Euler(
                    Mathf.Lerp(25f, -12f, t), -30f, 0f);
                _sun.intensity = Mathf.Lerp(1.1f, 0.15f, t);
                _sun.color = Color.Lerp(new Color(1f, 0.65f, 0.45f),
                    new Color(0.25f, 0.35f, 0.65f), t);
            }
            if (_sky != null)
            {
                _sky.SetColor("_Tint", Color.Lerp(_duskTint, _nightTint, t));
                if (_sky.HasProperty("_Exposure"))
                    _sky.SetFloat("_Exposure", Mathf.Lerp(_duskExp, _nightExp, t));
            }
            RenderSettings.ambientIntensity = Mathf.Lerp(_duskAmb, _nightAmb, t);

            // luzes noturnas ligam no escurecer
            foreach (var l in _nightLights)
                if (l != null) l.enabled = IsNight;
        }
    }
}
