using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wendao.CameraSystem;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.Enemy;
using Wendao.Entities.Player;
using Wendao.Systems.Combat;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Equipment;
using Wendao.Systems.Input;
using Wendao.Systems.Inventory;
using Wendao.Systems.Quest;
using Wendao.Systems.Skill;
using Wendao.Systems.Tutorial;
using Wendao.Systems.World;
using Wendao.UI.Common;
using Wendao.UI.Cultivation;
using Wendao.UI.SceneFlow;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0201PlayModeTests
    {
        private string _storageRoot;
        private CultivationManager _cultivation;
        private IInventoryService _inventory;
        private IPlayerInputSource _input;
        private PlayerStats _player;
        private BreakthroughCeremonyView _ceremony;
        private GameToastView _toast;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            EventBus.Clear();
            DestroyRuntimeObjects();
            yield return null;
            ServiceLocator.Clear();

            SceneFlowBootstrap.Install();
            SceneUiBootstrap.Install();
            PlayerRuntimeBootstrap.Install();

            _storageRoot = Path.Combine(
                Path.GetTempPath(),
                "WendaoG0201Tests_" + Guid.NewGuid().ToString("N"));
            SaveManager.Instance.ConfigureStorageRoot(_storageRoot);
            Assert.That(SaveManager.Instance.SaveGame(0), Is.True);

            AsyncOperation load = SceneManager.LoadSceneAsync(
                SceneLoader.DefaultMapSceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;

            EnterPlayingState();
            PlayerRuntimeBootstrap.EnsureForScene(SceneManager.GetActiveScene());
            SceneUiBootstrap.EnsureForScene(SceneManager.GetActiveScene());
            yield return null;

            SpiritRootSelectionView rootView =
                Object.FindAnyObjectByType<SpiritRootSelectionView>();
            if (rootView != null && rootView.IsOpen)
            {
                Assert.That(rootView.SelectRoot(SpiritRootType.Fire), Is.True);
                rootView.ConfirmSelection();
            }

            _cultivation = Object.FindAnyObjectByType<CultivationManager>();
            _inventory = ServiceLocator.Get<IInventoryService>();
            _input = ServiceLocator.Get<IPlayerInputSource>();
            _player = Object.FindAnyObjectByType<PlayerStats>();
            _ceremony = Object.FindAnyObjectByType<BreakthroughCeremonyView>();
            _toast = Object.FindAnyObjectByType<GameToastView>();

            Assert.That(_cultivation, Is.Not.Null);
            Assert.That(_inventory, Is.Not.Null);
            Assert.That(_input, Is.Not.Null);
            Assert.That(_player, Is.Not.Null);
            Assert.That(_ceremony, Is.Not.Null);
            Assert.That(_toast, Is.Not.Null);
            Assert.That(_input.IsEnabled, Is.True);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            EventBus.Clear();
            DestroyRuntimeObjects();
            yield return null;
            ServiceLocator.Clear();

            if (!string.IsNullOrEmpty(_storageRoot)
                && Directory.Exists(_storageRoot))
            {
                Directory.Delete(_storageRoot, true);
            }
        }

        [Test]
        public void BlockersDriveToastForStageItemCombatStateAndClosedRealm()
        {
            SetProfile(RealmType.QiCondensation, 1, 0f);
            BreakthroughBlocker[] blockers =
                _cultivation.GetBreakthroughBlockers().ToArray();
            Assert.That(
                blockers.Select(blocker => blocker.Code),
                Does.Contain(CultivationContentIds.NotMaxSubStageBlocker));
            Assert.That(
                blockers.Select(blocker => blocker.Code),
                Does.Contain(CultivationContentIds.MissingItemBlocker));

            Assert.That(_cultivation.TryBreakthrough(), Is.False);
            Assert.That(
                _toast.CurrentLocalizationKey,
                Is.EqualTo(CultivationContentIds.NotMaxSubStageMessageKey));

            SetProfile(RealmType.QiCondensation, 9, 1000f);
            blockers = _cultivation.GetBreakthroughBlockers().ToArray();
            BreakthroughBlocker missing = blockers.Single(
                blocker => blocker.Code
                    == CultivationContentIds.MissingItemBlocker);
            Assert.That(
                missing.RelatedItemId,
                Is.EqualTo(InventoryContentIds.FoundationPill));
            Assert.That(
                missing.AcquisitionHintKeys,
                Does.Contain(CultivationContentIds.FoundationHintKey));
            Assert.That(_cultivation.TryBreakthrough(), Is.False);
            Assert.That(
                _toast.CurrentLocalizationKey,
                Is.EqualTo(CultivationContentIds.MissingItemMessageKey));

            AddItem(InventoryContentIds.FoundationPill);
            GameManager.Instance.SetCombatFlag(true);
            Assert.That(
                _cultivation.GetBreakthroughBlockers()
                    .Any(blocker => blocker.Code
                        == CultivationContentIds.InCombatBlocker),
                Is.True);
            GameManager.Instance.SetCombatFlag(false);

            Assert.That(
                GameManager.Instance.TrySetState(GameState.Dialogue),
                Is.True);
            Assert.That(
                _cultivation.GetBreakthroughBlockers()
                    .Any(blocker => blocker.Code
                        == CultivationContentIds.WrongStateBlocker),
                Is.True);
            Assert.That(
                GameManager.Instance.TrySetState(GameState.Playing),
                Is.True);

            SetProfile(RealmType.GoldenCore, 3, 50000f);
            Assert.That(_cultivation.CanBreakthrough(), Is.False);
            Assert.That(
                _cultivation.GetBreakthroughBlockers().Single().Code,
                Is.EqualTo(CultivationContentIds.NoNextRealmBlocker));
        }

        [Test]
        public void FiveBeatSuccessLocksInputGrantsThreeSecondInvincibilityAndConsumesPill()
        {
            SetProfile(RealmType.QiCondensation, 9, 3200f);
            AddItem(InventoryContentIds.FoundationPill);
            _cultivation.SetRandomValueProvider(() => 0f);
            RealmChangeInfo received = default;
            int eventCount = 0;
            Action<RealmChangeInfo> handler = info =>
            {
                received = info;
                eventCount++;
            };
            EventBus.Subscribe(CultivationEvents.RealmBreakthrough, handler);
            try
            {
                Assert.That(_cultivation.GetBreakthroughSuccessRate(), Is.EqualTo(0.85f));
                Assert.That(_cultivation.TryBreakthrough(), Is.True);
                Assert.That(_cultivation.CeremonyBeat, Is.EqualTo(1));
                Assert.That(_cultivation.IsBreakthroughInvincible, Is.True);
                Assert.That(_player.IsInvincible, Is.True);
                Assert.That(_input.IsEnabled, Is.False);

                TickAndRefresh(0.3f);
                Assert.That(_cultivation.CeremonyBeat, Is.EqualTo(2));
                TickAndRefresh(1.5f);
                Assert.That(_cultivation.CeremonyBeat, Is.EqualTo(3));
                Assert.That(_ceremony.CurrentBeat, Is.EqualTo(3));
                Assert.That(_ceremony.IsVisible, Is.True);

                TickAndRefresh(1.2f);
                Assert.That(_cultivation.CeremonyBeat, Is.EqualTo(4));
                Assert.That(_cultivation.IsBreakthroughInvincible, Is.False);
                Assert.That(_player.IsInvincible, Is.False);
                Assert.That(eventCount, Is.EqualTo(1));
                Assert.That(received.Success, Is.True);
                Assert.That(received.PrevRealm, Is.EqualTo(RealmType.QiCondensation));
                Assert.That(received.NewRealm, Is.EqualTo(RealmType.Foundation));
                Assert.That(_cultivation.Realm, Is.EqualTo(RealmType.Foundation));
                Assert.That(_cultivation.SubStage, Is.EqualTo(1));
                Assert.That(_cultivation.CurrentXp, Is.Zero);
                Assert.That(
                    _inventory.CountItem(InventoryContentIds.FoundationPill),
                    Is.Zero);
                Assert.That(_player.MaxHp, Is.EqualTo(600f));
                Assert.That(
                    _ceremony.CurrentLocalizationKey,
                    Is.EqualTo(CultivationContentIds.SuccessMessageKey));

                TickAndRefresh(1.2f);
                Assert.That(_cultivation.CeremonyBeat, Is.EqualTo(5));
                Assert.That(
                    _toast.CurrentLocalizationKey,
                    Is.EqualTo(CultivationContentIds.SuccessMessageKey));
                TickAndRefresh(0.8f);
                Assert.That(_cultivation.CeremonyBeat, Is.Zero);
                Assert.That(_cultivation.IsBreakthroughActive, Is.False);
                Assert.That(_input.IsEnabled, Is.True);
            }
            finally
            {
                EventBus.Unsubscribe(CultivationEvents.RealmBreakthrough, handler);
            }
        }

        [Test]
        public void FailureKeepsPillAppliesPenaltyHeartDemonAndDetailedToast()
        {
            SetProfile(RealmType.QiCondensation, 9, 1000f);
            AddItem(InventoryContentIds.FoundationPill);
            _cultivation.SetRandomValueProvider(() => 0.99f);
            RealmChangeInfo received = default;
            int eventCount = 0;
            Action<RealmChangeInfo> handler = info =>
            {
                received = info;
                eventCount++;
            };
            EventBus.Subscribe(
                CultivationEvents.RealmBreakthroughFailed,
                handler);
            try
            {
                Assert.That(_cultivation.TryBreakthrough(), Is.True);
                TickAndRefresh(3f);

                Assert.That(eventCount, Is.EqualTo(1));
                Assert.That(received.Success, Is.False);
                Assert.That(received.SuccessRate, Is.EqualTo(0.85f));
                Assert.That(_cultivation.Realm, Is.EqualTo(RealmType.QiCondensation));
                Assert.That(_cultivation.CurrentXp, Is.EqualTo(800f));
                Assert.That(
                    _inventory.CountItem(InventoryContentIds.FoundationPill),
                    Is.EqualTo(1));
                IStatusEffectService statuses =
                    ServiceLocator.Get<IStatusEffectService>();
                Assert.That(
                    statuses.Has(StatusEffectContentIds.HeartDemon, _player.gameObject),
                    Is.True);
                Assert.That(
                    statuses.GetRemainingDuration(
                        StatusEffectContentIds.HeartDemon,
                        _player.gameObject),
                    Is.EqualTo(30f).Within(0.01f));
                Assert.That(
                    _ceremony.CurrentLocalizationKey,
                    Is.EqualTo(CultivationContentIds.FailureCeremonyMessageKey));

                TickAndRefresh(1.2f);
                Assert.That(
                    _toast.CurrentLocalizationKey,
                    Is.EqualTo(CultivationContentIds.FailureMessageKey));
                Assert.That(_toast.CurrentDefaultValue, Does.Contain("85"));
                Assert.That(_toast.CurrentDefaultValue, Does.Contain("30"));
                TickAndRefresh(0.8f);
                Assert.That(_input.IsEnabled, Is.True);
            }
            finally
            {
                EventBus.Unsubscribe(
                    CultivationEvents.RealmBreakthroughFailed,
                    handler);
            }
        }

        [Test]
        public void FoundationPityConsumesOnStartRestoresOnInterruptAndPersistsAfterRoll()
        {
            SetProfile(RealmType.QiCondensation, 9, 1200f);
            AddItem(InventoryContentIds.FoundationPill);
            EventBus.Publish(
                QuestEvents.Accepted,
                new QuestInfo
                {
                    QuestId = QuestContentIds.MainFoundationBreakthrough,
                    Status = QuestStatus.Active
                });
            Assert.That(GetPityFlag(), Is.True);
            Assert.That(_cultivation.GetBreakthroughSuccessRate(), Is.EqualTo(1f));

            Assert.That(_cultivation.TryBreakthrough(), Is.True);
            Assert.That(GetPityFlag(), Is.False);
            TickAndRefresh(1f);
            Assert.That(_cultivation.InterruptBreakthrough(), Is.True);
            Assert.That(GetPityFlag(), Is.True);
            Assert.That(_cultivation.CurrentXp, Is.EqualTo(1200f));
            Assert.That(
                _inventory.CountItem(InventoryContentIds.FoundationPill),
                Is.EqualTo(1));
            Assert.That(_input.IsEnabled, Is.True);

            _cultivation.SetRandomValueProvider(() => 0.99f);
            Assert.That(_cultivation.TryBreakthrough(), Is.True);
            Assert.That(GetPityFlag(), Is.False);
            TickAndRefresh(3f);
            Assert.That(_cultivation.Realm, Is.EqualTo(RealmType.Foundation));
            Assert.That(GetPityFlag(), Is.False);
            Assert.That(
                _inventory.CountItem(InventoryContentIds.FoundationPill),
                Is.Zero);
        }

        [Test]
        public void FoundationCanReachGoldenCorePityIsIgnoredAndPauseSuspendsCeremony()
        {
            SaveManager.Instance.World.QuestFlags[
                CultivationContentIds.FoundationPityFlag] = true;
            SetProfile(RealmType.Foundation, 3, 12000f);
            AddItem(InventoryContentIds.GoldenCorePill);
            _cultivation.SetRandomValueProvider(() => 0f);

            Assert.That(_cultivation.CanBreakthrough(), Is.True);
            Assert.That(_cultivation.GetBreakthroughSuccessRate(), Is.EqualTo(0.65f));
            Assert.That(_cultivation.TryBreakthrough(), Is.True);
            Assert.That(GetPityFlag(), Is.True);
            Assert.That(
                GameManager.Instance.TrySetState(GameState.Paused),
                Is.True);
            _cultivation.TickBreakthrough(2f);
            Assert.That(_cultivation.CeremonyBeat, Is.EqualTo(1));
            Assert.That(
                GameManager.Instance.TrySetState(GameState.Playing),
                Is.True);

            TickAndRefresh(3f);
            Assert.That(_cultivation.Realm, Is.EqualTo(RealmType.GoldenCore));
            Assert.That(
                _inventory.CountItem(InventoryContentIds.GoldenCorePill),
                Is.Zero);
            Assert.That(GetPityFlag(), Is.True);
            Assert.That(_cultivation.InterruptBreakthrough(), Is.True);
            Assert.That(_cultivation.CanBreakthrough(), Is.False);
        }

        [Test]
        public void TeleportRequestQueuesUntilBreakthroughCeremonyEnds()
        {
            SetProfile(RealmType.QiCondensation, 9, 3200f);
            AddItem(InventoryContentIds.FoundationPill);
            Assert.That(_cultivation.TryBreakthrough(), Is.True);

            SceneLoader loader = SceneLoader.Instance;
            Assert.That(
                loader.LoadMap(SceneLoader.DefaultMapId, "spawn_test"),
                Is.True,
                loader.LastError);
            Assert.That(loader.HasQueuedMapLoad, Is.True);
            Assert.That(loader.QueuedMapId, Is.EqualTo(SceneLoader.DefaultMapId));
            Assert.That(loader.QueuedSpawnId, Is.EqualTo("spawn_test"));
            Assert.That(loader.IsLoading, Is.False);
            Assert.That(loader.CancelQueuedMapLoad(), Is.True);
            Assert.That(_cultivation.InterruptBreakthrough(), Is.True);
        }

        private void TickAndRefresh(float deltaTime)
        {
            _cultivation.TickBreakthrough(deltaTime);
            _ceremony.Refresh(_cultivation.CeremonyBeat);
        }

        private static void SetProfile(
            RealmType realm,
            int subStage,
            float cultivationXp)
        {
            SaveManager.Instance.Profile.Realm = (int)realm;
            SaveManager.Instance.Profile.SubStage = subStage;
            SaveManager.Instance.Profile.CultivationXp = cultivationXp;
        }

        private void AddItem(string itemId)
        {
            Assert.That(
                _inventory.AddItem(itemId, 1, AcquireSource.Cheat),
                Is.True,
                itemId);
        }

        private static bool GetPityFlag()
        {
            return SaveManager.Instance.World.QuestFlags.TryGetValue(
                    CultivationContentIds.FoundationPityFlag,
                    out bool enabled)
                && enabled;
        }

        private static void EnterPlayingState()
        {
            GameManager gameManager = GameManager.Instance;
            Assert.That(gameManager, Is.Not.Null);
            if (gameManager.State == GameState.Boot)
            {
                Assert.That(gameManager.TrySetState(GameState.MainMenu), Is.True);
            }

            if (gameManager.State == GameState.MainMenu)
            {
                Assert.That(gameManager.TrySetState(GameState.Loading), Is.True);
            }

            if (gameManager.State == GameState.Loading)
            {
                Assert.That(gameManager.TrySetState(GameState.Playing), Is.True);
            }
        }

        private static void DestroyRuntimeObjects()
        {
            DestroyAll<Wendao.Systems.Loot.WorldItemPickup>();
            DestroyAll<EnemyBrain>();
            DestroyAll<EnemySpawner>();
            DestroyAll<PlayerController>();
            DestroyAll<Wendao.Entities.NPC.NPCController>();
            DestroyAll<TrainingDummy>();
            DestroyAll<ThirdPersonCamera>();
            DestroyAll<SpiritRootSelectionView>();
            DestroyAll<CultivationHudView>();
            DestroyAll<BreakthroughCeremonyView>();
            DestroyAll<GameToastView>();
            DestroyAll<Wendao.UI.Inventory.InventoryPanelView>();
            DestroyAll<Wendao.UI.Skill.SkillQuickbarView>();
            DestroyAll<Wendao.UI.NPC.DialogueView>();
            DestroyAll<Wendao.UI.Quest.QuestTrackerView>();
            DestroyAll<Wendao.UI.Combat.DamageFloatingTextView>();
            DestroyAll<Wendao.UI.Tutorial.TutorialToastView>();
            DestroyAll<TutorialManager>();
            DestroyAll<Wendao.Systems.Loot.LootSystem>();
            DestroyAll<SkillProjectile>();
            DestroyAll<SkillManager>();
            DestroyAll<Wendao.Systems.NPC.DialogueManager>();
            DestroyAll<Wendao.Systems.Quest.QuestManager>();
            DestroyAll<BodyRefinementManager>();
            DestroyAll<CultivationManager>();
            DestroyAll<SpiritRootSystem>();
            DestroyAll<EquipmentManager>();
            DestroyAll<ItemUseSystem>();
            DestroyAll<InventoryManager>();
            DestroyAll<Wendao.Systems.Crafting.AlchemySystem>();
            DestroyAll<Wendao.Systems.Shop.ShopSystem>();
            DestroyAll<Wendao.Systems.Crafting.GatheringSystem>();
            DestroyAll<Wendao.UI.Crafting.AlchemyPanelView>();
            DestroyAll<Wendao.UI.Shop.ShopPanelView>();
            DestroyAll<StatusEffectManager>();
            DestroyAll<CombatSystem>();
            DestroyAll<SceneLoader>();
            DestroyAll<GameManager>();
            DestroyAll<SaveManager>();
            DestroyAll<ConfigDatabase>();
        }

        private static void DestroyAll<T>() where T : Component
        {
            T[] instances = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            foreach (T instance in instances)
            {
                if (instance != null)
                {
                    Object.Destroy(instance.gameObject);
                }
            }
        }
    }
}
