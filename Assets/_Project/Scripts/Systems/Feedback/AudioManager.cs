using System;
using System.Collections.Generic;
using UnityEngine;
using Wendao.Core;

namespace Wendao.Systems.Feedback
{
    public sealed class AudioManager : Singleton<AudioManager>, IAudioService
    {
        private const int SampleRate = 11025;
        private const float BgmClipSeconds = 2f;
        private const float AmbienceClipSeconds = 1f;
        private const float SfxClipSeconds = 0.16f;

        private readonly Dictionary<string, AudioClip> _clips =
            new Dictionary<string, AudioClip>(StringComparer.Ordinal);

        private AudioSource _bgmSourceA;
        private AudioSource _bgmSourceB;
        private AudioSource _currentBgmSource;
        private AudioSource _fadeOutBgmSource;
        private AudioSource _sfxSource;
        private AudioSource _ambienceSource;
        private float _masterVolume = 1f;
        private float _bgmVolume = 0.65f;
        private float _sfxVolume = 0.8f;

        public string CurrentBgmId { get; private set; } = string.Empty;
        public string PreviousBgmId { get; private set; } = string.Empty;
        public string CurrentAmbienceId { get; private set; } = string.Empty;
        public string LastSfxId { get; private set; } = string.Empty;
        public float CrossfadeDuration { get; private set; }
        public float CrossfadeRemaining { get; private set; }
        public int SfxPlayCount { get; private set; }
        public float MasterVolume => _masterVolume;
        public float BgmVolume => _bgmVolume;
        public float SfxVolume => _sfxVolume;

        protected override void OnSingletonAwake()
        {
            _bgmSourceA = CreateSource(true, 0f, 0f);
            _bgmSourceB = CreateSource(true, 0f, 0f);
            _sfxSource = CreateSource(false, 0f, EffectiveSfxVolume);
            _ambienceSource = CreateSource(true, 0f, 0f);

            EnsureRegistered();
        }

        protected override void OnSingletonDestroyed()
        {
            if (ServiceLocator.TryGet<IAudioService>(out IAudioService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IAudioService>();
            }

            foreach (AudioClip clip in _clips.Values)
            {
                if (clip != null)
                {
                    Destroy(clip);
                }
            }

            _clips.Clear();
        }

        private void Update()
        {
            EnsureRegistered();
            TickAudio(Time.unscaledDeltaTime);
        }

        public void EnsureRegistered()
        {
            if (ServiceLocator.TryGet<IAudioService>(out IAudioService current))
            {
                return;
            }

            ServiceLocator.Register<IAudioService>(this);
        }

        public void PlayBGM(string id, float fade = 1f)
        {
            CrossfadeBGM(id, fade);
        }

        public void CrossfadeBGM(string id, float duration = 2f)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                StopBgm(Mathf.Max(0f, duration));
                return;
            }

            if (!AudioContentIds.IsBgm(id)
                || string.Equals(CurrentBgmId, id, StringComparison.Ordinal))
            {
                return;
            }

            AudioSource next = ReferenceEquals(_currentBgmSource, _bgmSourceA)
                ? _bgmSourceB
                : _bgmSourceA;
            next.Stop();
            next.clip = GetOrCreateClip(id, AudioClipKind.Bgm);
            next.loop = true;
            next.volume = 0f;
            next.Play();

            PreviousBgmId = CurrentBgmId;
            CurrentBgmId = id;
            _fadeOutBgmSource = _currentBgmSource;
            _currentBgmSource = next;
            CrossfadeDuration = Mathf.Max(0f, duration);
            CrossfadeRemaining = CrossfadeDuration;

            if (CrossfadeDuration <= 0.0001f)
            {
                CompleteCrossfade();
            }
        }

        public void PlaySFX(string id, float volume = 1f)
        {
            if (!AudioContentIds.IsSfx(id) || _sfxSource == null)
            {
                return;
            }

            AudioClip clip = GetOrCreateClip(id, AudioClipKind.Sfx);
            _sfxSource.PlayOneShot(
                clip,
                Mathf.Clamp01(volume));
            LastSfxId = id;
            SfxPlayCount++;
        }

        public void PlaySFXAt(string id, Vector3 position)
        {
            if (!AudioContentIds.IsSfx(id))
            {
                return;
            }

            AudioClip clip = GetOrCreateClip(id, AudioClipKind.Sfx);
            var instance = new GameObject(id + "_AudioPlaceholder");
            instance.transform.position = position;
            AudioSource source = instance.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.maxDistance = 24f;
            source.volume = EffectiveSfxVolume;
            source.clip = clip;
            source.Play();
            Destroy(instance, Mathf.Max(0.05f, clip.length + 0.05f));
            LastSfxId = id;
            SfxPlayCount++;
        }

