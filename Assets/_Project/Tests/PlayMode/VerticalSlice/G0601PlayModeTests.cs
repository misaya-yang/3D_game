using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wendao.CameraSystem;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.Enemy;
using Wendao.Entities.Player;
using Wendao.Systems.Combat;
using Wendao.Systems.Crafting;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Equipment;
using Wendao.Systems.Input;
using Wendao.Systems.Inventory;
using Wendao.Systems.Loot;
using Wendao.Systems.NPC;
using Wendao.Systems.Quest;
using Wendao.Systems.Shop;
using Wendao.Systems.Skill;
using Wendao.Systems.Tutorial;
using Wendao.Systems.World;
using Wendao.UI.Combat;
using Wendao.UI.Common;
using Wendao.UI.Crafting;
using Wendao.UI.Cultivation;
using Wendao.UI.Inventory;
using Wendao.UI.NPC;
using Wendao.UI.Quest;
using Wendao.UI.SceneFlow;
using Wendao.UI.Shop;
using Wendao.UI.Skill;
using Wendao.UI.Tutorial;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0601PlayModeTests
    {
        private string _storageRoot;
        private IUIManager _manager;
        private IPlayerInputSource _input;
        private IInventoryService _inventory;
        private IDailyQuestService _dailies;
        private InventoryPanelView _inventoryPanel;
        private SkillPanelView _skillPanel;
        private QuestPanelView _questPanel;
        private MapPanelView _mapPanel;
        private PausePanelView _pausePanel;
        private AlchemyPanelView _alchemyPanel;
        private CombatStatusHudView _statusHud;

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
                "WendaoG0601Tests_" + Guid.NewGuid().ToString("N"));
            SaveManager save = SaveManager.Instance;
            save.ConfigureStorageRoot(_storageRoot);
            save.Profile.DisplayName = "青玄";
            save.Profile.SpiritRoot = SpiritRootType.Fire.ToString();
            Assert.That(save.SaveGame(0), Is.True, save.LastError);

            AsyncOperation load = SceneManager.LoadSceneAsync(
                SceneLoader.DefaultMapSceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;

            EnterPlayingState();
            PlayerRuntimeBootstrap.EnsureForScene(SceneManager.GetActiveScene());
            SceneUiBootstrap.EnsureForScene(SceneManager.GetActiveScene());
            yield return null;

            _manager = ServiceLocator.Get<IUIManager>();
            _input = ServiceLocator.Get<IPlayerInputSource>();
            _inventory = ServiceLocator.Get<IInventoryService>();
            _dailies = ServiceLocator.Get<IDailyQuestService>();
            _inventoryPanel = Object.FindAnyObjectByType<InventoryPanelView>();
            _skillPanel = Object.FindAnyObjectByType<SkillPanelView>();
            _questPanel = Object.FindAnyObjectByType<QuestPanelView>();
            _mapPanel = Object.FindAnyObjectByType<MapPanelView>();
            _pausePanel = Object.FindAnyObjectByType<PausePanelView>();
            _alchemyPanel = Object.FindAnyObjectByType<AlchemyPanelView>();
            _statusHud = Object.FindAnyObjectByType<CombatStatusHudView>();

            Assert.That(_manager, Is.Not.Null);
            Assert.That(_input, Is.Not.Null);
            Assert.That(_inventoryPanel, Is.Not.Null);
            Assert.That(_skillPanel, Is.Not.Null);
            Assert.That(_questPanel, Is.Not.Null);
            Assert.That(_mapPanel, Is.Not.Null);
            Assert.That(_pausePanel, Is.Not.Null);
            Assert.That(_alchemyPanel, Is.Not.Null);
            Assert.That(_statusHud, Is.Not.Null);
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
        public void BootstrapCreatesMvpNavigationAndInputActionsHaveBindings()
        {
            Assert.That(Object.FindAnyObjectByType<UIManager>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<CharacterPanelView>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<ShopPanelView>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<QuestTrackerView>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<SkillQuickbarView>(), Is.Not.Null);

            InputActionAsset asset = Resources.Load<InputActionAsset>(
                PlayerInputReader.DefaultActionAssetResourcePath);
            InputActionMap player = asset.FindActionMap(
                PlayerInputReader.PlayerActionMapName,
                true);
            AssertBinding(player, "OpenInventory", "<Keyboard>/b");
            AssertBinding(player, "OpenCharacter", "<Keyboard>/c");
            AssertBinding(player, "OpenSkill", "<Keyboard>/k");
            AssertBinding(player, "OpenQuest", "<Keyboard>/j");
            AssertBinding(player, "OpenMap", "<Keyboard>/m");
            AssertBinding(player, "Pause", "<Keyboard>/escape");
        }

        [Test]
        public void FullscreenPanelsAreMutuallyExclusiveAndCancelClosesBeforePause()
        {
            Assert.That(_input.IsEnabled, Is.True);
            _manager.ShowPanel(UiPanelIds.Inventory);
            Assert.That(_inventoryPanel.IsOpen, Is.True);
            Assert.That(_input.IsEnabled, Is.False);

            _manager.ShowPanel(UiPanelIds.Skill);
            Assert.That(_inventoryPanel.IsOpen, Is.False);
            Assert.That(_skillPanel.IsOpen, Is.True);
            Assert.That(_input.IsEnabled, Is.False);

            _manager.ShowPanel(UiPanelIds.Quest);
            Assert.That(_skillPanel.IsOpen, Is.False);
            Assert.That(_questPanel.IsOpen, Is.True);

            _manager.HandleCancel();
            Assert.That(_questPanel.IsOpen, Is.False);
            Assert.That(_pausePanel.IsOpen, Is.False);
            Assert.That(_input.IsEnabled, Is.True);
        }

        [Test]
        public void CancelOpensPauseConfirmHasPriorityAndSecondCancelResumes()
        {
            _manager.HandleCancel();
            Assert.That(_pausePanel.IsOpen, Is.True);
            Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Paused));
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(_input.IsEnabled, Is.False);

            _manager.ShowConfirm("测试确认", () => { });
            Assert.That(Object.FindAnyObjectByType<UIManager>().IsConfirmOpen, Is.True);
            _manager.HandleCancel();
            Assert.That(Object.FindAnyObjectByType<UIManager>().IsConfirmOpen, Is.False);
            Assert.That(_pausePanel.IsOpen, Is.True);

            _manager.HandleCancel();
            Assert.That(_pausePanel.IsOpen, Is.False);
            Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Playing));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(_input.IsEnabled, Is.True);
        }

        [Test]
        public void QuestDailyMapAndStatusHudRefreshFromRuntimeServicesAndEvents()
        {
            _manager.ShowPanel(UiPanelIds.Quest);
            _questPanel.SelectQuest(QuestContentIds.DailyHunt, true);
            StringAssert.Contains("0/15", _questPanel.ProgressText);

            for (int index = 0; index < 15; index++)
            {
                EventBus.Publish(
                    CombatEvents.EnemyKilled,
                    new EnemyDeathInfo
                    {
                        EnemyId = QuestContentIds.GreyWolfEnemy,
                        Rank = EnemyRank.Normal
                    });
            }

            Assert.That(_dailies.IsComplete(QuestContentIds.DailyHunt), Is.True);
            StringAssert.Contains(QuestPanelView.DailyReadyDefaultValue, _questPanel.ProgressText);
            Assert.That(_questPanel.CanClaimDaily, Is.True);
            Assert.That(_questPanel.TryClaimSelectedDaily(), Is.True);
            StringAssert.Contains(QuestPanelView.DailyClaimedDefaultValue, _questPanel.ProgressText);

            int before = _inventory.SpiritStones;
            Assert.That(_inventory.AddSpiritStones(7), Is.True);
            StringAssert.Contains((before + 7).ToString(), _statusHud.CurrencyText);
            StringAssert.Contains("青玄", _statusHud.PlayerNameText);

            _manager.ShowPanel(UiPanelIds.Map);
            Assert.That(_questPanel.IsOpen, Is.False);
            Assert.That(_mapPanel.IsOpen, Is.True);
            Assert.That(_mapPanel.UnlockedButtonCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(
                _mapPanel.SelectedTeleportId,
                Is.EqualTo(MapContentIds.QingshiTownTeleport));
        }

        [UnityTest]
        public IEnumerator InteractionPanelOpeningWinsAndDialogueClosesAllPanels()
        {
            _manager.ShowPanel(UiPanelIds.Inventory);
            _alchemyPanel.SetOpen(true);
            yield return null;

            Assert.That(_inventoryPanel.IsOpen, Is.False);
            Assert.That(_alchemyPanel.IsOpen, Is.True);
            Assert.That(_input.IsEnabled, Is.False);

            Assert.That(
                GameManager.Instance.TrySetState(GameState.Dialogue),
                Is.True);
            EventBus.Publish(
                DialogueEvents.Started,
                new DialogueInfo
                {
                    NpcId = QuestContentIds.YaoLaoNpc,
                    DialogueId = "dialogue_test"
                });
            Assert.That(_alchemyPanel.IsOpen, Is.False);
            Assert.That(_manager.HasOpenPanel, Is.False);
            Assert.That(_input.IsEnabled, Is.False);
        }

        private static void AssertBinding(
            InputActionMap map,
            string actionName,
            string path)
        {
            InputAction action = map.FindAction(actionName, true);
            Assert.That(
                action.bindings,
                Has.Some.Matches<InputBinding>(binding => binding.path == path));
        }

        private static void EnterPlayingState()
        {
            GameManager gameManager = GameManager.Instance;
            Assert.That(gameManager, Is.Not.Null);
            if (gameManager.State == GameState.Boot)
            {
                Assert.That(gameManager.TrySetState(GameState.MainMenu), Is.True);
            }

            if (gameManager.State != GameState.Playing)
            {
                Assert.That(gameManager.TrySetState(GameState.Loading), Is.True);
                Assert.That(gameManager.TrySetState(GameState.Playing), Is.True);
            }

            gameManager.SetCombatFlag(false);
        }

        private static void DestroyRuntimeObjects()
        {
            DestroyAll<UIManager>();
            DestroyAll<CombatStatusHudView>();
            DestroyAll<InventoryPanelView>();
            DestroyAll<CharacterPanelView>();
            DestroyAll<SkillPanelView>();
            DestroyAll<SkillQuickbarView>();
            DestroyAll<QuestPanelView>();
            DestroyAll<MapPanelView>();
            DestroyAll<PausePanelView>();
            DestroyAll<AlchemyPanelView>();
            DestroyAll<ShopPanelView>();
            DestroyAll<QuestTrackerView>();
            DestroyAll<DialogueView>();
            DestroyAll<CultivationHudView>();
            DestroyAll<TrainingDummy>();
            DestroyAll<EnemyBrain>();
            DestroyAll<EnemySpawner>();
            DestroyAll<ThirdPersonCamera>();
            DestroyAll<PlayerController>();
            DestroyAll<DailyQuestManager>();
            DestroyAll<QuestManager>();
            DestroyAll<DialogueManager>();
            DestroyAll<TutorialManager>();
            DestroyAll<SkillManager>();
            DestroyAll<RefineSystem>();
            DestroyAll<EquipmentManager>();
            DestroyAll<ItemUseSystem>();
            DestroyAll<InventoryManager>();
            DestroyAll<AlchemySystem>();
            DestroyAll<GatheringSystem>();
            DestroyAll<ShopSystem>();
            DestroyAll<LootSystem>();
            DestroyAll<BodyRefinementManager>();
            DestroyAll<CultivationManager>();
            DestroyAll<SpiritRootSystem>();
            DestroyAll<MapTravelSystem>();
            DestroyAll<BlackwindDungeonSystem>();
            DestroyAll<SafeZoneSystem>();
            DestroyAll<StatusEffectManager>();
            DestroyAll<CombatFeelController>();
            DestroyAll<CombatSystem>();
            DestroyAll<SceneLoader>();
            DestroyAll<GameManager>();
            DestroyAll<SaveManager>();
            DestroyAll<ConfigDatabase>();
        }

        private static void DestroyAll<T>() where T : Component
        {
            T[] instances = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            for (int index = 0; index < instances.Length; index++)
            {
                if (instances[index] != null)
                {
                    Object.Destroy(instances[index].gameObject);
                }
            }
        }
    }
}
