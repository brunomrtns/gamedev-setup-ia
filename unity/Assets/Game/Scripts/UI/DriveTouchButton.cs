using Studio.Input;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Studio.UI
{
    /// <summary>
    /// Botão touch de condução (gas/freio/esterço/drift): escreve estado "held"
    /// no DriveInputAdapter enquanto o dedo está pressionado.
    /// </summary>
    public class DriveTouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private DriveInputAdapter.DriveControl _control = DriveInputAdapter.DriveControl.Gas;

        private void OnDisable()
        {
            Set(false);
        }

        public void OnPointerDown(PointerEventData eventData) => Set(true);
        public void OnPointerUp(PointerEventData eventData) => Set(false);
        public void OnPointerExit(PointerEventData eventData) => Set(false);

        private void Set(bool held)
        {
            if (DriveInputAdapter.Instance != null)
            {
                DriveInputAdapter.Instance.SetVirtualControl(_control, held);
            }
        }
    }
}
