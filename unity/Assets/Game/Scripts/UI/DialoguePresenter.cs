using System;
using System.Collections.Generic;
using UnityEngine;

namespace Studio.UI
{
    [Serializable]
    public class DialogueLineData
    {
        public string id;
        public string character;
        public string display_name;
        public string language;
        public string text;
        public string subtitle_pt;
        public string subtitle_en;
        public string wav_file;
        public float duration_sec = 3f;
    }

    [Serializable]
    public class DialogueScenePackage
    {
        public string schema;
        public string scene;
        public List<DialogueLineData> lines = new List<DialogueLineData>();
    }

    /// <summary>
    /// Sistema runtime de Dialogos & Barks em gameplay (Auditoria P0-1 + P1-6 — Studio V2).
    /// Consome os mesmos JSONs gerados por dialogue_adapter.py / tts_bridge.py (vo_manifest.json),
    /// suporta alternancia de idioma (pt-BR / en), fila de barks e callback de amplitude para lip-sync.
    /// </summary>
    public class DialoguePresenter : MonoBehaviour
    {
        public string activeLanguage = "pt-BR";
        public AudioSource voiceAudioSource;
        public bool showDefaultOverlay = true;

        public event Action<DialogueLineData, string> OnLineStarted;
        public event Action<DialogueLineData> OnLineFinished;
        public event Action<float> OnVoiceAmplitude;

        private readonly Queue<(DialogueLineData line, AudioClip clip)> _queue =
            new Queue<(DialogueLineData, AudioClip)>();
        private DialogueLineData _current;
        private float _timer;
        private float[] _sampleBuf;
        private GUIStyle _boxStyle;
        private GUIStyle _textStyle;

        public DialogueLineData CurrentLine => _current;
        public bool IsSpeaking => _current != null;

        public static DialogueScenePackage ParsePackage(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            return JsonUtility.FromJson<DialogueScenePackage>(json);
        }

        public string ResolveSubtitle(DialogueLineData line)
        {
            if (line == null) return string.Empty;
            if (activeLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(line.subtitle_en))
                return line.subtitle_en;
            if (!string.IsNullOrEmpty(line.subtitle_pt))
                return line.subtitle_pt;
            return line.text ?? string.Empty;
        }

        public void EnqueueLine(DialogueLineData line, AudioClip clip = null)
        {
            if (line == null) return;
            _queue.Enqueue((line, clip));
            if (_current == null) Advance();
        }

        public void TriggerBark(string speaker, string text, float durationSec = 2.8f, AudioClip clip = null)
        {
            EnqueueLine(new DialogueLineData
            {
                id = $"bark_{Time.frameCount}",
                character = speaker,
                display_name = speaker,
                text = text,
                duration_sec = durationSec,
            }, clip);
        }

        public void SkipCurrent()
        {
            if (_current == null) return;
            if (voiceAudioSource != null && voiceAudioSource.isPlaying)
                voiceAudioSource.Stop();
            var finished = _current;
            _current = null;
            OnLineFinished?.Invoke(finished);
            Advance();
        }

        private void Advance()
        {
            if (_queue.Count == 0) return;
            var (line, clip) = _queue.Dequeue();
            _current = line;
            float dur = line.duration_sec > 0.1f ? line.duration_sec : 3.0f;
            if (clip != null)
            {
                dur = Mathf.Max(dur, clip.length);
                if (voiceAudioSource != null)
                {
                    voiceAudioSource.clip = clip;
                    voiceAudioSource.Play();
                }
            }
            _timer = dur;
            OnLineStarted?.Invoke(line, ResolveSubtitle(line));
        }

        private void Update()
        {
            if (_current == null) return;

            if (voiceAudioSource != null && voiceAudioSource.isPlaying && OnVoiceAmplitude != null)
            {
                if (_sampleBuf == null) _sampleBuf = new float[128];
                voiceAudioSource.GetOutputData(_sampleBuf, 0);
                float sum = 0f;
                for (int i = 0; i < _sampleBuf.Length; i++) sum += Mathf.Abs(_sampleBuf[i]);
                OnVoiceAmplitude.Invoke(Mathf.Clamp01((sum / _sampleBuf.Length) * 8f));
            }

            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                SkipCurrent();
            }
        }

        private void OnGUI()
        {
            if (!showDefaultOverlay || _current == null) return;
            if (_textStyle == null)
            {
                _boxStyle = new GUIStyle(GUI.skin.box);
                _textStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Max(16, Screen.height / 38),
                    wordWrap = true,
                    richText = true
                };
                _textStyle.normal.textColor = Color.white;
            }
            string speaker = string.IsNullOrEmpty(_current.display_name) ? _current.character : _current.display_name;
            string body = ResolveSubtitle(_current);
            string formatted = string.IsNullOrEmpty(speaker) ? body : $"<b>{speaker}:</b> {body}";

            float w = Screen.width * 0.72f;
            float h = Mathf.Max(68f, Screen.height * 0.10f);
            var rect = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 24f, w, h);
            GUI.Box(rect, GUIContent.none, _boxStyle);
            GUI.Label(new Rect(rect.x + 16, rect.y + 6, rect.width - 32, rect.height - 12), formatted, _textStyle);
        }
    }
}
