using Studio.Input;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Studio.UI
{
    /// <summary>
    /// Joystick virtual (lado esquerdo): arrastar alimenta DriveInputAdapter.FootMove.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform _knob;
        [SerializeField] private float _radius = 80f;

        public void OnPointerDown(PointerEventData e) => OnDrag(e);

        public void OnDrag(PointerEventData e)
        {
            var rect = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out var local);
            var v = Vector2.ClampMagnitude(local / _radius, 1f);
            DriveInputAdapter.Instance?.SetFootMove(v);
            if (_knob != null) _knob.anchoredPosition = v * _radius;
        }

        public void OnPointerUp(PointerEventData e)
        {
            DriveInputAdapter.Instance?.SetFootMove(Vector2.zero);
            if (_knob != null) _knob.anchoredPosition = Vector2.zero;
        }
    }

    /// <summary>
    /// Zona de look (lado direito da tela): drag alimenta DriveInputAdapter.FootLook.
    /// </summary>
    public class TouchLookZone : MonoBehaviour, IDragHandler
    {
        public void OnDrag(PointerEventData e)
        {
            DriveInputAdapter.Instance?.SetFootLook(e.delta * 0.12f);
        }
    }
}
