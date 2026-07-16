using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wendao.CameraSystem;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.Enemy;
using Wendao.Entities.Player;
using Wendao.Systems.Combat;
using Wendao.Systems.Crafting;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Equipment;
using Wendao.Systems.Feedback;
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
    public sealed class G0901PlayModeTests
    {
        private string _storageRoot;
        private IUIManager _manager;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            EventBus.Clear();
            DestroyRuntimeObjects();
            yield return null;
            ServiceLocator.Clear();

            _storageRoot = Path.Combine(
                Path.GetTempPath(),
                "WendaoG0901Tests_" + Guid.NewGuid().ToString("N"));
            GameSettingsStore.ConfigureStorageRoot(_storageRoot);
            Assert.That(
                GameSettingsStore.TrySave(
                    new GameSettingsData
                    {
                        MasterVolume = 0.8f,
                        BgmVolume = 0.6f,
                        SfxVolume = 0.4f,
                        Fullscreen = false
                    },
                    out string settingsError),
                Is.True,
                settingsError);

            SceneFlowBootstrap.Install();
            SceneUiBootstrap.Install();
            PlayerRuntimeBootstrap.Install();

            SaveManager save = SaveManager.Instance;
            save.ConfigureStorageRoot(_storageRoot);
            save.Profile.SpiritRoot = SpiritRootType.Wood.ToString();
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
            Assert.That(_manager, Is.Not.Null);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            EventBus.Clear();
            DestroyRuntimeObjects();
            yield return null;
            ServiceLocator.Clear();
            GameSettingsStore.ConfigureStorageRoot(string.Empty);

            if (!string.IsNullOrEmpty(_storageRoot)
                && Directory.Exists(_storageRoot))
            {
                Directory.Delete(_storageRoot, true);
            }
        }

        [Test]
        public void EventSystemAndVisibleMenuBarProvideKeyboardAndMouseNavigation()
        {
            Assert.That(EventSystem.current, Is.Not.Null);
            InputSystemUIInputModule module =
                EventSystem.current.GetComponent<InputSystemUIInputModule>();
            Assert.That(module, Is.Not.Null);
            Assert.That(module.actionsAsset, Is.Not.Null);
            Assert.That(module.point.action.enabled, Is.True);
            Assert.That(module.leftClick.action.enabled, Is.True);
            Assert.That(module.submit.action.enabled, Is.True);

            GameplayMenuBarView menu =
                Object.FindAnyObjectByType<GameplayMenuBarView>();
            Assert.That(menu, Is.Not.Null);
            AssertViewRendered(menu, true);
            Assert.That(
                menu.GetComponentsInChildren<Button>(true),
                Has.Length.EqualTo(6));

            InventoryPanelView inventory =
                Object.FindAnyObjectByType<InventoryPanelView>();
            _manager.ShowPanel(UiPanelIds.Inventory);
            Assert.That(inventory.IsOpen, Is.True);
            AssertViewRendered(menu, false);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.Null);

            _manager.HidePanel(UiPanelIds.Inventory);
            AssertViewRendered(menu, true);
        }

        [Test]
        public void FullscreenPanelsHideHudAndQuickbarOnlyRemainsForSkillEditing()
        {
            CombatStatusHudView status =
                Object.FindAnyObjectByType<CombatStatusHudView>();
            CultivationHudView cultivation =
                Object.FindAnyObjectByType<CultivationHudView>();
            SkillQuickbarView quickbar =
                Object.FindAnyObjectByType<SkillQuickbarView>();

            AssertViewRendered(status, true);
            AssertViewRendered(cultivation, true);
            AssertViewRendered(quickbar, true);

            _manager.ShowPanel(UiPanelIds.Inventory);
            AssertViewRendered(status, false);
            AssertViewRendered(cultivation, false);
            AssertViewRendered(quickbar, false);

            _manager.ShowPanel(UiPanelIds.Skill);
            AssertViewRendered(status, false);
            AssertViewRendered(cultivation, false);
            AssertViewRendered(quickbar, true);
        }

        [Test]
        public void SettingsPreviewCancelsSafelyAndApplyPersistsAtomically()
        {
            _manager.HandleCancel();
            PausePanelView pause = Object.FindAnyObjectByType<PausePanelView>();
            Assert.That(pause.IsOpen, Is.True);
            pause.ShowSettingsSummary();
            Assert.That(pause.IsSettingsOpen, Is.True);

            Slider[] sliders = pause.GetComponentsInChildren<Slider>(true);
            Assert.That(sliders, Has.Length.EqualTo(3));
            FindSlider(sliders, "MasterVolumeSlider").value = 0.25f;
            FindSlider(sliders, "BgmVolumeSlider").value = 0.35f;
            FindSlider(sliders, "SfxVolumeSlider").value = 0.45f;
            Assert.That(AudioManager.Instance.MasterVolume, Is.EqualTo(0.25f));

            _manager.HandleCancel();
            Assert.That(pause.IsOpen, Is.True);
            Assert.That(pause.IsSettingsOpen, Is.False);
            Assert.That(AudioManager.Instance.MasterVolume, Is.EqualTo(0.8f));
            Assert.That(AudioManager.Instance.BgmVolume, Is.EqualTo(0.6f));
            Assert.That(AudioManager.Instance.SfxVolume, Is.EqualTo(0.4f));

            pause.ShowSettingsSummary();
            sliders = pause.GetComponentsInChildren<Slider>(true);
            FindSlider(sliders, "MasterVolumeSlider").value = 0.3f;
            FindSlider(sliders, "BgmVolumeSlider").value = 0.5f;
            FindSlider(sliders, "SfxVolumeSlider").value = 0.7f;
            pause.ToggleFullscreen();
            Assert.That(pause.ApplySettings(), Is.True);
            Assert.That(pause.IsSettingsOpen, Is.False);
            Assert.That(File.Exists(GameSettingsStore.SettingsPath), Is.True);

            GameSettingsData saved = GameSettingsStore.LoadOrDefault();
            Assert.That(saved.MasterVolume, Is.EqualTo(0.3f));
            Assert.That(saved.BgmVolume, Is.EqualTo(0.5f));
            Assert.That(saved.SfxVolume, Is.EqualTo(0.7f));
            Assert.That(saved.Fullscreen, Is.True);
        }

        [Test]
        public void WorldAreaMarkersUseTransientToastWithoutVisibleWorldPlates()
        {
            WorldAreaMarker[] markers =
                Object.FindObjectsByType<WorldAreaMarker>(FindObjectsSortMode.None);
            Assert.That(markers.Length, Is.GreaterThan(0));
            foreach (WorldAreaMarker marker in markers)
            {
                Transform plate = marker.transform.Find(WorldAreaMarker.PlateName);
                Transform label = marker.transform.Find(WorldAreaMarker.LabelName);
                Assert.That(plate, Is.Not.Null);
                Assert.That(plate.GetComponent<Renderer>().enabled, Is.False);
                Assert.That(label, Is.Not.Null);
                Assert.That(label.gameObject.activeSelf, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator NonGameplayStatesHideHudChromeAndRestoreItSafely()
        {
            CombatStatusHudView status =
                Object.FindAnyObjectByType<CombatStatusHudView>();
            CultivationHudView cultivation =
                Object.FindAnyObjectByType<CultivationHudView>();
            SkillQuickbarView quickbar =
                Object.FindAnyObjectByType<SkillQuickbarView>();
            GameplayMenuBarView menu =
                Object.FindAnyObjectByType<GameplayMenuBarView>();
            GameManager gameManager = GameManager.Instance;

            Assert.That(gameManager.TrySetState(GameState.Dialogue), Is.True);
            yield return null;
            AssertViewRendered(status, false);
            AssertViewRendered(cultivation, false);
            AssertViewRendered(quickbar, false);
            AssertViewRendered(menu, false);

            Assert.That(gameManager.TrySetState(GameState.Playing), Is.True);
            yield return null;
            AssertViewRendered(status, true);
            AssertViewRendered(cultivation, true);
            AssertViewRendered(quickbar, true);
            AssertViewRendered(menu, true);

            Assert.That(gameManager.TrySetState(GameState.Dead), Is.True);
            yield return null;
            AssertViewRendered(status, false);
            AssertViewRendered(cultivation, false);
            AssertViewRendered(quickbar, false);
            AssertViewRendered(menu, false);
            Assert.That(
                Object.FindAnyObjectByType<DeathView>().IsOpen,
                Is.True);
        }

        [UnityTest]
        public IEnumerator DialogueShutdownToleratesDestroyedPlayerInput()
        {
            DialogueManager dialogue =
                Object.FindAnyObjectByType<DialogueManager>();
            PlayerInputReader input =
                Object.FindAnyObjectByType<PlayerInputReader>();
            Assert.That(dialogue, Is.Not.Null);
            Assert.That(input, Is.Not.Null);
            Assert.That(
                dialogue.TryStartDialogue(
                    QuestContentIds.HuntStartDialogue,
                    QuestContentIds.YaoLaoNpc),
                Is.True);

            Object.Destroy(input);
            yield return null;
            Object.Destroy(dialogue.gameObject);
            yield return null;

            Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Playing));
        }

        private static Slider FindSlider(Slider[] sliders, string name)
        {
            for (int index = 0; index < sliders.Length; index++)
            {
                if (sliders[index].name == name)
                {
                    return sliders[index];
                }
            }

            Assert.Fail("Missing slider: " + name);
            return null;
        }

        private static void AssertViewRendered(Behaviour view, bool expected)
        {
            Assert.That(view, Is.Not.Null);
            Canvas canvas = view.GetComponentInChildren<Canvas>(true);
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.enabled, Is.EqualTo(expected));
            GraphicRaycaster raycaster =
                view.GetComponentInChildren<GraphicRaycaster>(true);
            Assert.That(raycaster, Is.Not.Null);
            Assert.That(raycaster.enabled, Is.EqualTo(expected));
        }

        private static void EnterPlayingState()
        {
            GameManager gameManager = GameManager.Instance;
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
            DestroyAll<UIManager>();
            DestroyAll<GameplayMenuBarView>();
            DestroyAll<GameSettingsRuntime>();
            DestroyAll<AudioStateController>();
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
            DestroyAll<GameToastView>();
            DestroyAll<DeathView>();
            DestroyAll<DamageFloatingTextView>();
            DestroyAll<TutorialToastView>();
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
            DestroyAll<AudioManager>();
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
