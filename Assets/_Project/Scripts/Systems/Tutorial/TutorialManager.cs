using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Combat;
using Wendao.Systems.Crafting;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Input;
using Wendao.Systems.Inventory;
using Wendao.Systems.Skill;
using Wendao.Systems.World;

namespace Wendao.Systems.Tutorial
{
    public sealed class TutorialManager : SafeBehaviour, ITutorialService
    {
        public const string TutorialPromptedEvent = "OnTutorialPrompted";

        public const string MoveTutorialId = "tut_move";
        public const string CombatTutorialId = "tut_combat";
        public const string SkillTutorialId = "tut_skill";
        public const string InventoryTutorialId = "tut_inv";
        public const string CultivationTutorialId = "tut_cult";
        public const string DungeonTutorialId = "tut_dungeon";
        public const string FlightTutorialId = "tut_flight";
        public const string AlchemyTutorialId = "tut_alchemy";

        public const string MoveStepId = "move";
        public const string LookStepId = "look";
        public const string JumpStepId = "jump";
        public const string CombatLightStepId = "light_attack";
        public const string CombatDefeatStepId = "defeat_dummy";
        public const string SkillOverviewStepId = "skill_overview";
        public const string InventoryOverviewStepId = "inventory_overview";
        public const string CultivationOverviewStepId = "cultivation_overview";
        public const string DungeonOverviewStepId = "dungeon_overview";
        public const string FlightOverviewStepId = "flight_overview";
        public const string AlchemyOverviewStepId = "alchemy_overview";
        public const string CompleteStepId = "complete";

        public const string MovePromptKey = "tutorial_move_move";
        public const string MovePromptDefault = "使用 WASD 或左摇杆移动。";
        public const string LookPromptKey = "tutorial_move_look";
        public const string LookPromptDefault = "移动鼠标或右摇杆转动视角。";
        public const string JumpPromptKey = "tutorial_move_jump";
        public const string JumpPromptDefault = "按空格键或手柄南键跳跃。";
        public const string MoveCompletePromptKey = "tutorial_move_complete";
        public const string MoveCompletePromptDefault = "基础身法已掌握。";

        // Backwards-compatible names used by the G-VS-01 acceptance tests.
        public const string CompletePromptKey = MoveCompletePromptKey;
        public const string CompletePromptDefault = MoveCompletePromptDefault;

        public const string CombatLightPromptKey = "tutorial_combat_light_attack";
        public const string CombatLightPromptDefault =
            "按鼠标左键或手柄右扳机进行轻击。";
        public const string CombatDefeatPromptKey = "tutorial_combat_defeat_dummy";
        public const string CombatDefeatPromptDefault =
            "击破前方木桩，完成战斗练习。";
        public const string CombatCompletePromptKey = "tutorial_combat_complete";
        public const string CombatCompletePromptDefault = "基础战斗已掌握。";

        public const string SkillPromptKey = "tutorial_skill_overview";
        public const string SkillPromptDefault =
            "按 K 打开功法面板，将已学功法拖入快捷栏后施展。";
        public const string InventoryPromptKey = "tutorial_inventory_overview";
        public const string InventoryPromptDefault =
            "按 B 打开背包，可使用丹药或穿戴装备。";
        public const string CultivationPromptKey = "tutorial_cultivation_overview";
        public const string CultivationPromptDefault =
            "气机圆满时按 C 打开角色面板，查看条件并尝试突破。";
        public const string DungeonPromptKey = "tutorial_dungeon_overview";
        public const string DungeonPromptDefault =
            "黑风秘境共五层；完成当层目标会记录检查点。";
        public const string FlightPromptKey = "tutorial_flight_overview";
        public const string FlightPromptDefault =
            "筑基后持有飞剑，可召剑起飞；禁飞区域需先落地。";
        public const string AlchemyPromptKey = "tutorial_alchemy_overview";
        public const string AlchemyPromptDefault =
            "选择丹方并备齐材料；丹火失衡时主材返还、辅材消耗。";
        public const string OptionalCompletePromptKey = "tutorial_optional_complete";
        public const string OptionalCompletePromptDefault = "指引已记录。";

        private const int DefaultSaveSlot = 0;
        private const string WorldSaveModule = "world";
        private const float CompletionPromptDuration = 2.5f;
        private const float InputThresholdSqr = 0.04f;
        private const float FollowUpTutorialDelay = 1.5f;

