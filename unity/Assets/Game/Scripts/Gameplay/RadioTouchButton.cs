using UnityEngine;
using UnityEngine.EventSystems;

namespace Studio.Gameplay
{
    /// <summary>
    /// Botão único da rádio (topo): TOQUE = próxima faixa, SEGURAR = liga/desliga.
    /// Usa pointer events direto (Button.onClick falha nesse canvas touch).
    /// </summary>
    public class RadioTouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private float _downAt = -1f;
        private const float HoldTime = 0.55f;

        public void OnPointerDown(PointerEventData e) { _downAt = Time.unscaledTime; }

        public void OnPointerUp(PointerEventData e)
        {
            if (_downAt < 0f) return;
            var r = RadioSystem.Instance;
            if (r == null) { _downAt = -1f; return; }
            if (Time.unscaledTime - _downAt >= HoldTime) r.ToggleOnOff();
            else r.NextTrack();
            _downAt = -1f;
        }
    }
}
