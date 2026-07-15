using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Combat;
using Wendao.Systems.Input;
using Wendao.Systems.Inventory;
using Wendao.Systems.Quest;
using Wendao.Systems.Skill;

namespace Wendao.Systems.Cultivation
{
    public sealed class CultivationManager : SafeBehaviour, ICultivationService
    {
        public const float TrainingDummyXpReward = 25f;
        public const float BreakthroughDurationSeconds = 3f;
        public const float BreakthroughResultDurationSeconds = 2f;
        public const float BeatTwoStartSeconds = 0.3f;
        public const float BeatThreeStartSeconds = 1.8f;
        public const float BeatFiveStartSeconds = 1.2f;
        public const float MaxNormalSuccessRate = 0.95f;
        public const float ResultToastDurationSeconds = 5f;

        private bool _registeredService;
        private BreakthroughState _breakthroughState;
        private float _breakthroughElapsed;
        private BreakthroughConfig _activeBreakthrough;
        private RealmType _activeSourceRealm;
        private RealmType _activeTargetRealm;
        private string _activeRequiredItemId = string.Empty;
        private float _activeSuccessRate;
        private bool _activePityConsumed;
        private bool _rollResolved;
        private bool _resultToastPublished;
        private ToastInfo _pendingResultToast;
        private IPlayerInputSource _lockedInput;
        private bool _inputWasEnabled;
        private Func<float> _randomValueProvider =
            () => UnityEngine.Random.value;

        public RealmType Realm
        {
            get
            {
                int value = SaveManager.Instance?.Profile?.Realm
                    ?? (int)RealmType.QiCondensation;
                return ConfigDatabase.Instance?.GetRealm(value) != null
                    ? (RealmType)value
                    : RealmType.QiCondensation;
            }
        }

        public int SubStage
        {
            get
            {
                RealmEntry realm = GetRealmEntry();
                int value = SaveManager.Instance?.Profile?.SubStage ?? 1;
                return Mathf.Clamp(value, 1, Mathf.Max(1, realm?.SubStages ?? 1));
            }
        }

        public float CurrentXp
        {
            get
            {
                float value = SaveManager.Instance?.Profile?.CultivationXp ?? 0f;
                return IsFinite(value) ? Mathf.Max(0f, value) : 0f;
            }
        }

        public float XpToNext
        {
            get
            {
                RealmEntry realm = GetRealmEntry();
                int index = SubStage - 1;
                return realm?.XpPerSubStage != null
                    && index >= 0
                    && index < realm.XpPerSubStage.Length
                    ? Mathf.Max(0f, realm.XpPerSubStage[index])
                    : 0f;
            }
        }

        public BreakthroughState CurrentBreakthroughState => _breakthroughState;
        public bool IsBreakingThrough =>
            _breakthroughState == BreakthroughState.BreakingThrough;
        public bool IsBreakthroughActive =>
            _breakthroughState != BreakthroughState.Idle;
        public bool IsBreakthroughInvincible => IsBreakingThrough;
        public int CeremonyBeat { get; private set; }
        public bool LastBreakthroughSucceeded { get; private set; }
        public float ActiveBreakthroughSuccessRate => _activeSuccessRate;

