using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.World;

namespace Wendao.Systems.Feedback
{
    public sealed class AudioStateController : MonoBehaviour, IAudioStateService
    {
        public const float CombatCrossfadeSeconds = 1f;
        public const float BossCrossfadeSeconds = 1f;
        public const float ExploreCrossfadeSeconds = 1.5f;
        public const float CombatMusicExitDelaySeconds = 8f;
        public const float PostCombatFlagTailSeconds =
            CombatMusicExitDelaySeconds - FormulaLibrary.OutOfCombatRecoveryDelay;

        private IAudioService _audio;
        private string _sceneName = string.Empty;
        private string _bossBgmId = AudioContentIds.BossStoneGeneral;
        private bool _lastCombatFlag;
        private float _postCombatTailRemaining;
        private bool _registeredService;

        public BgmPlaybackState State { get; private set; }
        public string ExplorationBgmId { get; private set; } = string.Empty;
        public string DesiredBgmId { get; private set; } = string.Empty;
        public bool IsBossEncounter { get; private set; }
        public float PostCombatTailRemaining => _postCombatTailRemaining;
        public float LastTransitionDuration { get; private set; }
        public int TransitionCount { get; private set; }

        private void Awake()
        {
            if (ServiceLocator.TryGet<IAudioStateService>(
                    out IAudioStateService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            ServiceLocator.Register<IAudioStateService>(this);
            _registeredService = true;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Start()
        {
            ResolveAudio();
            RefreshForScene(SceneManager.GetActiveScene().name);
        }

        private void Update()
        {
            RepairServiceRegistration();
            TickState(Time.unscaledDeltaTime);
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (_registeredService
                && ServiceLocator.TryGet<IAudioStateService>(
                    out IAudioStateService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IAudioStateService>();
            }

            _registeredService = false;
        }

        public void SetBossEncounter(
            bool active,
            string bossBgmId = AudioContentIds.BossStoneGeneral)
        {
            if (active && AudioContentIds.IsBgm(bossBgmId))
            {
                _bossBgmId = bossBgmId;
            }

            if (IsBossEncounter == active)
            {
                return;
            }

            IsBossEncounter = active;
            TickState(0f);
        }

        public void EnsureRegistered()
        {
            RepairServiceRegistration();
        }

        public void RefreshForScene(string sceneName)
        {
            _sceneName = sceneName ?? string.Empty;
            ExplorationBgmId = ResolveExplorationBgm(_sceneName);
            _postCombatTailRemaining = 0f;
            _lastCombatFlag = GameManager.Instance?.IsInCombat == true;

            ResolveAudio();
            if (_audio != null)
            {
                _audio.SetAmbience(ResolveAmbience(_sceneName));
            }

            TickState(0f);
        }

        public void TickState(float deltaTime)
        {
            ResolveAudio();
            bool combatFlag = GameManager.Instance?.IsInCombat == true;
            if (combatFlag)
            {
                _postCombatTailRemaining = PostCombatFlagTailSeconds;
            }
            else if (_lastCombatFlag)
            {
                _postCombatTailRemaining = PostCombatFlagTailSeconds;
            }
            else if (_postCombatTailRemaining > 0f)
            {
                _postCombatTailRemaining = Mathf.Max(
                    0f,
                    _postCombatTailRemaining - Mathf.Max(0f, deltaTime));
            }

            _lastCombatFlag = combatFlag;
            if (IsBossEncounter)
            {
                TransitionTo(
                    BgmPlaybackState.Boss,
                    _bossBgmId,
                    BossCrossfadeSeconds);
                return;
            }

            if (combatFlag || _postCombatTailRemaining > 0f)
            {
                TransitionTo(
                    BgmPlaybackState.Combat,
                    AudioContentIds.CombatNormal,
                    CombatCrossfadeSeconds);
                return;
            }

            TransitionTo(
                string.IsNullOrEmpty(ExplorationBgmId)
                    ? BgmPlaybackState.None
                    : BgmPlaybackState.Explore,
                ExplorationBgmId,
                ExploreCrossfadeSeconds);
        }

        private void TransitionTo(
            BgmPlaybackState nextState,
            string bgmId,
            float duration)
        {
            if (State == nextState
                && string.Equals(DesiredBgmId, bgmId, StringComparison.Ordinal))
            {
                return;
            }

            State = nextState;
            DesiredBgmId = bgmId ?? string.Empty;
            LastTransitionDuration = Mathf.Max(0f, duration);
            TransitionCount++;
            _audio?.CrossfadeBGM(DesiredBgmId, LastTransitionDuration);
        }

        private void ResolveAudio()
        {
            if (_audio == null)
            {
                ServiceLocator.TryGet(out _audio);
            }
        }

        private void RepairServiceRegistration()
        {
            if (ServiceLocator.TryGet<IAudioStateService>(
                    out IAudioStateService current))
            {
                _registeredService = ReferenceEquals(current, this);
                return;
            }

            ServiceLocator.Register<IAudioStateService>(this);
            _registeredService = true;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            IsBossEncounter = false;
            RefreshForScene(scene.name);
        }

        private static string ResolveExplorationBgm(string sceneName)
        {
            if (sceneName == SceneLoader.DefaultMapSceneName)
            {
                return AudioContentIds.ExploreQingshi;
            }

            if (sceneName == SceneLoader.CangwuMapSceneName
                || sceneName == SceneLoader.BlackwindDungeonSceneName)
            {
                return AudioContentIds.ExploreCangwu;
            }

            return string.Empty;
        }

        private static string ResolveAmbience(string sceneName)
        {
            if (sceneName == SceneLoader.DefaultMapSceneName)
            {
                return AudioContentIds.WindPlain;
            }

            if (sceneName == SceneLoader.CangwuMapSceneName
                || sceneName == SceneLoader.BlackwindDungeonSceneName)
            {
                return AudioContentIds.WindMountain;
            }

            return string.Empty;
        }
    }
}
