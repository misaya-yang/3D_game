using UnityEngine;
using Wendao.Core;
using Wendao.Data;

namespace Wendao.Systems.Combat
{
    /// <summary>
    /// Owns global hitstop and listens to resolved damage notifications.
    /// Uses unscaled time so the stop can end while gameplay time is frozen.
    /// </summary>
    public sealed class CombatFeelController : MonoBehaviour, ICombatFeelService
    {
        private bool _registeredService;
        private float _restoreTimeScale = 1f;

        public bool IsHitstopActive { get; private set; }
        public float HitstopRemaining { get; private set; }
        public float LastPlayedDuration { get; private set; }
        public int PlayCount { get; private set; }

        private void Awake()
        {
            if (ServiceLocator.TryGet<ICombatFeelService>(
                    out ICombatFeelService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            ServiceLocator.Register<ICombatFeelService>(this);
            _registeredService = true;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnEnable()
        {
            if (!_registeredService)
            {
                return;
            }

            EventBus.Subscribe<DamageInfo>(
                CombatEvents.DamageApplied,
                HandleDamageApplied);
            EventBus.Subscribe<GameStateInfo>(
                GameManager.GameStateChangedEvent,
                HandleGameStateChanged);
        }

        private void OnDisable()
        {
            if (!_registeredService)
            {
                return;
            }

            EventBus.Unsubscribe<DamageInfo>(
                CombatEvents.DamageApplied,
                HandleDamageApplied);
            EventBus.Unsubscribe<GameStateInfo>(
                GameManager.GameStateChangedEvent,
                HandleGameStateChanged);
            CancelHitstopInternal(ShouldRestoreTimeScale());
        }

        private void OnDestroy()
        {
            if (!_registeredService)
            {
                return;
            }

            if (ServiceLocator.TryGet<ICombatFeelService>(
                    out ICombatFeelService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<ICombatFeelService>();
            }

            _registeredService = false;
        }

        private void Update()
        {
            TickHitstop(Time.unscaledDeltaTime);
        }

        public void PlayHitstop(float seconds)
        {
            if (seconds <= 0f
                || float.IsNaN(seconds)
                || float.IsInfinity(seconds)
                || !IsGameplayPlaying())
            {
                return;
            }

            if (!IsHitstopActive)
            {
                if (Time.timeScale <= 0f)
                {
                    return;
                }

                _restoreTimeScale = Time.timeScale;
                IsHitstopActive = true;
            }

            HitstopRemaining = Mathf.Max(HitstopRemaining, seconds);
            LastPlayedDuration = seconds;
            PlayCount++;
            Time.timeScale = 0f;
        }

        public void TickHitstop(float unscaledDeltaTime)
        {
            if (!IsHitstopActive)
            {
                return;
            }

            GameManager gameManager = GameManager.Instance;
            if (gameManager != null && gameManager.State == GameState.Paused)
            {
                // Pause owns the freeze and suspends the hitstop countdown.
                Time.timeScale = 0f;
                return;
            }

            if (gameManager != null && gameManager.State != GameState.Playing)
            {
                CancelHitstopInternal(true);
                return;
            }

            if (unscaledDeltaTime > 0f
                && !float.IsNaN(unscaledDeltaTime)
                && !float.IsInfinity(unscaledDeltaTime))
            {
                HitstopRemaining = Mathf.Max(
                    0f,
                    HitstopRemaining - unscaledDeltaTime);
            }

            if (HitstopRemaining > 0f)
            {
                Time.timeScale = 0f;
                return;
            }

            CancelHitstopInternal(true);
        }

        public void CancelHitstop()
        {
            CancelHitstopInternal(ShouldRestoreTimeScale());
        }

        private void HandleDamageApplied(DamageInfo info)
        {
            if (info.Amount > 0f && info.HitstopSeconds > 0f)
            {
                PlayHitstop(info.HitstopSeconds);
            }
        }

        private void HandleGameStateChanged(GameStateInfo info)
        {
            if (!IsHitstopActive)
            {
                return;
            }

            if (info.Next == GameState.Paused
                || info.Next == GameState.Playing)
            {
                Time.timeScale = 0f;
                return;
            }

            CancelHitstopInternal(true);
        }

        private void CancelHitstopInternal(bool restoreTimeScale)
        {
            bool wasActive = IsHitstopActive;
            IsHitstopActive = false;
            HitstopRemaining = 0f;
            if (wasActive && restoreTimeScale && Time.timeScale <= 0f)
            {
                Time.timeScale = Mathf.Max(0.0001f, _restoreTimeScale);
            }
        }

        private static bool IsGameplayPlaying()
        {
            GameManager gameManager = GameManager.Instance;
            return gameManager == null || gameManager.State == GameState.Playing;
        }

        private static bool ShouldRestoreTimeScale()
        {
            GameManager gameManager = GameManager.Instance;
            return gameManager == null || gameManager.State != GameState.Paused;
        }
    }
}