        public void SetAmbience(string id)
        {
            if (_ambienceSource == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                CurrentAmbienceId = string.Empty;
                _ambienceSource.Stop();
                _ambienceSource.clip = null;
                return;
            }

            if (!AudioContentIds.IsAmbience(id)
                || string.Equals(
                    CurrentAmbienceId,
                    id,
                    StringComparison.Ordinal))
            {
                return;
            }

            CurrentAmbienceId = id;
            _ambienceSource.clip = GetOrCreateClip(id, AudioClipKind.Ambience);
            _ambienceSource.loop = true;
            _ambienceSource.volume = EffectiveBgmVolume * 0.45f;
            _ambienceSource.Play();
        }

        public void SetVolumes(float master, float bgm, float sfx)
        {
            _masterVolume = Mathf.Clamp01(master);
            _bgmVolume = Mathf.Clamp01(bgm);
            _sfxVolume = Mathf.Clamp01(sfx);
            if (_sfxSource != null)
            {
                _sfxSource.volume = EffectiveSfxVolume;
            }

            if (_ambienceSource != null)
            {
                _ambienceSource.volume = EffectiveBgmVolume * 0.45f;
            }

            ApplyCurrentCrossfadeVolumes();
        }

        public void TickAudio(float deltaTime)
        {
            if (CrossfadeRemaining <= 0f || CrossfadeDuration <= 0f)
            {
                return;
            }

            CrossfadeRemaining = Mathf.Max(
                0f,
                CrossfadeRemaining - Mathf.Max(0f, deltaTime));
            ApplyCurrentCrossfadeVolumes();
            if (CrossfadeRemaining <= 0f)
            {
                CompleteCrossfade();
            }
        }

        private void StopBgm(float duration)
        {
            PreviousBgmId = CurrentBgmId;
            CurrentBgmId = string.Empty;
            _fadeOutBgmSource = _currentBgmSource;
            _currentBgmSource = null;
            CrossfadeDuration = duration;
            CrossfadeRemaining = duration;
            if (duration <= 0.0001f)
            {
                CompleteCrossfade();
            }
        }

        private void ApplyCurrentCrossfadeVolumes()
        {
            float progress = CrossfadeDuration <= 0.0001f
                ? 1f
                : 1f - Mathf.Clamp01(CrossfadeRemaining / CrossfadeDuration);
            if (_fadeOutBgmSource != null)
            {
                _fadeOutBgmSource.volume = EffectiveBgmVolume * (1f - progress);
            }

            if (_currentBgmSource != null)
            {
                _currentBgmSource.volume = EffectiveBgmVolume * progress;
            }
        }

        private void CompleteCrossfade()
        {
            CrossfadeRemaining = 0f;
            if (_fadeOutBgmSource != null)
            {
                _fadeOutBgmSource.Stop();
                _fadeOutBgmSource.clip = null;
            }

            _fadeOutBgmSource = null;
            if (_currentBgmSource != null)
            {
                _currentBgmSource.volume = EffectiveBgmVolume;
            }
        }

        private AudioSource CreateSource(
            bool loop,
            float spatialBlend,
            float volume)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = spatialBlend;
            source.volume = volume;
            return source;
        }

        private AudioClip GetOrCreateClip(string id, AudioClipKind kind)
        {
            if (_clips.TryGetValue(id, out AudioClip existing)
                && existing != null)
            {
                return existing;
            }

            float seconds = kind == AudioClipKind.Bgm
                ? BgmClipSeconds
                : kind == AudioClipKind.Ambience
                    ? AmbienceClipSeconds
                    : SfxClipSeconds;
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * seconds));
            var samples = new float[sampleCount];
            float frequency = ResolveFrequency(id, kind);
            float amplitude = kind == AudioClipKind.Sfx ? 0.16f : 0.045f;
            for (int sample = 0; sample < sampleCount; sample++)
            {
                float time = sample / (float)SampleRate;
                float envelope = kind == AudioClipKind.Sfx
                    ? Mathf.Clamp01(1f - sample / (float)sampleCount)
                    : 1f;
                float overtone = Mathf.Sin(2f * Mathf.PI * frequency * 0.5f * time);
                samples[sample] = amplitude * envelope
                    * (Mathf.Sin(2f * Mathf.PI * frequency * time)
                        + 0.22f * overtone);
            }

            AudioClip clip = AudioClip.Create(
                id + "_Placeholder",
                sampleCount,
                1,
                SampleRate,
                false);
            clip.SetData(samples, 0);
            _clips[id] = clip;
            return clip;
        }

        private static float ResolveFrequency(string id, AudioClipKind kind)
        {
            int checksum = 0;
            for (int index = 0; index < id.Length; index++)
            {
                checksum = (checksum + id[index] * (index + 1)) % 997;
            }

            float baseFrequency = kind == AudioClipKind.Bgm
                ? 110f
                : kind == AudioClipKind.Ambience
                    ? 70f
                    : 280f;
            return baseFrequency + checksum % (kind == AudioClipKind.Sfx ? 360 : 90);
        }

        private float EffectiveBgmVolume => _masterVolume * _bgmVolume;
        private float EffectiveSfxVolume => _masterVolume * _sfxVolume;

        private enum AudioClipKind
        {
            Bgm,
            Sfx,
            Ambience
        }
    }
}
