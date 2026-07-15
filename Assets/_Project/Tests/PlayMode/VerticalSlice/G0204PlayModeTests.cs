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
using Wendao.Systems.Cultivation;
using Wendao.Systems.Equipment;
using Wendao.Systems.Input;
using Wendao.Systems.Inventory;
using Wendao.Systems.World;
using Wendao.UI.Cultivation;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0204PlayModeTests
    {
        private string _storageRoot;
        private SaveManager _save;
        private BodyRefinementManager _body;
        private CultivationManager _cultivation;
        private CharacterPanelView _panel;
        private IInventoryService _inventory;
        private IPlayerInputSource _input;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            EventBus.Clear();
            DestroyRuntimeObjects();
            yield return null;
            ServiceLocator.Clear();

            SceneFlowBootstrap.Install();
            PlayerRuntimeBootstrap.Install();

            _storageRoot = Path.Combine(
                Path.GetTempPath(),
                "WendaoG0204Tests_" + Guid.NewGuid().ToString("N"));
            _save = SaveManager.Instance;
            _save.ConfigureStorageRoot(_storageRoot);
            Assert.That(_save.SaveGame(0), Is.True, _save.LastError);
            _save.Profile.SpiritRoot = SpiritRootType.Waste.ToString();

            AsyncOperation load = SceneManager.LoadSceneAsync(
                SceneLoader.DefaultMapSceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;

            EnterPlayingState();
            PlayerRuntimeBootstrap.EnsureForScene(SceneManager.GetActiveScene());
            yield return null;

            _body = Object.FindAnyObjectByType<BodyRefinementManager>();
            _cultivation = Object.FindAnyObjectByType<CultivationManager>();
            _panel = Object.FindAnyObjectByType<CharacterPanelView>();
            _inventory = ServiceLocator.Get<IInventoryService>();
            _input = ServiceLocator.Get<IPlayerInputSource>();

            Assert.That(_body, Is.Not.Null);
            Assert.That(_cultivation, Is.Not.Null);
            Assert.That(_panel, Is.Not.Null);
            Assert.That(_inventory, Is.Not.Null);
            Assert.That(_input, Is.Not.Null);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _cultivation?.InterruptBreakthrough();
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
        public void CharacterPanelHasCBindingAndDisplaysRealmRootBodyAndFinalStats()
        {
            _save.Profile.Realm = (int)RealmType.Foundation;
            _save.Profile.SubStage = 2;
            _save.Profile.CultivationXp = 325f;
            _save.Profile.BodyLevel = (int)BodyLevel.Copper;
            _save.Profile.BodyXp = 1500f;

            InputActionAsset asset = Resources.Load<InputActionAsset>(
                PlayerInputReader.DefaultActionAssetResourcePath);
            Assert.That(asset, Is.Not.Null);
            InputAction openCharacter = asset
                .FindActionMap(PlayerInputReader.PlayerActionMapName, true)
                .FindAction("OpenCharacter", true);
            Assert.That(
                openCharacter.bindings,
                Has.Some.Matches<InputBinding>(
                    binding => binding.path == "<Keyboard>/c"));

            _panel.SetOpen(true);

            Assert.That(_panel.IsOpen, Is.True);
            Assert.That(_input.IsEnabled, Is.False);
            Assert.That(_panel.RealmText, Does.Contain("筑基"));
            Assert.That(_panel.RealmText, Does.Contain("第2层"));
            Assert.That(_panel.SpiritRootText, Does.Contain("废脉"));
            Assert.That(_panel.BodyText, Does.Contain("铜皮铁骨"));
            Assert.That(_panel.BodyText, Does.Contain("1500"));
            Assert.That(_panel.StatsText, Does.Contain("气血"));
            Assert.That(_panel.StatsText, Does.Contain("攻击"));
            Assert.That(
                _panel.CurrentSpiritRootLocalizationKey,
                Is.EqualTo("root_name_waste"));
            Assert.That(
                _panel.CurrentBodyLocalizationKey,
                Is.EqualTo("body_name_copper"));

            _panel.SetOpen(false);
            Assert.That(_input.IsEnabled, Is.True);
        }

        [Test]
        public void BreakthroughButtonClosesPanelAndStartsReadyCeremony()
        {
            _save.Profile.Realm = (int)RealmType.QiCondensation;
            _save.Profile.SubStage = 9;
            _save.Profile.CultivationXp = 1000f;
            Assert.That(
                _inventory.AddItem(
                    InventoryContentIds.FoundationPill,
                    1,
                    AcquireSource.Cheat),
                Is.True);

            _panel.SetOpen(true);
            _panel.Refresh();
            Assert.That(_panel.CanAttemptBreakthrough, Is.True);
            Assert.That(_panel.BreakthroughStatusText, Does.Contain("成功率"));

            Assert.That(_panel.TryBreakthrough(), Is.True);
            Assert.That(_panel.IsOpen, Is.False);
            Assert.That(_cultivation.IsBreakthroughActive, Is.True);
            Assert.That(_input.IsEnabled, Is.False);
        }

        [Test]
        public void BodyLevelAndXpRoundTripThroughProfileWithoutShadowState()
        {
            _body.AddBodyXp(1000f);
            Assert.That(_body.TryLevelUp(), Is.True);
            Assert.That(_body.Level, Is.EqualTo(BodyLevel.Copper));
            Assert.That(_body.Xp, Is.EqualTo(1000f));
            Assert.That(_save.SaveGame(0), Is.True, _save.LastError);

            string profilePath = Path.Combine(
                _storageRoot,
                "SaveSlot_0",
                "profile.json");
            string profileJson = File.ReadAllText(profilePath);
            StringAssert.Contains("\"bodyLevel\": 1", profileJson);
            StringAssert.Contains("\"bodyXp\": 1000", profileJson);

            _save.Profile.BodyLevel = (int)BodyLevel.Mortal;
            _save.Profile.BodyXp = 0f;
            Assert.That(_body.Level, Is.EqualTo(BodyLevel.Mortal));
            Assert.That(_body.Xp, Is.Zero);

            Assert.That(_save.LoadGame(0), Is.True, _save.LastError);
            Assert.That(_body.Level, Is.EqualTo(BodyLevel.Copper));
            Assert.That(_body.Xp, Is.EqualTo(1000f));
            _panel.SetOpen(true);
            Assert.That(_panel.BodyText, Does.Contain("铜皮铁骨"));
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
            DestroyAll<CharacterPanelView>();
            DestroyAll<Wendao.Systems.Loot.WorldItemPickup>();
            DestroyAll<EnemyBrain>();
            DestroyAll<EnemySpawner>();
            DestroyAll<Wendao.UI.Inventory.InventoryPanelView>();
            DestroyAll<Wendao.Entities.NPC.NPCController>();
            DestroyAll<PlayerController>();
            DestroyAll<TrainingDummy>();
            DestroyAll<ThirdPersonCamera>();
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
