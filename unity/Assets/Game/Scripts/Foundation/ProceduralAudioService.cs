using System;
using UnityEngine;

namespace Studio.Foundation
{
    [RequireComponent(typeof(AudioSource))]
    public class ProceduralAudioService : MonoBehaviour
    {
        private static ProceduralAudioService _instance;
        public static ProceduralAudioService Instance => _instance;

        private AudioSource _audioSource;
        private AudioClip _collectClip;
        private AudioClip _jumpClip;
        private AudioClip _slideClip;
        private AudioClip _hitClip;
        private AudioClip _victoryClip;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f; // 2D Audio

            GenerateAllClips();
        }

        private void GenerateAllClips()
        {
            _collectClip = GenerateChimeClip(880f, 1320f, 0.20f, "CollectSFX");
            _jumpClip = GenerateSweepClip(220f, 660f, 0.22f, "JumpSFX");
            _slideClip = GenerateNoiseClip(0.30f, "SlideSFX");
            _hitClip = GenerateHitClip(150f, 50f, 0.35f, "HitSFX");
            _victoryClip = GenerateVictoryArpeggio("VictorySFX");
        }

        public void PlayCollect()
        {
            if (_audioSource != null && _collectClip != null)
                _audioSource.PlayOneShot(_collectClip, 0.7f);
        }

        public void PlayJump()
        {
            if (_audioSource != null && _jumpClip != null)
                _audioSource.PlayOneShot(_jumpClip, 0.6f);
        }

        public void PlaySlide()
        {
            if (_audioSource != null && _slideClip != null)
                _audioSource.PlayOneShot(_slideClip, 0.5f);
        }

        public void PlayHit()
        {
            if (_audioSource != null && _hitClip != null)
                _audioSource.PlayOneShot(_hitClip, 0.9f);
        }

        public void PlayVictory()
        {
            if (_audioSource != null && _victoryClip != null)
                _audioSource.PlayOneShot(_victoryClip, 0.8f);
        }

        #region Sound Synthesizers

        private AudioClip GenerateChimeClip(float freq1, float freq2, float duration, string name)
        {
            int sampleRate = 44100;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 12f); // Decaimento exponencial rápido
                float wave1 = Mathf.Sin(2f * Mathf.PI * freq1 * t);
                float wave2 = Mathf.Sin(2f * Mathf.PI * freq2 * t);
                samples[i] = (wave1 * 0.5f + wave2 * 0.5f) * envelope;
            }

            var clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateSweepClip(float startFreq, float endFreq, float duration, string name)
        {
            int sampleRate = 44100;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / totalSamples;
                float currentFreq = Mathf.Lerp(startFreq, endFreq, t);
                float phase = 2f * Mathf.PI * currentFreq * ((float)i / sampleRate);
                float envelope = Mathf.Sin(t * Mathf.PI); // Sino suave
                samples[i] = Mathf.Sin(phase) * envelope * 0.6f;
            }

            var clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateNoiseClip(float duration, string name)
        {
            int sampleRate = 44100;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];
            System.Random rand = new System.Random(42);

            float lastSample = 0f;
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / totalSamples;
                float raw = (float)(rand.NextDouble() * 2.0 - 1.0);
                // Filtro passa-baixa simples para simular fricção
                lastSample = Mathf.Lerp(lastSample, raw, 0.15f);
                float envelope = Mathf.Sin(t * Mathf.PI);
                samples[i] = lastSample * envelope * 0.4f;
            }

            var clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateHitClip(float startFreq, float endFreq, float duration, string name)
        {
            int sampleRate = 44100;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];
            System.Random rand = new System.Random(99);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / totalSamples;
                float currentFreq = Mathf.Lerp(startFreq, endFreq, t);
                float phase = 2f * Mathf.PI * currentFreq * ((float)i / sampleRate);
                float tone = Mathf.Sin(phase);
                float noise = (float)(rand.NextDouble() * 2.0 - 1.0);
                float envelope = Mathf.Exp(-t * 8f);
                samples[i] = (tone * 0.6f + noise * 0.4f) * envelope * 0.7f;
            }

            var clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateVictoryArpeggio(string name)
        {
            float[] freqs = new float[] { 523.25f, 659.25f, 783.99f, 1046.50f }; // C5, E5, G5, C6
            float noteDuration = 0.12f;
            float totalDuration = noteDuration * freqs.Length + 0.3f;
            int sampleRate = 44100;
            int totalSamples = (int)(sampleRate * totalDuration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float timeSec = (float)i / sampleRate;
                int noteIndex = Mathf.Min(freqs.Length - 1, (int)(timeSec / noteDuration));
                float noteTime = timeSec - (noteIndex * noteDuration);
                float freq = freqs[noteIndex];

                float wave = Mathf.Sin(2f * Mathf.PI * freq * timeSec);
                float env = Mathf.Exp(-noteTime * 4f);
                samples[i] = wave * env * 0.5f;
            }

            var clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        #endregion
    }
}
