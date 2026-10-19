using UnityEngine;

namespace Studio.Gameplay
{
    /// <summary>
    /// Prop urbano destrutível por limiar de impulso (padrão physics-and-combat.md).
    /// Vive na RAIZ VISUAL do prop junto com um Rigidbody kinematic — ao quebrar,
    /// o rb vira dinâmico e o objeto INTEIRO (colisor + MeshRenderer) tomba no
    /// solo, eliminando o bug da textura fantasma suspensa. Após o tempo de
    /// vida do entulho o prop desativa.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BreakableProp : MonoBehaviour
    {
        [SerializeField] private float _breakImpulse = 3000f;   // N·s (~2.2 m/s num carro de 1350kg)
        [SerializeField] private float _debrisMass = 45f;
        [SerializeField] private float _debrisLifetime = 25f;

        private bool _broken;
        private Rigidbody _rb;

        /// <summary>Decisão pura de quebra — testável em EditMode.</summary>
        public static bool ShouldBreak(float impulse, float threshold) => impulse >= threshold;

        /// <summary>Escala vertical do toco em relação à malha original — testável.</summary>
        public static float StumpScaleY(float boundsHeight, float stumpHeight)
            => Mathf.Clamp(stumpHeight / Mathf.Max(0.01f, boundsHeight), 0.05f, 0.6f);

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            // Plantado até quebrar — nunca cai sozinho nem é empurrado
            _rb.isKinematic = true;
            _rb.mass = _debrisMass;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_broken) return;
            float impulse = collision.impulse.magnitude;
            if (!ShouldBreak(impulse, _breakImpulse))
            {
                Foundation.ProceduralAudioService.Instance?.PlayCollisionThud();
                return;
            }
            var c = collision.GetContact(0);
            Break(c.normal, c.point, impulse);
        }

        /// <summary>
        /// Executa a quebra: o prop INTEIRO tomba — rb dinâmico absorve o
        /// impulso dosado no ponto de contato; colisor e visual caem juntos.
        /// </summary>
        public void Break(Vector3 pushDir, Vector3 contactPoint, float impulse)
        {
            if (_broken) return;
            _broken = true;
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
            _rb.mass = _debrisMass;
            _rb.isKinematic = false;
            // impulso no ponto de contato + levante sutil → tombo com arco
            _rb.AddForceAtPosition(pushDir * impulse * 0.4f + Vector3.up * impulse * 0.06f,
                contactPoint, ForceMode.Impulse);
            Foundation.ProceduralAudioService.Instance?.PlayCollisionThud(1f);
            StartCoroutine(ExpireAfterLifetime());
        }

        private System.Collections.IEnumerator ExpireAfterLifetime()
        {
            yield return new WaitForSeconds(_debrisLifetime);
            gameObject.SetActive(false);
        }
    }
}
