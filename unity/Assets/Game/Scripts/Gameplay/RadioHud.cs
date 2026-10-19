using UnityEngine;
using UnityEngine.UI;

namespace Studio.Gameplay
{
    /// <summary>HUD da rádio (topo-centro): nome da faixa + ◀▶/OFF.</summary>
    public class RadioHud : MonoBehaviour
    {
        [SerializeField] private Text _label;

        public void Bind(Text label) { _label = label; }

        private void Update()
        {
            var r = RadioSystem.Instance;
            if (_label != null && r != null)
                _label.text = r.CurrentTrack;
        }

        public void OnNext() => RadioSystem.Instance?.NextTrack();
        public void OnOff() => RadioSystem.Instance?.SetOff();
    }
}
