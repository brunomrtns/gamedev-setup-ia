using UnityEngine;

namespace Studio.Gameplay
{
    /// <summary>Interface para objetos que respondem a tiro (mundo reativo).</summary>
    public interface IShootable
    {
        void OnShot(RaycastHit hit);
    }

    /// <summary>Poste: ao levar tiro apaga (e pisca) até a área resetar.</summary>
    public class ShootableLamp : MonoBehaviour, IShootable
    {
        private Light[] _lights;
        private bool _out;

        private void Awake() { _lights = GetComponentsInChildren<Light>(true); }

        public void OnShot(RaycastHit hit)
        {
            if (_out) return;
            _out = true;
            StartCoroutine(FlickerOut());
            Foundation.ProceduralAudioService.Instance?.PlayGlassBreak();
        }

        private System.Collections.IEnumerator FlickerOut()
        {
            for (int i = 0; i < 5; i++)
            {
                SetLit(i % 2 == 0);
                yield return new WaitForSeconds(0.07f);
            }
            SetLit(false);
        }

        private void SetLit(bool on)
        {
            if (_lights == null) return;
            foreach (var l in _lights) l.enabled = on;
        }
    }

    /// <summary>Lata/alvo de brincadeira: +1 no contador CANS e salta.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ShootableCan : MonoBehaviour, IShootable
    {
        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.mass = 0.25f;
        }

        public void OnShot(RaycastHit hit)
        {
            _rb.isKinematic = false;
            _rb.AddForceAtPosition(hit.normal * -4f + Vector3.up * 3f, hit.point, ForceMode.Impulse);
            Foundation.ProceduralAudioService.Instance?.PlayImpactMetal();
            NeonDistrictModeManager.Instance?.RegisterCanHit();
        }
    }

    /// <summary>Prop que salta com tiro (banco, cone, lixo).</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ShootableProp : MonoBehaviour, IShootable
    {
        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.mass = 4f;
        }

        public void OnShot(RaycastHit hit)
        {
            _rb.isKinematic = false;
            _rb.AddForceAtPosition(-hit.normal * 2.5f, hit.point, ForceMode.Impulse);
            Foundation.ProceduralAudioService.Instance?.PlayImpactConcrete();
        }
    }

    /// <summary>Superfície simples (vidro, metal, concreto): só feedback.</summary>
    public class ShootableSurface : MonoBehaviour, IShootable
    {
        public enum Surface { Concrete, Metal, Glass }
        public Surface surface = Surface.Concrete;

        public void OnShot(RaycastHit hit)
        {
            var a = Foundation.ProceduralAudioService.Instance;
            if (a == null) return;
            switch (surface)
            {
                case Surface.Metal: a.PlayImpactMetal(); break;
                case Surface.Glass: a.PlayGlassBreak(); break;
                default: a.PlayImpactConcrete(); break;
            }
        }
    }
}