        private void Awake()
        {
            if (ServiceLocator.TryGet<ICultivationService>(
                    out ICultivationService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            ServiceLocator.Register<ICultivationService>(this);
            _registeredService = true;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<EnemyDeathInfo>(
                CombatEvents.EnemyKilled,
                HandleEnemyKilled);
            EventBus.Subscribe<QuestInfo>(
                QuestEvents.Accepted,
                HandleQuestAccepted);
            EventBus.Subscribe<GameStateInfo>(
                GameManager.GameStateChangedEvent,
                HandleGameStateChanged);
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        private void Update()
        {
            EnsureServiceRegistration();
            if (!IsBreakthroughActive)
            {
                return;
            }

            GameManager gameManager = GameManager.Instance;
            if (gameManager != null && gameManager.State == GameState.Paused)
            {
                return;
            }

            if (gameManager == null || gameManager.State != GameState.Playing)
            {
                InterruptBreakthrough();
                return;
            }

            TickBreakthrough(Time.deltaTime);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EnemyDeathInfo>(
                CombatEvents.EnemyKilled,
                HandleEnemyKilled);
            EventBus.Unsubscribe<QuestInfo>(
                QuestEvents.Accepted,
                HandleQuestAccepted);
            EventBus.Unsubscribe<GameStateInfo>(
                GameManager.GameStateChangedEvent,
                HandleGameStateChanged);
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            InterruptBreakthrough();
        }

        private void OnDestroy()
        {
            if (_registeredService
                && ServiceLocator.TryGet<ICultivationService>(
                    out ICultivationService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<ICultivationService>();
            }

            _registeredService = false;
        }

        public void AddXp(float amount, XpSourceType source)
        {
            SaveManager saveManager = SaveManager.Instance;
            SaveProfileData profile = saveManager?.Profile;
            if (profile == null || amount <= 0f || !IsFinite(amount))
            {
                return;
            }

            ServiceLocator.TryGet<ISpiritRootService>(
                out ISpiritRootService spiritRoot);
            ServiceLocator.TryGet<ICultivationStatsProvider>(
                out ICultivationStatsProvider stats);
            float rootMultiplier = Mathf.Max(
                0f,
                spiritRoot?.GetCultivationMultiplier() ?? 1f);
            float statMultiplier = Mathf.Max(
                0f,
                1f + (stats?.CultivationSpeed ?? 0f));
            float multiplier = rootMultiplier * statMultiplier;
            float gained = amount * multiplier;
            if (gained <= 0f || !IsFinite(gained))
            {
                return;
            }

            NormalizeProfile(profile);
            profile.CultivationXp += gained;
            int safety = 32;
            while (safety-- > 0 && TryAdvanceSubStageInternal(profile))
            {
            }

            RealmEntry currentRealm = GetRealmEntry();
            if (currentRealm != null
                && !CanLevelSubStage()
                && SubStage >= currentRealm.SubStages)
            {
                profile.CultivationXp = Mathf.Min(profile.CultivationXp, XpToNext);
            }

            PersistProfile(saveManager);
            EventBus.Publish(
                CultivationEvents.XpGained,
                new XpGainInfo
                {
                    Amount = gained,
                    Source = source,
                    MultiplierApplied = multiplier
                });
        }

        public float ApplyDeathXpPenalty(float percent)
        {
            SaveManager saveManager = SaveManager.Instance;
            SaveProfileData profile = saveManager?.Profile;
            if (profile == null || !IsFinite(percent) || percent < 0f)
            {
                return 0f;
            }

            NormalizeProfile(profile);
            float appliedPercent = Mathf.Clamp01(percent);
            float previousXp = profile.CultivationXp;
            float amountLost = previousXp * appliedPercent;
            profile.CultivationXp = Mathf.Max(0f, previousXp - amountLost);
            PersistProfile(saveManager);
            EventBus.Publish(
                CultivationEvents.DeathXpPenaltyApplied,
                new CultivationXpPenaltyInfo
                {
                    PreviousXp = previousXp,
                    AmountLost = amountLost,
                    CurrentXp = profile.CultivationXp,
                    Percent = appliedPercent
                });
            return amountLost;
        }

        public bool CanLevelSubStage()
        {
            RealmEntry realm = GetRealmEntry();
            float threshold = XpToNext;
            return realm != null
                && SubStage < realm.SubStages
                && threshold > 0f
                && CurrentXp + 0.0001f >= threshold;
        }

        public bool TryAdvanceSubStage()
        {
            SaveManager saveManager = SaveManager.Instance;
            SaveProfileData profile = saveManager?.Profile;
            if (profile == null || !TryAdvanceSubStageInternal(profile))
            {
                return false;
            }

            PersistProfile(saveManager);
            EventBus.Publish(
                CultivationEvents.XpGained,
                new XpGainInfo
                {
                    Amount = 0f,
                    Source = XpSourceType.Other,
                    MultiplierApplied = 1f
                });
            return true;
        }

        public bool CanBreakthrough()
        {
            return GetBreakthroughBlockers().Count == 0;
        }

        public IReadOnlyList<BreakthroughBlocker> GetBreakthroughBlockers()
        {
            var blockers = new List<BreakthroughBlocker>(5);
            if (IsBreakthroughActive)
            {
                blockers.Add(CreateBlocker(
                    CultivationContentIds.WrongStateBlocker,
                    CultivationContentIds.WrongStateMessageKey));
                return blockers;
            }

            RealmEntry realm = GetRealmEntry();
            BreakthroughConfig breakthrough = realm?.BreakthroughToNext;
            if (breakthrough == null
                || ConfigDatabase.Instance?.GetRealm((int)Realm + 1) == null)
            {
                blockers.Add(CreateBlocker(
                    CultivationContentIds.NoNextRealmBlocker,
                    CultivationContentIds.NoNextRealmMessageKey));
                return blockers;
            }

            if (SubStage < breakthrough.MinSubStage)
            {
                blockers.Add(CreateBlocker(
                    CultivationContentIds.NotMaxSubStageBlocker,
                    CultivationContentIds.NotMaxSubStageMessageKey));
            }

            string requiredItemId = breakthrough.RequiredItemId ?? string.Empty;
            if (!string.IsNullOrEmpty(requiredItemId)
                && (!ServiceLocator.TryGet<IInventoryService>(
                        out IInventoryService inventory)
                    || inventory.CountItem(requiredItemId) < 1))
            {
                blockers.Add(CreateBlocker(
                    CultivationContentIds.MissingItemBlocker,
                    CultivationContentIds.MissingItemMessageKey,
                    requiredItemId,
                    GetAcquisitionHintKeys(requiredItemId)));
            }

            GameManager gameManager = GameManager.Instance;
            if (gameManager != null && gameManager.IsInCombat)
            {
                blockers.Add(CreateBlocker(
                    CultivationContentIds.InCombatBlocker,
                    CultivationContentIds.InCombatMessageKey));
            }

            if (gameManager == null || gameManager.State != GameState.Playing)
            {
                blockers.Add(CreateBlocker(
                    CultivationContentIds.WrongStateBlocker,
                    CultivationContentIds.WrongStateMessageKey));
            }

            return blockers;
        }

        public float GetBreakthroughSuccessRate()
        {
            BreakthroughConfig breakthrough = GetRealmEntry()?.BreakthroughToNext;
            if (breakthrough == null)
            {
                return 0f;
            }

            RealmType targetRealm = (RealmType)((int)Realm + 1);
            if (targetRealm == RealmType.Foundation && IsFoundationPityAvailable())
            {
                return 1f;
            }

            return Mathf.Clamp(
                breakthrough.BaseSuccessRate,
                0f,
                MaxNormalSuccessRate);
        }

        public bool TryBreakthrough()
        {
            IReadOnlyList<BreakthroughBlocker> blockers =
                GetBreakthroughBlockers();
            if (blockers.Count > 0)
            {
                PublishBlockerToast(blockers[0]);
                return false;
            }

            SaveManager saveManager = SaveManager.Instance;
            SaveProfileData profile = saveManager?.Profile;
            RealmEntry realm = GetRealmEntry();
            BreakthroughConfig breakthrough = realm?.BreakthroughToNext;
            if (profile == null || breakthrough == null)
            {
                return false;
            }

            _activeBreakthrough = breakthrough;
            _activeSourceRealm = Realm;
            _activeTargetRealm = (RealmType)((int)_activeSourceRealm + 1);
            _activeRequiredItemId = breakthrough.RequiredItemId ?? string.Empty;
            _activeSuccessRate = GetBreakthroughSuccessRate();
            _activePityConsumed = _activeTargetRealm == RealmType.Foundation
                && IsFoundationPityAvailable();
            _rollResolved = false;
            _resultToastPublished = false;
            _pendingResultToast = default;
            LastBreakthroughSucceeded = false;

            if (_activePityConsumed)
            {
                SetFoundationPity(false);
                PersistWorld(saveManager);
            }

            LockGameplayInput();
            EnterState(BreakthroughState.BreakingThrough);
            return true;
        }

        public void TickBreakthrough(float deltaTime)
        {
            if (!IsBreakthroughActive
                || deltaTime <= 0f
                || !IsFinite(deltaTime))
            {
                return;
            }

            GameManager gameManager = GameManager.Instance;
            if (gameManager != null && gameManager.State == GameState.Paused)
            {
                return;
            }

            float remaining = deltaTime;
            int safety = 4;
            while (remaining > 0f && IsBreakthroughActive && safety-- > 0)
            {
                float stateDuration = IsBreakingThrough
                    ? BreakthroughDurationSeconds
                    : BreakthroughResultDurationSeconds;
                float untilBoundary = Mathf.Max(
                    0f,
                    stateDuration - _breakthroughElapsed);
                float step = Mathf.Min(remaining, untilBoundary);
                _breakthroughElapsed += step;
                remaining -= step;
                RefreshCeremonyBeat();

                if (_breakthroughElapsed + 0.0001f < stateDuration)
                {
                    break;
                }

                if (IsBreakingThrough)
                {
                    ResolveBreakthrough();
                }
                else
                {
                    FinishBreakthroughCeremony();
                }
            }
        }

        public bool InterruptBreakthrough()
        {
            if (!IsBreakthroughActive)
            {
                return false;
            }

            if (_activePityConsumed && !_rollResolved)
            {
                SetFoundationPity(true);
                PersistWorld(SaveManager.Instance);
            }

            ResetBreakthroughState();
            return true;
        }

        public void SetRandomValueProvider(Func<float> provider)
        {
            _randomValueProvider = provider ?? (() => UnityEngine.Random.value);
        }

        private bool TryAdvanceSubStageInternal(SaveProfileData profile)
        {
            if (profile == null || !CanLevelSubStage())
            {
                return false;
            }

            float threshold = XpToNext;
            profile.CultivationXp = Mathf.Max(0f, profile.CultivationXp - threshold);
            profile.SubStage = SubStage + 1;
            return true;
        }

        private void ResolveBreakthrough()
        {
            float roll = _randomValueProvider != null
                ? _randomValueProvider.Invoke()
                : UnityEngine.Random.value;
            if (!IsFinite(roll))
            {
                roll = 1f;
            }

            bool succeeded = _activeSuccessRate >= 1f
                || Mathf.Clamp01(roll) < _activeSuccessRate;
            RealmChangeInfo success = default;
            if (succeeded && !TryCommitSuccessfulBreakthrough(out success))
            {
                string missingItemId = _activeRequiredItemId;
                _rollResolved = false;
                InterruptBreakthrough();
                PublishBlockerToast(CreateBlocker(
                    CultivationContentIds.MissingItemBlocker,
                    CultivationContentIds.MissingItemMessageKey,
                    missingItemId,
                    GetAcquisitionHintKeys(missingItemId)));
                return;
            }

            _rollResolved = true;
            if (succeeded)
            {
                LastBreakthroughSucceeded = true;
                _pendingResultToast = new ToastInfo
                {
                    LocalizationKey = CultivationContentIds.SuccessMessageKey,
                    DefaultValue = CultivationContentIds.SuccessDefaultValue,
                    Duration = ResultToastDurationSeconds
                };
                EnterState(BreakthroughState.BreakthroughResult);
                EventBus.Publish(CultivationEvents.RealmBreakthrough, success);
                return;
            }

            RealmChangeInfo failure = CommitFailedBreakthrough();
            LastBreakthroughSucceeded = false;
            _pendingResultToast = BuildFailureToast();
            EnterState(BreakthroughState.BreakthroughResult);
            EventBus.Publish(CultivationEvents.RealmBreakthroughFailed, failure);
        }

        private bool TryCommitSuccessfulBreakthrough(out RealmChangeInfo info)
        {
            info = default;
            SaveManager saveManager = SaveManager.Instance;
            SaveProfileData profile = saveManager?.Profile;
            if (profile == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(_activeRequiredItemId)
                && (!ServiceLocator.TryGet<IInventoryService>(
                        out IInventoryService inventory)
                    || !inventory.RemoveItem(_activeRequiredItemId, 1)))
            {
                return false;
            }

            int previousSubStage = SubStage;
            profile.Realm = (int)_activeTargetRealm;
            profile.SubStage = 1;
            profile.CultivationXp = 0f;
            PersistProfile(saveManager);
            PersistInventory(saveManager);

            info = new RealmChangeInfo
            {
                PrevRealm = _activeSourceRealm,
                NewRealm = _activeTargetRealm,
                PrevSubStage = previousSubStage,
                NewSubStage = 1,
                Success = true,
                SuccessRate = _activeSuccessRate
            };
            return true;
        }

        private RealmChangeInfo CommitFailedBreakthrough()
        {
            SaveManager saveManager = SaveManager.Instance;
            SaveProfileData profile = saveManager?.Profile;
            int previousSubStage = SubStage;
            if (profile != null)
            {
                float penalty = Mathf.Clamp01(
                    _activeBreakthrough?.FailXpPenaltyPercent ?? 0f);
                profile.CultivationXp = Mathf.Max(
                    0f,
                    CurrentXp * (1f - penalty));
                PersistProfile(saveManager);
            }

            ApplyHeartDemon();
            return new RealmChangeInfo
            {
                PrevRealm = _activeSourceRealm,
                NewRealm = _activeSourceRealm,
                PrevSubStage = previousSubStage,
                NewSubStage = previousSubStage,
                Success = false,
                SuccessRate = _activeSuccessRate
            };
        }

        private void ApplyHeartDemon()
        {
            if (ServiceLocator.TryGet<IStatusEffectService>(
                    out IStatusEffectService statusEffects)
                && ServiceLocator.TryGet<IPlayerSkillCaster>(
                    out IPlayerSkillCaster player)
                && player.Actor != null)
            {
                statusEffects.Apply(
                    StatusEffectContentIds.HeartDemon,
                    player.Actor,
                    player.Actor);
            }
        }

        private ToastInfo BuildFailureToast()
        {
            float heartDemonSeconds = ConfigDatabase.Instance
                    ?.GetStatusEffect(StatusEffectContentIds.HeartDemon)
                    ?.Duration
                ?? 30f;
            float xpToRetry = Mathf.Max(0f, XpToNext - CurrentXp);
            string suggestion = _activeTargetRealm == RealmType.Foundation
                ? "调息稳固修为后再试；"
                    + CultivationContentIds.FoundationHintDefaultValue
                : "调息稳固修为后再试；"
                    + CultivationContentIds.GoldenCoreHintDefaultValue;
            return new ToastInfo
            {
                LocalizationKey = CultivationContentIds.FailureMessageKey,
                DefaultValue = string.Format(
                    CultivationContentIds.FailureDefaultValue,
                    _activeSuccessRate,
                    suggestion,
                    heartDemonSeconds,
                    xpToRetry),
                Duration = ResultToastDurationSeconds
            };
        }

        private void EnterState(BreakthroughState state)
        {
            _breakthroughState = state;
            _breakthroughElapsed = 0f;
            CeremonyBeat = state == BreakthroughState.BreakingThrough
                ? 1
                : state == BreakthroughState.BreakthroughResult
                    ? 4
                    : 0;
        }

        private void RefreshCeremonyBeat()
        {
            int nextBeat;
            if (_breakthroughState == BreakthroughState.BreakingThrough)
            {
                nextBeat = _breakthroughElapsed < BeatTwoStartSeconds
                    ? 1
                    : _breakthroughElapsed < BeatThreeStartSeconds
                        ? 2
                        : 3;
            }
            else if (_breakthroughState == BreakthroughState.BreakthroughResult)
            {
                nextBeat = _breakthroughElapsed < BeatFiveStartSeconds ? 4 : 5;
            }
            else
            {
                nextBeat = 0;
            }

            CeremonyBeat = nextBeat;
            if (nextBeat == 5)
            {
                PublishPendingResultToast();
            }
        }

        private void FinishBreakthroughCeremony()
        {
            PublishPendingResultToast();
            ResetBreakthroughState();
        }

        private void ResetBreakthroughState()
        {
            UnlockGameplayInput();
            _breakthroughState = BreakthroughState.Idle;
            _breakthroughElapsed = 0f;
            CeremonyBeat = 0;
            _activeBreakthrough = null;
            _activeSourceRealm = RealmType.Mortal;
            _activeTargetRealm = RealmType.Mortal;
            _activeRequiredItemId = string.Empty;
            _activeSuccessRate = 0f;
            _activePityConsumed = false;
            _rollResolved = false;
            _resultToastPublished = false;
            _pendingResultToast = default;
        }

        private void PublishPendingResultToast()
        {
            if (_resultToastPublished
                || string.IsNullOrWhiteSpace(_pendingResultToast.LocalizationKey))
            {
                return;
            }

            _resultToastPublished = true;
            EventBus.Publish(UiEvents.ToastRequested, _pendingResultToast);
        }

        private void LockGameplayInput()
        {
            _lockedInput = null;
            _inputWasEnabled = false;
            if (!ServiceLocator.TryGet<IPlayerInputSource>(
                    out IPlayerInputSource input))
            {
                return;
            }

            _lockedInput = input;
            _inputWasEnabled = input.IsEnabled;
            input.SetEnabled(false);
        }

        private void UnlockGameplayInput()
        {
            bool isAlive = _lockedInput != null
                && (!(_lockedInput is UnityEngine.Object unityObject)
                    || unityObject != null);
            if (isAlive)
            {
                _lockedInput.SetEnabled(_inputWasEnabled);
            }

            _lockedInput = null;
            _inputWasEnabled = false;
        }

        private void PublishBlockerToast(BreakthroughBlocker blocker)
        {
            EventBus.Publish(
                UiEvents.ToastRequested,
                new ToastInfo
                {
                    LocalizationKey = blocker.MessageKey,
                    DefaultValue = GetBlockerDefaultValue(blocker),
                    Duration = ResultToastDurationSeconds
                });
        }

        private string GetBlockerDefaultValue(BreakthroughBlocker blocker)
        {
            switch (blocker.Code)
            {
                case CultivationContentIds.NoNextRealmBlocker:
                    return CultivationContentIds.NoNextRealmDefaultValue;
                case CultivationContentIds.NotMaxSubStageBlocker:
                    RealmEntry realm = GetRealmEntry();
                    return string.Format(
                        CultivationContentIds.NotMaxSubStageDefaultValue,
                        realm?.Name ?? Realm.ToString(),
                        realm?.BreakthroughToNext?.MinSubStage ?? 1);
                case CultivationContentIds.MissingItemBlocker:
                    ItemData item = ConfigDatabase.Instance?.GetItem(
                        blocker.RelatedItemId);
                    return string.Format(
                        CultivationContentIds.MissingItemDefaultValue,
                        item?.DisplayName ?? blocker.RelatedItemId,
                        GetHintDefaultValue(blocker.RelatedItemId));
                case CultivationContentIds.InCombatBlocker:
                    return CultivationContentIds.InCombatDefaultValue;
                default:
                    return CultivationContentIds.WrongStateDefaultValue;
            }
        }

        private static BreakthroughBlocker CreateBlocker(
            string code,
            string messageKey,
            string relatedItemId = "",
            string[] acquisitionHintKeys = null)
        {
            return new BreakthroughBlocker
            {
                Code = code,
                MessageKey = messageKey,
                RelatedItemId = relatedItemId ?? string.Empty,
                AcquisitionHintKeys = acquisitionHintKeys ?? Array.Empty<string>()
            };
        }

        private static string[] GetAcquisitionHintKeys(string itemId)
        {
            if (string.Equals(
                    itemId,
                    InventoryContentIds.FoundationPill,
                    StringComparison.Ordinal))
            {
                return new[] { CultivationContentIds.FoundationHintKey };
            }

            if (string.Equals(
                    itemId,
                    InventoryContentIds.GoldenCorePill,
                    StringComparison.Ordinal))
            {
                return new[] { CultivationContentIds.GoldenCoreHintKey };
            }

            return Array.Empty<string>();
        }

        private static string GetHintDefaultValue(string itemId)
        {
            return string.Equals(
                itemId,
                InventoryContentIds.GoldenCorePill,
                StringComparison.Ordinal)
                ? CultivationContentIds.GoldenCoreHintDefaultValue
                : CultivationContentIds.FoundationHintDefaultValue;
        }

        private bool IsFoundationPityAvailable()
        {
            SaveWorldData world = SaveManager.Instance?.World;
            return world?.QuestFlags != null
                && world.QuestFlags.TryGetValue(
                    CultivationContentIds.FoundationPityFlag,
                    out bool enabled)
                && enabled;
        }

        private static void SetFoundationPity(bool enabled)
        {
            SaveWorldData world = SaveManager.Instance?.World;
            if (world == null)
            {
                return;
            }

            if (world.QuestFlags == null)
            {
                world.QuestFlags = new Dictionary<string, bool>(
                    StringComparer.Ordinal);
            }

            world.QuestFlags[CultivationContentIds.FoundationPityFlag] = enabled;
        }

        private void HandleQuestAccepted(QuestInfo info)
        {
            if (!string.Equals(
                    info.QuestId,
                    QuestContentIds.MainFoundationBreakthrough,
                    StringComparison.Ordinal)
                || Realm >= RealmType.Foundation)
            {
                return;
            }

            SetFoundationPity(true);
            PersistWorld(SaveManager.Instance);
        }

        private void HandleGameStateChanged(GameStateInfo info)
        {
            if (IsBreakthroughActive
                && info.Next != GameState.Playing
                && info.Next != GameState.Paused)
            {
                InterruptBreakthrough();
            }
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            if (IsBreakthroughActive)
            {
                InterruptBreakthrough();
            }
        }

        private void HandleEnemyKilled(EnemyDeathInfo info)
        {
            if (string.Equals(
                    info.EnemyId,
                    CombatContentIds.TrainingDummyEnemyId,
                    StringComparison.Ordinal))
            {
                AddXp(TrainingDummyXpReward, XpSourceType.Combat);
            }
        }

        private void EnsureServiceRegistration()
        {
            if (!ServiceLocator.TryGet<ICultivationService>(
                    out ICultivationService current))
            {
                ServiceLocator.Register<ICultivationService>(this);
                _registeredService = true;
            }
            else
            {
                _registeredService = ReferenceEquals(current, this);
            }
        }

        private RealmEntry GetRealmEntry()
        {
            return ConfigDatabase.Instance?.GetRealm((int)Realm);
        }

        private void NormalizeProfile(SaveProfileData profile)
        {
            RealmEntry realm = GetRealmEntry();
            profile.Realm = (int)Realm;
            profile.SubStage = Mathf.Clamp(
                profile.SubStage,
                1,
                Mathf.Max(1, realm?.SubStages ?? 1));
            if (!IsFinite(profile.CultivationXp) || profile.CultivationXp < 0f)
            {
                profile.CultivationXp = 0f;
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void PersistProfile(SaveManager saveManager)
        {
            if (saveManager != null && saveManager.ActiveSlot >= 0)
            {
                saveManager.TrySaveModule("profile");
            }
        }

        private static void PersistWorld(SaveManager saveManager)
        {
            if (saveManager != null && saveManager.ActiveSlot >= 0)
            {
                saveManager.TrySaveModule("world");
            }
        }

        private static void PersistInventory(SaveManager saveManager)
        {
            if (saveManager != null && saveManager.ActiveSlot >= 0)
            {
                saveManager.TrySaveModule(InventoryManager.SaveModuleName);
            }
        }
    }
}