        private static readonly string[] MoveSteps =
        {
            MoveStepId,
            LookStepId,
            JumpStepId
        };

        private static readonly string[] CombatSteps =
        {
            CombatLightStepId,
            CombatDefeatStepId
        };

        private static readonly string[] SkillSteps = { SkillOverviewStepId };
        private static readonly string[] InventorySteps = { InventoryOverviewStepId };
        private static readonly string[] CultivationSteps =
            { CultivationOverviewStepId };
        private static readonly string[] DungeonSteps = { DungeonOverviewStepId };
        private static readonly string[] FlightSteps = { FlightOverviewStepId };
        private static readonly string[] AlchemySteps = { AlchemyOverviewStepId };

        private static readonly string[] TutorialIds =
        {
            MoveTutorialId,
            CombatTutorialId,
            SkillTutorialId,
            InventoryTutorialId,
            CultivationTutorialId,
            DungeonTutorialId,
            FlightTutorialId,
            AlchemyTutorialId
        };

        private readonly List<string> _pendingTutorialIds = new List<string>();

        private IPlayerInputSource _inputSource;
        private int _activeStepIndex = -1;
        private float _nextTutorialStartTime = -1f;
        private bool _scheduledCoreFollowUp;
        private bool _trainingDummyDefeated;
        private bool _registered;

        public bool IsActive { get; private set; }
        public string ActiveTutorialId { get; private set; } = string.Empty;
        public string ActiveStepId
        {
            get
            {
                string[] steps = GetSteps(ActiveTutorialId);
                return IsActive
                    && steps != null
                    && _activeStepIndex >= 0
                    && _activeStepIndex < steps.Length
                    ? steps[_activeStepIndex]
                    : string.Empty;
            }
        }

        public bool IsForced => IsActive && IsForcedTutorial(ActiveTutorialId);
        public IReadOnlyList<string> KnownTutorialIds => TutorialIds;

