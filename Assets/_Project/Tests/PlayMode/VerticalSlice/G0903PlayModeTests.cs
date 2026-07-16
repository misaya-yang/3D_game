using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wendao.CameraSystem;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.Enemy;
using Wendao.Entities.NPC;
using Wendao.Entities.Player;
using Wendao.Entities.Visuals;
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
using Wendao.UI.Common;
using Wendao.UI.Crafting;
using Wendao.UI.Quest;
using Wendao.UI.SceneFlow;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0903PlayModeTests
    {
        private string _storageRoot;
        private IQuestService _quests;
        private IInventoryService _inventory;
        private IPlayerInputSource _input;
        private QuestTrackerView _tracker;
        private QuestWorldMarkerView _marker;
        private AlchemyPanelView _alchemyPanel;
        private PlayerController _player;
        private Keyboard _testKeyboard;
        private Mouse _testMouse;
        private Gamepad _testGamepad;
        private bool _ownsTestKeyboard;
        private bool _ownsTestMouse;
        private bool _ownsTestGamepad;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            EventBus.Clear();
            DestroyRuntimeObjects();
            yield return null;
            ServiceLocator.Clear();

            _testKeyboard = Keyboard.current;
            _ownsTestKeyboard = _testKeyboard == null;
            if (_testKeyboard == null)
            {
                _testKeyboard = InputSystem.AddDevice<Keyboard>();
            }

            _testMouse = Mouse.current;
            _ownsTestMouse = _testMouse == null;
            if (_testMouse == null)
            {
                _testMouse = InputSystem.AddDevice<Mouse>();
            }

            _testGamepad = Gamepad.current;
            _ownsTestGamepad = _testGamepad == null;
            if (_testGamepad == null)
            {
                _testGamepad = InputSystem.AddDevice<Gamepad>();
            }
            InputSystem.Update();

            SceneFlowBootstrap.Install();
            SceneUiBootstrap.Install();
            PlayerRuntimeBootstrap.Install();

            _storageRoot = Path.Combine(
                Path.GetTempPath(),
                "WendaoG0903Tests_" + Guid.NewGuid().ToString("N"));
            SaveManager save = SaveManager.Instance;
            save.ConfigureStorageRoot(_storageRoot);
            save.Profile.SpiritRoot = SpiritRootType.Wood.ToString();
            save.World.TutorialsCompleted.Add(TutorialManager.MoveTutorialId);
            save.World.TutorialsCompleted.Add(TutorialManager.CombatTutorialId);
            Assert.That(save.SaveGame(0), Is.True, save.LastError);

            AsyncOperation load = SceneManager.LoadSceneAsync(
                SceneLoader.DefaultMapSceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;

            EnterPlayingState();
            PlayerRuntimeBootstrap.EnsureForScene(
                SceneManager.GetActiveScene());
            SceneUiBootstrap.EnsureForScene(SceneManager.GetActiveScene());
            yield return null;

            _quests = ServiceLocator.Get<IQuestService>();
            _inventory = ServiceLocator.Get<IInventoryService>();
            _input = ServiceLocator.Get<IPlayerInputSource>();
            _tracker = Object.FindAnyObjectByType<QuestTrackerView>();
            _marker = Object.FindAnyObjectByType<QuestWorldMarkerView>();
            _alchemyPanel = Object.FindAnyObjectByType<AlchemyPanelView>();
            _player = Object.FindAnyObjectByType<PlayerController>();

            Assert.That(_quests, Is.Not.Null);
            Assert.That(_inventory, Is.Not.Null);
            Assert.That(_input, Is.Not.Null);
            Assert.That(_tracker, Is.Not.Null);
            Assert.That(_marker, Is.Not.Null);
            Assert.That(_alchemyPanel, Is.Not.Null);
            Assert.That(_player, Is.Not.Null);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            EventBus.Clear();
            DestroyRuntimeObjects();
            yield return null;
            ServiceLocator.Clear();
            if (_ownsTestGamepad && _testGamepad != null)
            {
                InputSystem.RemoveDevice(_testGamepad);
            }
            if (_ownsTestMouse && _testMouse != null)
            {
                InputSystem.RemoveDevice(_testMouse);
            }
            if (_ownsTestKeyboard && _testKeyboard != null)
            {
                InputSystem.RemoveDevice(_testKeyboard);
            }
            _testKeyboard = null;
            _testMouse = null;
            _testGamepad = null;
            _ownsTestKeyboard = false;
            _ownsTestMouse = false;
            _ownsTestGamepad = false;

            if (!string.IsNullOrEmpty(_storageRoot)
                && Directory.Exists(_storageRoot))
            {
                Directory.Delete(_storageRoot, true);
            }
        }

        [Test]
        public void FreshSaveShowsAvailableMainQuestAndYaoLaoMarker()
        {
            _tracker.Refresh();
            _marker.RefreshNow();

            Assert.That(_tracker.IsVisible, Is.True);
            Assert.That(
                _tracker.CurrentQuestId,
                Is.EqualTo(QuestContentIds.MainRootAwakening));
            Assert.That(
                _tracker.CurrentGuidanceKind,
                Is.EqualTo(MainQuestGuidanceKind.Accept));
            Assert.That(_tracker.ObjectiveText, Does.Contain("药老"));
            Assert.That(_marker.IsVisible, Is.True);
            Assert.That(
                _marker.TargetId,
                Is.EqualTo(QuestContentIds.YaoLaoNpc));
            Assert.That(
                _marker.TargetTransform.GetComponent<NPCController>()?.NpcId,
                Is.EqualTo(QuestContentIds.YaoLaoNpc));
        }

        [Test]
        public void TurnInAndNextAcceptWindowsKeepMainGuidanceVisible()
        {
            Assert.That(
                _quests.Accept(QuestContentIds.MainRootAwakening),
                Is.True);
            _quests.NotifyTalk(QuestContentIds.YaoLaoNpc);
            Assert.That(
                _quests.GetStatus(QuestContentIds.MainRootAwakening),
                Is.EqualTo(QuestStatus.Completed));

            _tracker.Refresh();
            Assert.That(
                _tracker.CurrentGuidanceKind,
                Is.EqualTo(MainQuestGuidanceKind.TurnIn));
            Assert.That(_tracker.ObjectiveText, Does.Contain("药老"));
            Assert.That(_tracker.IsVisible, Is.True);

            Assert.That(
                _quests.TurnIn(QuestContentIds.MainRootAwakening),
                Is.True);
            _tracker.Refresh();
            Assert.That(
                _tracker.CurrentQuestId,
                Is.EqualTo(QuestContentIds.MainHuntWolves));
            Assert.That(
                _tracker.CurrentGuidanceKind,
                Is.EqualTo(MainQuestGuidanceKind.Accept));
            Assert.That(_tracker.IsVisible, Is.True);
        }

        [Test]
        public void FurnaceInteractionOpensAlchemyAndLocksGameplayInput()
        {
            AlchemyFurnaceInteractable furnace =
                Object.FindAnyObjectByType<AlchemyFurnaceInteractable>();
            Assert.That(furnace, Is.Not.Null);
            Assert.That(
                furnace.FurnaceId,
                Is.EqualTo(AlchemyFurnaceRuntimeBootstrap.QingshiFurnaceId));

            _player.TeleportTo(
                furnace.transform.position + Vector3.forward,
                Quaternion.identity);
            Assert.That(furnace.TryInteract(), Is.True);

            Assert.That(_alchemyPanel.IsOpen, Is.True);
            Assert.That(_input.IsEnabled, Is.False);
            Assert.That(
                ServiceLocator.Get<IUIManager>()
                    .IsPanelOpen(UiPanelIds.Alchemy),
                Is.True);

            ServiceLocator.Get<IUIManager>().HidePanel(UiPanelIds.Alchemy);
            Assert.That(_input.IsEnabled, Is.True);
        }

        [Test]
        public void MainQuestFourCompletesThroughRuntimeFurnace()
        {
            CompleteQuest(
                QuestContentIds.MainRootAwakening,
                () => _quests.NotifyTalk(QuestContentIds.YaoLaoNpc));
            CompleteQuest(
                QuestContentIds.MainHuntWolves,
                () => Kill(QuestContentIds.GreyWolfEnemy, 3));

            Assert.That(
                _quests.Accept(QuestContentIds.MainGatherQingxin),
                Is.True);
            Assert.That(
                _inventory.AddItem(
                    InventoryContentIds.QingxinGrass,
                    5,
                    AcquireSource.Gather),
                Is.True);
            _quests.NotifyCollect(
                InventoryContentIds.QingxinGrass,
                _inventory.CountItem(InventoryContentIds.QingxinGrass));
            Kill(QuestContentIds.EliteWolfEnemy, 1);
            Assert.That(
                _quests.TurnIn(QuestContentIds.MainGatherQingxin),
                Is.True);

            Assert.That(
                _quests.Accept(QuestContentIds.MainCraftManaPotion),
                Is.True);
            Assert.That(
                _inventory.AddItem(
                    InventoryContentIds.SpiritDust,
                    2,
                    AcquireSource.Gather),
                Is.True);

            AlchemySystem alchemy =
                Object.FindAnyObjectByType<AlchemySystem>();
            AlchemyFurnaceInteractable furnace =
                Object.FindAnyObjectByType<AlchemyFurnaceInteractable>();
            Assert.That(alchemy, Is.Not.Null);
            Assert.That(furnace, Is.Not.Null);
            alchemy.SetRandomValueProvider(() => 0f);
            _player.TeleportTo(
                furnace.transform.position + Vector3.forward,
                Quaternion.identity);
            Assert.That(furnace.TryInteract(), Is.True);
            _alchemyPanel.SelectRecipe(AlchemyContentIds.ManaRecipe);
            Assert.That(_alchemyPanel.TryCraftSelected(), Is.True);
            Assert.That(
                _quests.GetStatus(QuestContentIds.MainCraftManaPotion),
                Is.EqualTo(QuestStatus.Completed));
            Assert.That(
                _inventory.CountItem(InventoryContentIds.ManaPotion01),
                Is.GreaterThanOrEqualTo(1));
        }

        [UnityTest]
        public IEnumerator KeyboardAndGamepadReachCoreActionsAndModalSuppressesGameplay()
        {
            Keyboard keyboard = _testKeyboard;
            Assert.That(keyboard, Is.Not.Null);

            Gamepad gamepad = _testGamepad;
            bool addedGamepadInTest = gamepad == null || !gamepad.added;
            if (addedGamepadInTest)
            {
                gamepad = InputSystem.AddDevice<Gamepad>();
            }
            InputSystem.Update();
            try
            {
                Assert.That(_input.IsEnabled, Is.True);
                PlayerInputReader reader = _input as PlayerInputReader;
                Assert.That(reader, Is.Not.Null);
                InputAction openInventory = GetPrivateAction(
                    reader,
                    "_openInventoryAction");
                InputAction pause = GetPrivateAction(
                    reader,
                    "_pauseAction");
                InputAction move = GetPrivateAction(
                    reader,
                    "_moveAction");
                InputAction lightAttack = GetPrivateAction(
                    reader,
                    "_lightAttackAction");
                InputAction interact = GetPrivateAction(
                    reader,
                    "_interactAction");
                Assert.That(openInventory, Is.Not.Null);
                Assert.That(pause, Is.Not.Null);
                Assert.That(move, Is.Not.Null);
                Assert.That(lightAttack, Is.Not.Null);
                Assert.That(interact, Is.Not.Null);
                Assert.That(openInventory.enabled, Is.True);
                openInventory.actionMap.Disable();
                openInventory.actionMap.Enable();
                Assert.That(
                    openInventory.controls,
                    Has.Some.Matches<InputControl>(
                        control => ReferenceEquals(control.device, keyboard)));
                Assert.That(
                    openInventory.controls,
                    Has.Some.Matches<InputControl>(
                        control => ReferenceEquals(control.device, gamepad)));
                Assert.That(
                    pause.controls,
                    Has.Some.Matches<InputControl>(
                        control => ReferenceEquals(
                            control,
                            gamepad.startButton)));
                Assert.That(
                    move.controls,
                    Has.Some.Matches<InputControl>(
                        control => ReferenceEquals(
                            control,
                            keyboard.wKey)));
                Assert.That(
                    move.controls,
                    Has.Some.Matches<InputControl>(
                        control => ReferenceEquals(
                            control,
                            gamepad.leftStick)));
                Assert.That(
                    lightAttack.controls,
                    Has.Some.Matches<InputControl>(
                        control => ReferenceEquals(
                            control,
                            _testMouse.leftButton)));
                Assert.That(
                    lightAttack.controls,
                    Has.Some.Matches<InputControl>(
                        control => ReferenceEquals(
                            control,
                            gamepad.rightTrigger)));
                Assert.That(
                    interact.controls,
                    Has.Some.Matches<InputControl>(
                        control => ReferenceEquals(
                            control,
                            keyboard.eKey)));
                Assert.That(
                    interact.controls,
                    Has.Some.Matches<InputControl>(
                        control => ReferenceEquals(
                            control,
                            gamepad.buttonWest)));

                ServiceLocator.Get<IUIManager>()
                    .ShowPanel(UiPanelIds.Inventory);
                Assert.That(_input.IsEnabled, Is.False);

                Assert.That(_input.Move, Is.EqualTo(Vector2.zero));
                Assert.That(_input.JumpPressedThisFrame, Is.False);

                Assert.That(_input.Move, Is.EqualTo(Vector2.zero));
                Assert.That(_input.LightAttackPressedThisFrame, Is.False);
                Assert.That(_input.BlockHeld, Is.False);
                yield return null;
            }
            finally
            {
                if (addedGamepadInTest && gamepad != null && gamepad.added)
                {
                    InputSystem.RemoveDevice(gamepad);
                }
            }
        }

        private void CompleteQuest(string questId, Action progress)
        {
            Assert.That(_quests.Accept(questId), Is.True, questId);
            progress();
            Assert.That(
                _quests.GetStatus(questId),
                Is.EqualTo(QuestStatus.Completed),
                questId);
            Assert.That(_quests.TurnIn(questId), Is.True, questId);
        }

        private void Kill(string enemyId, int count)
        {
            for (int index = 0; index < count; index++)
            {
                _quests.NotifyKill(enemyId);
            }
        }

        private static void EnterPlayingState()
        {
            GameManager gameManager = GameManager.Instance;
            Assert.That(gameManager, Is.Not.Null);
            if (gameManager.State == GameState.Boot)
            {
                Assert.That(
                    gameManager.TrySetState(GameState.MainMenu),
                    Is.True);
            }

            if (gameManager.State != GameState.Playing)
            {
                Assert.That(
                    gameManager.TrySetState(GameState.Loading),
                    Is.True);
                Assert.That(
                    gameManager.TrySetState(GameState.Playing),
                    Is.True);
            }

            gameManager.SetCombatFlag(false);
        }

        private static void DestroyRuntimeObjects()
        {
            DestroyAll<QuestWorldMarkerView>();
            DestroyAll<QuestTrackerView>();
            DestroyAll<AlchemyPanelView>();
            DestroyAll<AlchemyFurnaceInteractable>();
            DestroyAll<UIManager>();
            DestroyAll<NPCController>();
            DestroyAll<EnemyBrain>();
            DestroyAll<EnemySpawner>();
            DestroyAll<PlayerController>();
            DestroyAll<ThirdPersonCamera>();
            DestroyAll<DialogueManager>();
            DestroyAll<QuestManager>();
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

        private static InputAction GetPrivateAction(
            PlayerInputReader reader,
            string fieldName)
        {
            return typeof(PlayerInputReader)
                .GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(reader) as InputAction;
        }

        private static void DestroyAll<T>() where T : Component
        {
            T[] instances = Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
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
