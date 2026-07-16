using UnityEngine;
using Wendao.Core;
using Wendao.Data;

namespace Wendao.Systems.Feedback
{
    /// <summary>
    /// Applies the slot-independent settings file once on startup. The pause
    /// settings view reuses Apply so previewed audio matches persisted values.
    /// </summary>
    public sealed class GameSettingsRuntime : SafeBehaviour
    {
        private static GameSettingsRuntime _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            Apply(GameSettingsStore.LoadOrDefault(), true);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        public static void Apply(GameSettingsData settings, bool applyDisplay)
        {
            if (settings == null)
            {
                return;
            }

            settings.Sanitize();
            if (ServiceLocator.TryGet<IAudioService>(out IAudioService audio))
            {
                audio.SetVolumes(
                    settings.MasterVolume,
                    settings.BgmVolume,
                    settings.SfxVolume);
            }

            if (applyDisplay && !Application.isEditor)
            {
                Screen.fullScreen = settings.Fullscreen;
            }
        }
    }
}