        private void Awake()
        {
            if (ServiceLocator.TryGet<ITutorialService>(out ITutorialService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            ServiceLocator.Register<ITutorialService>(this);
            _registered = true;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<MapInfo>(SceneLoader.MapLoadedEvent, HandleMapLoaded);
            EventBus.Subscribe<EnemyDeathInfo>(
                CombatEvents.EnemyKilled,
                HandleEnemyKilled);
            EventBus.Subscribe<ItemAcquireInfo>(
                InventoryEvents.ItemAcquired,
                HandleItemAcquired);
            EventBus.Subscribe<ItemUseInfo>(
                InventoryEvents.ItemUsed,
                HandleItemUsed);
            EventBus.Subscribe<EquipmentChangeInfo>(
                InventoryEvents.EquipmentChanged,
                HandleEquipmentChanged);
            EventBus.Subscribe<SkillInfo>(
                SkillEvents.SkillLearned,
                HandleSkillLearned);
            EventBus.Subscribe<SkillCastInfo>(
                SkillEvents.SkillCast,
                HandleSkillCast);
            EventBus.Subscribe<XpGainInfo>(
                CultivationEvents.XpGained,
                HandleXpGained);
            EventBus.Subscribe<RealmChangeInfo>(
                CultivationEvents.RealmBreakthrough,
                HandleRealmBreakthrough);
            EventBus.Subscribe<CraftResultInfo>(
                AlchemyEvents.CraftCompleted,
                HandleCraftResult);
            EventBus.Subscribe<CraftResultInfo>(
                AlchemyEvents.CraftFailed,
                HandleCraftResult);
            EventBus.Subscribe<BlackwindFloorInfo>(
                BlackwindDungeonEvents.FloorEntered,
                HandleBlackwindFloorEntered);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<MapInfo>(SceneLoader.MapLoadedEvent, HandleMapLoaded);
            EventBus.Unsubscribe<EnemyDeathInfo>(
                CombatEvents.EnemyKilled,
                HandleEnemyKilled);
            EventBus.Unsubscribe<ItemAcquireInfo>(
                InventoryEvents.ItemAcquired,
                HandleItemAcquired);
            EventBus.Unsubscribe<ItemUseInfo>(
                InventoryEvents.ItemUsed,
                HandleItemUsed);
            EventBus.Unsubscribe<EquipmentChangeInfo>(
                InventoryEvents.EquipmentChanged,
                HandleEquipmentChanged);
            EventBus.Unsubscribe<SkillInfo>(
                SkillEvents.SkillLearned,
                HandleSkillLearned);
            EventBus.Unsubscribe<SkillCastInfo>(
                SkillEvents.SkillCast,
                HandleSkillCast);
            EventBus.Unsubscribe<XpGainInfo>(
                CultivationEvents.XpGained,
                HandleXpGained);
            EventBus.Unsubscribe<RealmChangeInfo>(
                CultivationEvents.RealmBreakthrough,
                HandleRealmBreakthrough);
            EventBus.Unsubscribe<CraftResultInfo>(
                AlchemyEvents.CraftCompleted,
                HandleCraftResult);
            EventBus.Unsubscribe<CraftResultInfo>(
                AlchemyEvents.CraftFailed,
                HandleCraftResult);
            EventBus.Unsubscribe<BlackwindFloorInfo>(
                BlackwindDungeonEvents.FloorEntered,
                HandleBlackwindFloorEntered);
        }

        private void OnDestroy()
        {
            if (!_registered)
            {
                return;
            }

            if (ServiceLocator.TryGet<ITutorialService>(out ITutorialService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<ITutorialService>();
            }

            _registered = false;
        }

        protected override void SafeStart()
        {
            ResolveInputSource();
            if (SceneManager.GetActiveScene().name == SceneLoader.DefaultMapSceneName)
            {
                StartNextMapTutorial();
            }
        }

        private void Update()
        {
            if (!IsActive)
            {
                if (_nextTutorialStartTime >= 0f
                    && Time.unscaledTime >= _nextTutorialStartTime)
                {
                    _nextTutorialStartTime = -1f;
                    if (_scheduledCoreFollowUp)
                    {
                        _scheduledCoreFollowUp = false;
                        StartNextMapTutorial();
                    }
                    else
                    {
                        StartNextPendingTutorial();
                    }
                }

                return;
            }

            if (!IsInputSourceAlive())
            {
                _inputSource = null;
                if (!ResolveInputSource())
                {
                    return;
                }
            }

            if (ActiveTutorialId == MoveTutorialId)
            {
                TickMoveTutorial();
            }
            else if (ActiveTutorialId == CombatTutorialId
                && ActiveStepId == CombatLightStepId
                && _inputSource.LightAttackPressedThisFrame)
            {
                CompleteStep(CombatLightStepId);
            }
        }

        public bool TryStart(string tutorialId)
        {
            string[] steps = GetSteps(tutorialId);
            if (steps == null || IsActive || HasCompleted(tutorialId))
            {
                return false;
            }

            ActiveTutorialId = tutorialId;
            _activeStepIndex = 0;
            IsActive = true;
            ResolveInputSource();
            PublishActivePrompt();

            if (tutorialId == CombatTutorialId && _trainingDummyDefeated)
            {
                CompleteStep(CombatLightStepId);
                if (IsActive)
                {
                    CompleteStep(CombatDefeatStepId);
                }
            }

            return true;
        }

        public bool RequestStart(string tutorialId)
        {
            if (GetSteps(tutorialId) == null || HasCompleted(tutorialId))
            {
                return false;
            }

            if (IsActive)
            {
                if (string.Equals(
                        ActiveTutorialId,
                        tutorialId,
                        StringComparison.Ordinal)
                    || _pendingTutorialIds.Contains(tutorialId))
                {
                    return true;
                }

                _pendingTutorialIds.Add(tutorialId);
                return true;
            }

            return TryStart(tutorialId);
        }

        public void CompleteStep(string stepId)
        {
            if (!IsActive || !string.Equals(stepId, ActiveStepId, StringComparison.Ordinal))
            {
                return;
            }

            string[] steps = GetSteps(ActiveTutorialId);
            _activeStepIndex++;
            if (steps == null || _activeStepIndex >= steps.Length)
            {
                Complete(ActiveTutorialId);
                return;
            }

            PublishActivePrompt();
        }

        public void Complete(string tutorialId)
        {
            if (!IsActive
                || !string.Equals(tutorialId, ActiveTutorialId, StringComparison.Ordinal))
            {
                return;
            }

            SaveManager saveManager = SaveManager.Instance;
            List<string> completed = saveManager?.World?.TutorialsCompleted;
            if (completed == null)
            {
                Debug.LogError("Tutorial completion could not access world save data.", this);
                return;
            }

            if (!completed.Contains(tutorialId))
            {
                completed.Add(tutorialId);
            }

            string completedTutorialId = ActiveTutorialId;
            IsActive = false;
            ActiveTutorialId = string.Empty;
            _activeStepIndex = -1;

            PersistWorld(saveManager);
            PublishCompletionPrompt(completedTutorialId);
            _scheduledCoreFollowUp = completedTutorialId == MoveTutorialId
                && !HasCompleted(CombatTutorialId);
            if (_scheduledCoreFollowUp || _pendingTutorialIds.Count > 0)
            {
                _nextTutorialStartTime =
                    Time.unscaledTime + FollowUpTutorialDelay;
            }
        }

        public bool DismissCurrent()
        {
            if (!IsActive || IsForced)
            {
                return false;
            }

            Complete(ActiveTutorialId);
            return true;
        }

        public void Skip()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (IsActive)
            {
                Complete(ActiveTutorialId);
            }
#endif
        }

        public bool HasCompleted(string tutorialId)
        {
            if (string.IsNullOrWhiteSpace(tutorialId))
            {
                return false;
            }

            List<string> completed = SaveManager.Instance?.World?.TutorialsCompleted;
            return completed != null && completed.Contains(tutorialId);
        }

        public bool AllowsInput(TutorialInputAction action)
        {
            if (action == TutorialInputAction.Pause || !IsActive || !IsForced)
            {
                return true;
            }

            switch (ActiveTutorialId)
            {
                case MoveTutorialId:
                    switch (ActiveStepId)
                    {
                        case MoveStepId:
                            return action == TutorialInputAction.Move;
                        case LookStepId:
                            return action == TutorialInputAction.Look;
                        case JumpStepId:
                            return action == TutorialInputAction.Jump;
                        default:
                            return false;
                    }
                case CombatTutorialId:
                    if (ActiveStepId == CombatLightStepId)
                    {
                        return action == TutorialInputAction.LightAttack
                            || action == TutorialInputAction.Look;
                    }

                    return action == TutorialInputAction.Move
                        || action == TutorialInputAction.Look
                        || action == TutorialInputAction.LightAttack
                        || action == TutorialInputAction.LockOn;
                case FlightTutorialId:
                    return action == TutorialInputAction.Move
                        || action == TutorialInputAction.Look
                        || action == TutorialInputAction.Jump
                        || action == TutorialInputAction.Block
                        || action == TutorialInputAction.Mount;
                default:
                    return true;
            }
        }

        public void SetInputSource(IPlayerInputSource inputSource)
        {
            _inputSource = inputSource;
        }

        public void RepublishActivePrompt()
        {
            if (IsActive)
            {
                PublishActivePrompt();
            }
        }

        private void TickMoveTutorial()
        {
            switch (ActiveStepId)
            {
                case MoveStepId when _inputSource.Move.sqrMagnitude >= InputThresholdSqr:
                    CompleteStep(MoveStepId);
                    break;
                case LookStepId when _inputSource.Look.sqrMagnitude >= InputThresholdSqr:
                    CompleteStep(LookStepId);
                    break;
                case JumpStepId when _inputSource.JumpPressedThisFrame:
                    CompleteStep(JumpStepId);
                    break;
            }
        }

        private bool IsInputSourceAlive()
        {
            return _inputSource != null
                && (!(_inputSource is UnityEngine.Object unityObject)
                    || unityObject != null);
        }

        private void HandleMapLoaded(MapInfo info)
        {
            if (info.MapId == SceneLoader.DefaultMapId)
            {
                if (IsActive)
                {
                    PublishActivePrompt();
                }
                else
                {
                    StartNextMapTutorial();
                }
            }
            else if (info.MapId == SceneLoader.BlackwindMapId)
            {
                RequestStart(DungeonTutorialId);
            }
        }

        private void HandleEnemyKilled(EnemyDeathInfo info)
        {
            if (info.EnemyId != CombatContentIds.TrainingDummyEnemyId)
            {
                return;
            }

            _trainingDummyDefeated = true;
            if (!IsActive || ActiveTutorialId != CombatTutorialId)
            {
                return;
            }

            if (ActiveStepId == CombatLightStepId)
            {
                CompleteStep(CombatLightStepId);
            }

            if (IsActive && ActiveStepId == CombatDefeatStepId)
            {
                CompleteStep(CombatDefeatStepId);
            }
        }

        private void HandleItemAcquired(ItemAcquireInfo info)
        {
            if (info.Count > 0)
            {
                RequestStart(InventoryTutorialId);
            }
        }

        private void HandleItemUsed(ItemUseInfo info)
        {
            CompleteOptionalFromEvent(InventoryTutorialId);
        }

        private void HandleEquipmentChanged(EquipmentChangeInfo info)
        {
            if (!string.IsNullOrEmpty(info.NewItemId))
            {
                CompleteOptionalFromEvent(InventoryTutorialId);
            }
        }

        private void HandleSkillLearned(SkillInfo info)
        {
            if (!string.IsNullOrEmpty(info.SkillId))
            {
                RequestStart(SkillTutorialId);
            }
        }

        private void HandleSkillCast(SkillCastInfo info)
        {
            CompleteOptionalFromEvent(SkillTutorialId);
        }

        private void HandleXpGained(XpGainInfo info)
        {
            if (ServiceLocator.TryGet<ICultivationService>(
                    out ICultivationService cultivation)
                && cultivation.CanBreakthrough())
            {
                RequestStart(CultivationTutorialId);
            }
        }

        private void HandleRealmBreakthrough(RealmChangeInfo info)
        {
            if (info.Success)
            {
                CompleteOptionalFromEvent(CultivationTutorialId);
            }
        }

        private void HandleCraftResult(CraftResultInfo info)
        {
            CompleteOptionalFromEvent(AlchemyTutorialId);
        }

        private void HandleBlackwindFloorEntered(BlackwindFloorInfo info)
        {
            CompleteOptionalFromEvent(DungeonTutorialId);
        }

        private void CompleteOptionalFromEvent(string tutorialId)
        {
            if (IsActive
                && !IsForced
                && string.Equals(
                    ActiveTutorialId,
                    tutorialId,
                    StringComparison.Ordinal))
            {
                CompleteStep(ActiveStepId);
            }
        }

        private void StartNextMapTutorial()
        {
            if (IsActive)
            {
                return;
            }

            if (!HasCompleted(MoveTutorialId))
            {
                TryStart(MoveTutorialId);
            }
            else if (!HasCompleted(CombatTutorialId))
            {
                TryStart(CombatTutorialId);
            }
            else
            {
                StartNextPendingTutorial();
            }
        }

        private void StartNextPendingTutorial()
        {
            while (!IsActive && _pendingTutorialIds.Count > 0)
            {
                string tutorialId = _pendingTutorialIds[0];
                _pendingTutorialIds.RemoveAt(0);
                TryStart(tutorialId);
            }
        }

        private bool ResolveInputSource()
        {
            return ServiceLocator.TryGet(out _inputSource);
        }

        private void PublishActivePrompt()
        {
            switch (ActiveStepId)
            {
                case MoveStepId:
                    PublishPrompt(MovePromptKey, MovePromptDefault);
                    break;
                case LookStepId:
                    PublishPrompt(LookPromptKey, LookPromptDefault);
                    break;
                case JumpStepId:
                    PublishPrompt(JumpPromptKey, JumpPromptDefault);
                    break;
                case CombatLightStepId:
                    PublishPrompt(CombatLightPromptKey, CombatLightPromptDefault);
                    break;
                case CombatDefeatStepId:
                    PublishPrompt(CombatDefeatPromptKey, CombatDefeatPromptDefault);
                    break;
                case SkillOverviewStepId:
                    PublishPrompt(SkillPromptKey, SkillPromptDefault);
                    break;
                case InventoryOverviewStepId:
                    PublishPrompt(InventoryPromptKey, InventoryPromptDefault);
                    break;
                case CultivationOverviewStepId:
                    PublishPrompt(CultivationPromptKey, CultivationPromptDefault);
                    break;
                case DungeonOverviewStepId:
                    PublishPrompt(DungeonPromptKey, DungeonPromptDefault);
                    break;
                case FlightOverviewStepId:
                    PublishPrompt(FlightPromptKey, FlightPromptDefault);
                    break;
                case AlchemyOverviewStepId:
                    PublishPrompt(AlchemyPromptKey, AlchemyPromptDefault);
                    break;
            }
        }

        private void PublishPrompt(string localizationKey, string defaultValue)
        {
            EventBus.Publish(
                TutorialPromptedEvent,
                new TutorialPromptInfo
                {
                    TutorialId = ActiveTutorialId,
                    StepId = ActiveStepId,
                    LocalizationKey = localizationKey,
                    DefaultValue = defaultValue,
                    Duration = 0f,
                    IsForced = IsForced,
                    CanDismiss = !IsForced,
                    FocusRectNormalized = GetFocusRect(
                        ActiveTutorialId,
                        ActiveStepId)
                });
        }

        private static void PublishCompletionPrompt(string tutorialId)
        {
            string localizationKey = OptionalCompletePromptKey;
            string defaultValue = OptionalCompletePromptDefault;
            if (tutorialId == MoveTutorialId)
            {
                localizationKey = MoveCompletePromptKey;
                defaultValue = MoveCompletePromptDefault;
            }
            else if (tutorialId == CombatTutorialId)
            {
                localizationKey = CombatCompletePromptKey;
                defaultValue = CombatCompletePromptDefault;
            }

            EventBus.Publish(
                TutorialPromptedEvent,
                new TutorialPromptInfo
                {
                    TutorialId = tutorialId,
                    StepId = CompleteStepId,
                    LocalizationKey = localizationKey,
                    DefaultValue = defaultValue,
                    Duration = CompletionPromptDuration,
                    IsForced = false,
                    CanDismiss = false,
                    FocusRectNormalized = Rect.zero
                });
        }

        private void PersistWorld(SaveManager saveManager)
        {
            bool saved = saveManager.ActiveSlot >= 0
                ? saveManager.TrySaveModule(WorldSaveModule)
                : saveManager.SaveGame(DefaultSaveSlot);
            if (!saved)
            {
                Debug.LogError(
                    "Tutorial completion remains in memory but world.json could not be saved: "
                    + saveManager.LastError,
                    this);
            }
        }

        private static bool IsForcedTutorial(string tutorialId)
        {
            return tutorialId == MoveTutorialId
                || tutorialId == CombatTutorialId
                || tutorialId == FlightTutorialId;
        }

        private static Rect GetFocusRect(string tutorialId, string stepId)
        {
            switch (tutorialId)
            {
                case MoveTutorialId when stepId == MoveStepId:
                    return new Rect(0.02f, 0.02f, 0.25f, 0.24f);
                case MoveTutorialId when stepId == LookStepId:
                    return new Rect(0.38f, 0.3f, 0.24f, 0.4f);
                case MoveTutorialId when stepId == JumpStepId:
                    return new Rect(0.32f, 0.02f, 0.36f, 0.19f);
                case CombatTutorialId:
                    return new Rect(0.32f, 0.22f, 0.36f, 0.5f);
                case SkillTutorialId:
                    return new Rect(0.28f, 0.02f, 0.44f, 0.18f);
                case InventoryTutorialId:
                    return new Rect(0.02f, 0.24f, 0.32f, 0.5f);
                case CultivationTutorialId:
                    return new Rect(0.02f, 0.7f, 0.32f, 0.27f);
                case DungeonTutorialId:
                    return new Rect(0.35f, 0.3f, 0.3f, 0.38f);
                case FlightTutorialId:
                    return new Rect(0.34f, 0.28f, 0.32f, 0.44f);
                case AlchemyTutorialId:
                    return new Rect(0.18f, 0.12f, 0.64f, 0.72f);
                default:
                    return new Rect(0.35f, 0.35f, 0.3f, 0.3f);
            }
        }

        private static string[] GetSteps(string tutorialId)
        {
            switch (tutorialId)
            {
                case MoveTutorialId:
                    return MoveSteps;
                case CombatTutorialId:
                    return CombatSteps;
                case SkillTutorialId:
                    return SkillSteps;
                case InventoryTutorialId:
                    return InventorySteps;
                case CultivationTutorialId:
                    return CultivationSteps;
                case DungeonTutorialId:
                    return DungeonSteps;
                case FlightTutorialId:
                    return FlightSteps;
                case AlchemyTutorialId:
                    return AlchemySteps;
                default:
                    return null;
            }
        }
    }
}
