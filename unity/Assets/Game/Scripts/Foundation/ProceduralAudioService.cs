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
        private AudioClip _plasmaShotClip;
        private AudioClip _knifeSlashClip;
        private AudioClip _oxygenRefillClip;
        private AudioClip _reloadClip;
        private AudioClip _sonarPingClip;

        // NEON DISTRICT — canais contínuos (motor, skid, trilha)
        private AudioClip _thudClip;
        private AudioClip _gunshotClip;
        private AudioClip _dryFireClip;
        private AudioClip _impactMetalClip;
        private AudioClip _impactConcreteClip;
        private AudioClip _glassClip;
        private AudioClip _pickupClip;
        private AudioClip _carDoorClip;
        private AudioClip _hornClip;
        private AudioClip _footstepClip;
        private AudioClip _whooshClip;
        private AudioClip _casingClip;
        private AudioSource _engineSource;
        private AudioSource _skidSource;
        private AudioSource _musicSource;

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

            // Abyssal Breach Sub-Aquatic SFX
            _plasmaShotClip = GenerateHitClip(620f, 110f, 0.18f, "PlasmaCavitationSFX");
            _knifeSlashClip = GenerateSweepClip(480f, 160f, 0.15f, "KnifeSlashSFX");
            _oxygenRefillClip = GenerateSweepClip(320f, 940f, 0.32f, "OxygenRefillSFX");
            _reloadClip = GenerateChimeClip(440f, 660f, 0.25f, "ReloadSFX");
            _sonarPingClip = GenerateChimeClip(1240f, 1245f, 0.40f, "SonarPingSFX");

            // Neon District
            _thudClip = GenerateHitClip(85f, 32f, 0.32f, "CarCollisionThudSFX");
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

        public void PlayPlasmaShot()
        {
            if (_audioSource != null && _plasmaShotClip != null)
                _audioSource.PlayOneShot(_plasmaShotClip, 0.75f);
        }

        public void PlayKnifeSlash()
        {
            if (_audioSource != null && _knifeSlashClip != null)
                _audioSource.PlayOneShot(_knifeSlashClip, 0.7f);
        }

        public void PlayOxygenRefill()
        {
            if (_audioSource != null && _oxygenRefillClip != null)
                _audioSource.PlayOneShot(_oxygenRefillClip, 0.8f);
        }

        public void PlayReload()
        {
            if (_audioSource != null && _reloadClip != null)
                _audioSource.PlayOneShot(_reloadClip, 0.65f);
        }

        public void PlaySonarPing()
        {
            if (_audioSource != null && _sonarPingClip != null)
                _audioSource.PlayOneShot(_sonarPingClip, 0.5f);
        }

        #region Neon District — Motor, Skid, Colisão e Synthwave

        // Preferência por samples reais (Resources/Audio/SFX) — baixados do
        // Freesound via tools/audio/download_sfx.py. Procedural é o fallback.
        private static readonly System.Collections.Generic.Dictionary<string, AudioClip> _sfx = new();
        private static bool _sfxTried;

        private static AudioClip Sfx(string key)
        {
            if (!_sfxTried)
            {
                _sfxTried = true;
                foreach (var clip in Resources.LoadAll<AudioClip>("Audio/SFX"))
                    _sfx[clip.name.Split('_')[0]] = clip;
            }
            return _sfx.TryGetValue(key, out var clip2) ? clip2 : null;
        }

        private bool TryPlaySample(string key, float vol = 1f)
        {
            var c = Sfx(key);
            if (c != null && _audioSource != null) { _audioSource.PlayOneShot(c, vol); return true; }
            return false;
        }

        /// <summary>
        /// Batida com intensidade 0..1: leve raspa toca só o início do clip e
        /// baixo; capotamento toca completo. Teto de 0.75 pra não estourar.
        /// </summary>
        public void PlayCollisionThud(float intensity = 1f)
        {
            // Multi-contato de UMA batida dispara várias vezes — cooldown global
            if (Time.time - _lastThudGlobal < 0.28f) return;
            _lastThudGlobal = Time.time;
            float t = Mathf.Clamp01(intensity);
            float vol = Mathf.Lerp(0.15f, 0.75f, t);
            var clip = Sfx("crash");
            if (clip != null && _audioSource != null)
            {
                if (_crashSrc == null)
                {
                    _crashSrc = gameObject.AddComponent<AudioSource>();
                    _crashSrc.spatialBlend = 0f;
                    _crashSrc.playOnAwake = false;
                }
                _crashSrc.Stop();
                _crashSrc.clip = clip;
                _crashSrc.volume = vol;
                _crashSrc.Play();
                // Corta o rabo nos impactos leves — duration escala com força
                _crashSrc.SetScheduledEndTime(
                    AudioSettings.dspTime + clip.length * Mathf.Lerp(0.12f, 1f, t));
                return;
            }
            if (_audioSource != null && _thudClip != null)
                _audioSource.PlayOneShot(_thudClip, vol);
        }
        private AudioSource _crashSrc;
        private static float _lastThudGlobal = -99f;

        /// <summary>
        /// Batida 3D para veículos de tráfego — espacializada (só audível a
        /// ~25m), sem flood de som da IA. Volume e duração por intensidade.
        /// </summary>
        public void PlayCollisionThud3D(Vector3 pos, float intensity = 1f)
        {
            if (Time.time - _lastThudGlobal < 0.28f) return;
            _lastThudGlobal = Time.time;
            float t = Mathf.Clamp01(intensity);
            var clip = Sfx("crash") ?? _thudClip;
            if (clip == null) return;
            var go = new GameObject("_crash3d");
            go.transform.position = pos;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.spatialBlend = 1f; // full 3D
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 2f;
            src.maxDistance = 22f;
            src.volume = Mathf.Lerp(0.15f, 0.75f, t);
            src.Play();
            Destroy(go, clip.length * Mathf.Lerp(0.2f, 1f, t));
        }

        public void PlayExplosion()
        {
            if (!TryPlaySample("explosion", 1f)) PlayCollisionThud();
        }

        public void PlayHornReal()
        {
            if (!TryPlaySample("horn", 0.9f)) PlayHorn();
        }

        public void PlayGunshotReal()
        {
            TryPlaySample("gunshot", 0.85f);
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.F12))
            {
                var beep = Sfx("horn") ?? _thudClip;
                _audioSource.PlayOneShot(beep, 1f);
                Debug.Log("[Audio] F12 test beep tocado");
            }
        }

        /// <summary>Loop do motor: pitch e volume acompanham a velocidade.</summary>
        public void SetEngineState(float speedNorm, float throttle)
        {
            EnsureEngineSource();
            if (_engineSource == null) return;
            if (!_engineSource.isPlaying) _engineSource.Play();
            _engineSource.pitch = Mathf.Lerp(_engineSource.pitch, 0.72f + speedNorm * 1.5f + throttle * 0.18f, 8f * Time.deltaTime);
            _engineSource.volume = Mathf.Lerp(_engineSource.volume, 0.14f + speedNorm * 0.22f + throttle * 0.08f, 8f * Time.deltaTime);
        }

        /// <summary>Loop de derrapagem (ruído filtrado) enquanto drift ativo.</summary>
        public void SetSkidActive(bool active)
        {
            EnsureSkidSource();
            if (_skidSource == null) return;
            if (active && !_skidSource.isPlaying) _skidSource.Play();
            else if (!active && _skidSource.isPlaying) _skidSource.Stop();
        }

        /// <summary>Trilha synthwave procedural em loop (Am–F–C–G, 120 BPM).</summary>
        public void StartCruiseMusic()
        {
            if (_musicSource != null && _musicSource.isPlaying) return;
            var clip = GenerateSynthwaveLoop("NeonDistrictCruiseLoop");
            var src = gameObject.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.volume = 0.24f;
            src.spatialBlend = 0f;
            src.playOnAwake = false;
            src.Play();
            _musicSource = src;
        }

        /// <summary>Para motor+skid — carro desligado/explodido/modo a pé.</summary>
        public void SetEngineOff()
        {
            if (_engineSource != null) _engineSource.Stop();
            if (_skidSource != null) _skidSource.Stop();
        }

        private void EnsureEngineSource()
        {
            if (_engineSource != null) return;
            // Loop de motor real (Freesound) com fallback pro procedural
            var clip = Sfx("engine_loop") ?? GenerateEngineLoop("EngineLoopSFX");
            _engineSource = gameObject.AddComponent<AudioSource>();
            _engineSource.clip = clip;
            _engineSource.loop = true;
            _engineSource.volume = 0.14f;
            _engineSource.pitch = 0.72f;
            _engineSource.spatialBlend = 0f;
            _engineSource.playOnAwake = false;
        }

        private void EnsureSkidSource()
        {
            if (_skidSource != null) return;
            var clip = GenerateSkidLoop("SkidLoopSFX");
            _skidSource = gameObject.AddComponent<AudioSource>();
            _skidSource.clip = clip;
            _skidSource.loop = true;
            _skidSource.volume = 0.5f;
            _skidSource.spatialBlend = 0f;
            _skidSource.playOnAwake = false;
        }

        /// <summary>Drone de motor: serra 72 Hz + sub-seno, loop contínuo crossfeadado.</summary>
        private AudioClip GenerateEngineLoop(string name)
        {
            int sampleRate = 44100;
            int total = sampleRate; // 1s
            float[] samples = new float[total];
            float f0 = 72f;
            float last = 0f;
            for (int i = 0; i < total; i++)
            {
                float t = (float)i / sampleRate;
                float saw = 2f * ((t * f0) % 1f) - 1f;
                float saw2 = 2f * ((t * f0 * 2.02f) % 1f) - 1f;
                float sub = Mathf.Sin(2f * Mathf.PI * f0 * 0.5f * t);
                float raw = saw * 0.45f + saw2 * 0.18f + sub * 0.35f;
                last = Mathf.Lerp(last, raw, 0.35f); // lowpass suave
                samples[i] = last * 0.55f;
            }
            MakeSeamless(samples, 1024);
            var clip = AudioClip.Create(name, total, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>Ruído filtrado de pneu derrapando, loop contínuo.</summary>
        private AudioClip GenerateSkidLoop(string name)
        {
            int sampleRate = 44100;
            int total = (int)(sampleRate * 0.8f);
            float[] samples = new float[total];
            var rand = new System.Random(77);
            float last = 0f;
            for (int i = 0; i < total; i++)
            {
                float raw = (float)(rand.NextDouble() * 2.0 - 1.0);
                // Passa-alta aproximada (diferença do suavizado) → chiado de borracha
                last = Mathf.Lerp(last, raw, 0.08f);
                samples[i] = (raw - last) * 0.9f;
            }
            MakeSeamless(samples, 2048);
            var clip = AudioClip.Create(name, total, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Synthwave procedural ~8s @120 BPM: kick nos tempos, baixo em colcheias,
        /// pads de acordes detunados, arpejo em semicolcheias e hats no contratempo.
        /// Progressão Am – F – C – G (1 bar = 2s).
        /// </summary>
        private AudioClip GenerateSynthwaveLoop(string name)
        {
            const int sampleRate = 44100;
            const float duration = 8f;
            int total = (int)(sampleRate * duration);
            float[] samples = new float[total];
            var rand = new System.Random(1337);

            float[] bassRoots = { 110f, 87.31f, 130.81f, 98f };          // A2 F2 C3 G2
            float[][] pads = {
                new[] { 220f, 261.63f, 329.63f },                        // Am
                new[] { 174.61f, 220f, 261.63f },                        // F
                new[] { 261.63f, 329.63f, 392f },                        // C
                new[] { 196f, 246.94f, 293.66f },                        // G
            };

            const float eighth = 0.25f;
            const float sixteenth = 0.125f;
            float noiseState = 0f;

            for (int i = 0; i < total; i++)
            {
                float t = (float)i / sampleRate;
                int bar = Mathf.Min(3, (int)(t / 2f));
                float s = 0f;

                // Pad: senos detunados do acorde do bar (sustentado, leve tremolo)
                foreach (var f in pads[bar])
                {
                    s += Mathf.Sin(2f * Mathf.PI * f * t) * 0.045f;
                    s += Mathf.Sin(2f * Mathf.PI * (f * 1.004f) * t) * 0.035f;
                }
                s *= 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 4.5f * t);

                // Baixo: colcheias (square com decay curto por nota)
                float eighthT = t % eighth;
                float bassEnv = Mathf.Exp(-eighthT * 9f);
                float sq = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * bassRoots[bar] * t));
                s += sq * bassEnv * 0.16f;

                // Arpejo: semicolcheias ciclando os tons do acorde 2 oitavas acima
                int step16 = (int)(t / sixteenth) % 12;
                float arpF = pads[bar][step16 % 3] * (step16 >= 6 ? 4f : 2f);
                float arpT = t % sixteenth;
                float arpEnv = Mathf.Exp(-arpT * 14f);
                s += Mathf.Sin(2f * Mathf.PI * arpF * t) * arpEnv * 0.11f;

                // Kick: tempos (0.5s) — seno com queda de pitch
                float quarterT = t % 0.5f;
                float kickEnv = Mathf.Exp(-quarterT * 18f);
                float kickF = Mathf.Lerp(120f, 42f, Mathf.Min(quarterT * 14f, 1f));
                s += Mathf.Sin(2f * Mathf.PI * kickF * quarterT) * kickEnv * 0.42f;

                // Hat: contratempos das colcheias (ruído HP curto)
                float hatT = (t + eighth * 0.5f) % eighth;
                float hatEnv = Mathf.Exp(-hatT * 45f);
                float nRaw = (float)(rand.NextDouble() * 2.0 - 1.0);
                noiseState = Mathf.Lerp(noiseState, nRaw, 0.12f);
                s += (nRaw - noiseState) * hatEnv * 0.35f;

                samples[i] = s * 0.8f;
            }

            // Fade-out de cauda para evitar clique no wrap do loop
            int fade = 2048;
            for (int i = 0; i < fade; i++)
            {
                float w = (float)i / fade;
                samples[total - fade + i] *= w;
            }

            var clip = AudioClip.Create(name, total, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>Crossfade cauda→cabeça para loops contínuos sem clique.</summary>
        private static void MakeSeamless(float[] samples, int fade)
        {
            for (int i = 0; i < fade; i++)
            {
                float w = (float)i / fade;
                int tail = samples.Length - fade + i;
                samples[tail] = Mathf.Lerp(samples[i], samples[tail], w);
            }
        }

        #endregion

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

        
        #region Neon District v2 — Tiro, Impacto, Pickup, Porta

        /// <summary>AK-47 com punch: crack de alta + corpo grave 80Hz + tail de ruído.</summary>
        // PlayGunshot procedural removido — só o sample real toca

        public void PlayDryFire()
        {
            if (_audioSource == null) return;
            if (_dryFireClip == null) _dryFireClip = GenerateClick("DryFireSFX", 0.05f, 1400f);
            _audioSource.PlayOneShot(_dryFireClip, 0.6f);
        }

        public void PlayImpactMetal()
        {
            if (!TryPlaySample("impact_metal", 0.9f)) PlayImpactMetalInternal();
        }

        public void PlayImpactMetalInternal()
        {
            if (_audioSource == null) return;
            if (_impactMetalClip == null) _impactMetalClip = GeneratePing("ImpactMetalSFX", 620f, 0.28f);
            _audioSource.PlayOneShot(_impactMetalClip, 0.55f);
        }

        public void PlayImpactConcrete()
        {
            if (_audioSource == null) return;
            if (_impactConcreteClip == null) _impactConcreteClip = GenerateShot("ImpactConcreteSFX", 0.14f, 220f);
            _audioSource.PlayOneShot(_impactConcreteClip, 0.5f);
        }

        public void PlayGlassBreak()
        {
            if (!TryPlaySample("glass", 0.9f)) PlayGlassBreakInternal();
        }

        public void PlayGlassBreakInternal()
        {
            if (_audioSource == null) return;
            if (_glassClip == null) _glassClip = GenerateGlass("GlassBreakSFX");
            _audioSource.PlayOneShot(_glassClip, 0.6f);
        }

        public void PlayPickup()
        {
            if (_audioSource == null) return;
            if (_pickupClip == null) _pickupClip = GeneratePickupChime("PickupSFX");
            _audioSource.PlayOneShot(_pickupClip, 0.7f);
        }

        public void PlayCarDoor()
        {
            if (_audioSource == null) return;
            if (_carDoorClip == null) _carDoorClip = GenerateShot("CarDoorSFX", 0.18f, 160f);
            _audioSource.PlayOneShot(_carDoorClip, 0.7f);
        }

        /// <summary>Buzina procedural (tráfego atingido).</summary>
        public void PlayHorn()
        {
            if (_audioSource == null) return;
            if (_hornClip == null) _hornClip = GenerateHorn("HornSFX");
            _audioSource.PlayOneShot(_hornClip, 0.8f);
        }

        public void PlayJumpWhoosh()
        {
            if (_audioSource == null) return;
            if (_whooshClip == null) _whooshClip = GenerateShot("JumpWhooshSFX", 0.3f, 500f);
            _audioSource.PlayOneShot(_whooshClip, 0.4f);
        }

        /// <summary>Tinkle de estojo caindo no chão.</summary>
        public void PlayCasingTinkle()
        {
            if (_audioSource == null) return;
            if (_casingClip == null) _casingClip = GeneratePing("CasingSFX", 2400f, 0.12f);
            _audioSource.PlayOneShot(_casingClip, 0.25f);
        }

        /// <summary>Passo por superfície (tick filtrado curto).</summary>
        public void PlayFootstep()
        {
            if (_audioSource == null) return;
            if (_footstepClip == null) _footstepClip = GenerateClick("FootstepSFX", 0.04f, 300f);
            _audioSource.PlayOneShot(_footstepClip, 0.3f);
        }

        /// <summary>Ruído com decay exponencial (tiro/thud/porta).</summary>
        private AudioClip GenerateShot(string name, float dur, float cutoff)
        {
            int sr = 44100, total = (int)(sr * dur);
            var s = new float[total];
            var rand = new System.Random(31);
            float last = 0f;
            for (int i = 0; i < total; i++)
            {
                float t = (float)i / sr;
                float env = Mathf.Exp(-t * 18f);
                float raw = (float)(rand.NextDouble() * 2.0 - 1.0);
                last = Mathf.Lerp(last, raw, Mathf.Clamp01(cutoff / 1000f));
                s[i] = (last * 0.7f + raw * 0.3f * env) * env;
            }
            var c = AudioClip.Create(name, total, 1, sr, false); c.SetData(s, 0); return c;
        }

        /// <summary>Tiro com corpo: crack de ruído + seno grave 90Hz + tail seco.</summary>
        private AudioClip GenerateGunshotPunch(string name)
        {
            int sr = 44100, total = (int)(sr * 0.28f);
            var s = new float[total];
            var rand = new System.Random(47);
            float low = 0f;
            for (int i = 0; i < total; i++)
            {
                float t = (float)i / sr;
                float crack = (float)(rand.NextDouble() * 2 - 1) * Mathf.Exp(-t * 40f);
                low = Mathf.Lerp(low, Mathf.Sin(2f * Mathf.PI * 90f * t), 0.4f);
                float body = low * Mathf.Exp(-t * 14f) * 0.9f;
                float tail = (float)(rand.NextDouble() * 2 - 1) * Mathf.Exp(-t * 7f) * 0.25f;
                s[i] = Mathf.Clamp((crack * 0.6f + body + tail) * 1.2f, -1f, 1f);
            }
            var c = AudioClip.Create(name, total, 1, sr, false); c.SetData(s, 0); return c;
        }

        /// <summary>Click/tick curto (tiro seco, passo).</summary>
        private AudioClip GenerateClick(string name, float dur, float freq)
        {
            int sr = 44100, total = (int)(sr * dur);
            var s = new float[total];
            for (int i = 0; i < total; i++)
            {
                float t = (float)i / sr;
                s[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-t * 60f) * 0.6f;
            }
            var c = AudioClip.Create(name, total, 1, sr, false); c.SetData(s, 0); return c;
        }

        /// <summary>Ping metálico decaindo (impacto em metal).</summary>
        private AudioClip GeneratePing(string name, float freq, float dur)
        {
            int sr = 44100, total = (int)(sr * dur);
            var s = new float[total];
            for (int i = 0; i < total; i++)
            {
                float t = (float)i / sr;
                float env = Mathf.Exp(-t * 9f);
                s[i] = (Mathf.Sin(2f * Mathf.PI * freq * t)
                      + 0.4f * Mathf.Sin(2f * Mathf.PI * freq * 2.76f * t)) * env * 0.5f;
            }
            var c = AudioClip.Create(name, total, 1, sr, false); c.SetData(s, 0); return c;
        }

        /// <summary>Caco de vidro: rajada de alta freq + faíscas.</summary>
        private AudioClip GenerateGlass(string name)
        {
            int sr = 44100, total = (int)(sr * 0.3f);
            var s = new float[total];
            var rand = new System.Random(77);
            for (int i = 0; i < total; i++)
            {
                float t = (float)i / sr;
                float env = Mathf.Exp(-t * 12f);
                float sparkle = Mathf.Sin(2f * Mathf.PI * (3800f + rand.Next(900)) * t) * 0.15f;
                s[i] = ((float)(rand.NextDouble() * 2 - 1) * 0.7f + sparkle) * env;
            }
            var c = AudioClip.Create(name, total, 1, sr, false); c.SetData(s, 0); return c;
        }

        /// <summary>Chime de pickup: duas notas ascendentes.</summary>
        private AudioClip GeneratePickupChime(string name)
        {
            int sr = 44100, total = (int)(sr * 0.35f);
            var s = new float[total];
            float[] f = { 660f, 990f };
            for (int i = 0; i < total; i++)
            {
                float t = (float)i / sr;
                int seg = t < 0.12f ? 0 : 1;
                float lt = t - seg * 0.12f;
                s[i] = Mathf.Sin(2f * Mathf.PI * f[seg] * lt) * Mathf.Exp(-lt * 8f) * 0.5f;
            }
            var c = AudioClip.Create(name, total, 1, sr, false); c.SetData(s, 0); return c;
        }

        /// <summary>Buzina: onda quadrada aproximada ~330Hz, 0.4s.</summary>
        private AudioClip GenerateHorn(string name)
        {
            int sr = 44100, total = (int)(sr * 0.4f);
            var s = new float[total];
            for (int i = 0; i < total; i++)
            {
                float t = (float)i / sr;
                float v = Mathf.Sin(2f * Mathf.PI * 330f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * 660f * t);
                s[i] = Mathf.Clamp(v, -1f, 1f) * 0.4f * Mathf.Min(1f, t * 30f) * Mathf.Exp(-t * 3f);
            }
            var c = AudioClip.Create(name, total, 1, sr, false); c.SetData(s, 0); return c;
        }

        #endregion

        #endregion
    }
}
