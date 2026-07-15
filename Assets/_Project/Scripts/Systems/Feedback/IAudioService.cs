using UnityEngine;

namespace Wendao.Systems.Feedback
{
    public interface IAudioService
    {
        string CurrentBgmId { get; }
        string PreviousBgmId { get; }
        string CurrentAmbienceId { get; }
        string LastSfxId { get; }
        float CrossfadeDuration { get; }
        float CrossfadeRemaining { get; }
        int SfxPlayCount { get; }

        void PlayBGM(string id, float fade = 1f);
        void CrossfadeBGM(string id, float duration = 2f);
        void PlaySFX(string id, float volume = 1f);
        void PlaySFXAt(string id, Vector3 position);
        void SetAmbience(string id);
        void SetVolumes(float master, float bgm, float sfx);
    }

    public enum BgmPlaybackState
    {
        None,
        Explore,
        Combat,
        Boss
    }

    public interface IAudioStateService
    {
        BgmPlaybackState State { get; }
        string ExplorationBgmId { get; }
        string DesiredBgmId { get; }
        bool IsBossEncounter { get; }

        void SetBossEncounter(
            bool active,
            string bossBgmId = AudioContentIds.BossStoneGeneral);
        void RefreshForScene(string sceneName);
        void TickState(float deltaTime);
    }
}
