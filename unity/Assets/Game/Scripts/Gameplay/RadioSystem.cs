using UnityEngine;

namespace Studio.Gameplay
{
    /// <summary>
    /// Rádio do carro estilo GTA: toca música só no modo Driving; pausa
    /// (mantém posição) ao sair a pé. Tecla R troca de faixa. As faixas
    /// são carregadas de Resources/Audio/Music e entram em loop.
    /// </summary>
    public class RadioSystem : MonoBehaviour
    {
        [SerializeField] private float _volume = 0.55f;
        private AudioSource _src;
        private AudioClip[] _tracks;
        private int _idx;
        private bool _driving = true;

        public static RadioSystem Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            _src = gameObject.AddComponent<AudioSource>();
            _src.spatialBlend = 0f;
            _src.volume = _volume;
            _src.loop = true;
            _src.playOnAwake = false;
            _tracks = Resources.LoadAll<AudioClip>("Audio/Music");
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Chamado pelo ModeManager ao alternar Driving/OnFoot.</summary>
        public void OnModeChanged(bool driving)
        {
            if (_src == null || _tracks.Length == 0) return;
            if (driving) { if (!_src.isPlaying) { _src.clip = _tracks[_idx]; _src.Play(); } }
            else _src.Pause(); // preserva posição — igual rádio do GTA
        }

        private void Update()
        {
            // Estado inicial: jogo nasce dirigindo → começa a tocar
            if (_driving && _src != null && !_src.isPlaying && _tracks.Length > 0)
            { _src.clip = _tracks[_idx]; _src.Play(); }
            if (_driving && UnityEngine.Input.GetKeyDown(KeyCode.R)) NextTrack();
        }

        public void SetDriving(bool driving) { _driving = driving; OnModeChanged(driving); }

        private bool _radioOn = true;

        public string CurrentTrack => _radioOn && _tracks.Length > 0
            ? _tracks[_idx].name : "OFF";

        /// <summary>Botão da HUD: cicla faixas e desliga no fim (GTA-style).</summary>
        public void ToggleOrNext()
        {
            if (!_radioOn) { _radioOn = true; _src.Play(); return; }
            NextTrack();
        }

        /// <summary>Desliga a rádio (botão OFF da HUD).</summary>
        public void SetOff() { _radioOn = false; _src.Stop(); }

        /// <summary>Liga/desliga (segurar no botão).</summary>
        public void ToggleOnOff()
        {
            _radioOn = !_radioOn;
            if (_radioOn) { if (_tracks.Length > 0) { _src.clip = _tracks[_idx]; _src.Play(); } }
            else _src.Stop();
        }

        /// <summary>Troca de faixa (R no carro).</summary>
        public void NextTrack()
        {
            if (_tracks.Length == 0) return;
            _radioOn = true;
            _idx = (_idx + 1) % _tracks.Length;
            _src.clip = _tracks[_idx];
            _src.Play();
        }

        /// <summary>Chamado todo frame pelo input adapter enquanto driving.</summary>
        public void Tick(bool driving)
        {
            if (driving && UnityEngine.Input.GetKeyDown(KeyCode.R)) NextTrack();
        }
    }
}
