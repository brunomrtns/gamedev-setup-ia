using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace Studio.Cutscenes
{
    [Serializable]
    public class CutsceneShotData
    {
        public string id;
        public float startTime;
        public float duration;
        public Vector3 cameraFrom;
        public Vector3 cameraTo;
        public Vector3 lookAt;
        public float fov = 45f;
        public string speaker;
        public string subtitle;
        public string voClipPath;
    }

    /// <summary>
    /// Controlador runtime de Cutscenes declarativas (Studio V2 Fase 3).
    /// Sincronizado com o PlayableDirector (Unity Timeline) gerado pelo CutsceneBuilder:
    /// movimenta a camera cinematica ao longo dos shots e exibe letterbox + legendas.
    /// </summary>
    [RequireComponent(typeof(PlayableDirector))]
    public class CutscenePlayer : MonoBehaviour
    {
        public string cutsceneId;
        public float totalDuration;
        public bool playOnStart = false;
        public bool showLetterbox = true;
        public List<CutsceneShotData> shots = new List<CutsceneShotData>();

        private PlayableDirector _director;
        private Camera _cutsceneCam;
        private Camera _previousMainCam;
        private bool _isPlaying;
        private GUIStyle _subtitleStyle;
        private GUIStyle _shadowStyle;
        private Texture2D _blackTex;

        public event Action OnCutsceneFinished;

        private void Awake()
        {
            _director = GetComponent<PlayableDirector>();
            _cutsceneCam = GetComponentInChildren<Camera>(true);
            if (_cutsceneCam != null)
            {
                _cutsceneCam.enabled = false;
            }
        }

        private void Start()
        {
            if (playOnStart)
            {
                Play();
            }
        }

        public void Play()
        {
            if (_director == null) _director = GetComponent<PlayableDirector>();
            _previousMainCam = Camera.main;
            if (_previousMainCam != null && _previousMainCam != _cutsceneCam)
            {
                _previousMainCam.enabled = false;
            }
            if (_cutsceneCam != null)
            {
                _cutsceneCam.enabled = true;
            }
            _isPlaying = true;
            _director.time = 0;
            _director.Play();
        }

        public void Stop()
        {
            if (!_isPlaying) return;
            _isPlaying = false;
            if (_director != null) _director.Stop();
            if (_cutsceneCam != null) _cutsceneCam.enabled = false;
            if (_previousMainCam != null) _previousMainCam.enabled = true;
            OnCutsceneFinished?.Invoke();
        }

        private void Update()
        {
            if (!_isPlaying || _director == null) return;

            float t = (float)_director.time;
            if (t >= totalDuration && totalDuration > 0f)
            {
                Stop();
                return;
            }

            var shot = GetActiveShot(t);
            if (shot != null && _cutsceneCam != null)
            {
                float u = shot.duration > 0.001f ? Mathf.Clamp01((t - shot.startTime) / shot.duration) : 0f;
                float smooth = u * u * (3f - 2f * u);
                _cutsceneCam.transform.position = Vector3.Lerp(shot.cameraFrom, shot.cameraTo, smooth);
                _cutsceneCam.transform.LookAt(shot.lookAt);
                _cutsceneCam.fieldOfView = shot.fov > 5f ? shot.fov : 45f;
            }
        }

        public CutsceneShotData GetActiveShot(float timeSec)
        {
            for (int i = 0; i < shots.Count; i++)
            {
                var s = shots[i];
                if (timeSec >= s.startTime && timeSec <= s.startTime + s.duration)
                    return s;
            }
            return shots.Count > 0 ? shots[shots.Count - 1] : null;
        }

        private void OnGUI()
        {
            if (!_isPlaying || _director == null) return;

            if (_blackTex == null)
            {
                _blackTex = new Texture2D(1, 1);
                _blackTex.SetPixel(0, 0, Color.black);
                _blackTex.Apply();
            }

            if (showLetterbox)
            {
                float barH = Screen.height * 0.10f;
                GUI.DrawTexture(new Rect(0, 0, Screen.width, barH), _blackTex);
                GUI.DrawTexture(new Rect(0, Screen.height - barH, Screen.width, barH), _blackTex);
            }

            var shot = GetActiveShot((float)_director.time);
            if (shot == null || string.IsNullOrEmpty(shot.subtitle)) return;

            if (_subtitleStyle == null)
            {
                _subtitleStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Max(18, Screen.height / 34),
                    wordWrap = true,
                    richText = true
                };
                _subtitleStyle.normal.textColor = Color.white;

                _shadowStyle = new GUIStyle(_subtitleStyle);
                _shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            }

            string label = string.IsNullOrEmpty(shot.speaker)
                ? shot.subtitle
                : $"<b>{shot.speaker}:</b> {shot.subtitle}";

            float w = Screen.width * 0.80f;
            float h = Screen.height * 0.12f;
            var rect = new Rect((Screen.width - w) * 0.5f, Screen.height - h - Screen.height * 0.01f, w, h);
            var shadowRect = new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height);
            GUI.Label(shadowRect, label, _shadowStyle);
            GUI.Label(rect, label, _subtitleStyle);
        }
    }
}
