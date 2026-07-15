using System;
using System.Collections;
using System.IO;
using System.Linq;
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
using Wendao.Systems.Input;
using Wendao.Systems.Tutorial;
using Wendao.Systems.World;
using Wendao.UI.SceneFlow;
using Wendao.UI.Combat;
using Wendao.UI.Tutorial;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class GVS01PlayModeTests
    {
        private string _storageRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EventBus.Clear();
            DestroyRuntimeObjects();
            yield return null;
            ServiceLocator.Clear();

            SceneFlowBootstrap.Install();
            SceneUiBootstrap.Install();
            PlayerRuntimeBootstrap.Install();

            _storageRoot = Path.Combine(
                Path.GetTempPath(),
                "WendaoGVS01Tests_" + Guid.NewGuid().ToString("N"));
            SaveManager.Instance.ConfigureStorageRoot(_storageRoot);
            ServiceLocator.Get<Wendao.Systems.Cultivation.ISpiritRootService>()
                .ChooseRoot(SpiritRootType.Fire);
            Assert.That(SaveManager.Instance.SaveGame(0), Is.True);

            AsyncOperation load = SceneManager.LoadSceneAsync(
                SceneLoader.DefaultMapSceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;

            GameManager gameManager = GameManager.Instance;
            Assert.That(gameManager, Is.Not.Null);
            Assert.That(gameManager.TrySetState(GameState.MainMenu), Is.True);
            Assert.That(gameManager.TrySetState(GameState.Loading), Is.True);
            Assert.That(gameManager.TrySetState(GameState.Playing), Is.True);

            PlayerRuntimeBootstrap.EnsureForScene(SceneManager.GetActiveScene());
            SceneUiBootstrap.EnsureForScene(SceneManager.GetActiveScene());
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            EventBus.Clear();
            DestroyRuntimeObjects();
            yield return null;
            ServiceLocator.Clear();

            if (!string.IsNullOrEmpty(_storageRoot) && Directory.Exists(_storageRoot))
            {
                Directory.Delete(_storageRoot, true);
            }
        }

        [Test]
        public void InputActionsContainKeyboardMouseAndGamepadLocomotionBindings()
        {
            InputActionAsset asset = Resources.Load<InputActionAsset>(
                PlayerInputReader.DefaultActionAssetResourcePath);
            Assert.That(asset, Is.Not.Null);

            InputActionMap playerMap = asset.FindActionMap(
                PlayerInputReader.PlayerActionMapName,
                true);
            AssertBinding(playerMap.FindAction("Move", true), "<Keyboard>/w");
            AssertBinding(playerMap.FindAction("Move", true), "<Gamepad>/leftStick");
            AssertBinding(playerMap.FindAction("Look", true), "<Mouse>/delta");
            AssertBinding(playerMap.FindAction("Look", true), "<Gamepad>/rightStick");
            AssertBinding(playerMap.FindAction("Jump", true), "<Keyboard>/space");
            AssertBinding(playerMap.FindAction("Jump", true), "<Gamepad>/buttonSouth");
            AssertBinding(playerMap.FindAction("Sprint", true), "<Keyboard>/leftShift");

            Assert.That(
                Resources.Load<GameObject>(PlayerRuntimeBootstrap.PlayerPrefabResourcePath),
                Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator PlayerMovesSprintsJumpsAndCameraAvoidsObstacle()
        {
            PlayerController player = Object.FindAnyObjectByType<PlayerController>();
            ThirdPersonCamera camera = Object.FindAnyObjectByType<ThirdPersonCamera>();
            Assert.That(player, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);

            var input = new FakePlayerInputSource();
            player.SetInputSource(input);
            camera.SetInputSource(input);
            player.TeleportTo(Vector3.zero, Quaternion.identity);
            Physics.SyncTransforms();
            yield return null;

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "CameraCollisionProbe";
            wall.transform.SetPositionAndRotation(
                new Vector3(0f, 1.8f, -2.5f),
                Quaternion.identity);
            wall.transform.localScale = new Vector3(4f, 4f, 0.5f);
            Physics.SyncTransforms();
            yield return null;

            Assert.That(camera.CurrentDistance, Is.LessThan(5f));
            Assert.That(camera.transform.position.z, Is.GreaterThan(-2.5f));
            Object.Destroy(wall);
            yield return null;

            Vector3 start = player.transform.position;
            input.Move = Vector2.up;
            yield return new WaitForSeconds(0.1f);

            Assert.That(player.transform.position.z, Is.GreaterThan(start.z + 0.1f));
            Assert.That(player.State, Is.EqualTo(PlayerState.Move));

            input.SprintHeld = true;
            yield return new WaitForSeconds(0.02f);

            Assert.That(player.State, Is.EqualTo(PlayerState.Sprint));

            input.Move = Vector2.zero;
            input.SprintHeld = false;
            input.JumpPressedThisFrame = true;
            yield return null;
            input.JumpPressedThisFrame = false;
            yield return new WaitForSeconds(0.03f);
            float jumpHeight = player.transform.position.y;

            Assert.That(jumpHeight, Is.GreaterThan(0.05f));
            Assert.That(
                player.State == PlayerState.Jump || player.State == PlayerState.Fall,
                Is.True);

            float yawBefore = camera.Yaw;
            input.LookIsPointerDelta = true;
            input.Look = new Vector2(10f, 0f);
            yield return null;
            input.Look = Vector2.zero;
            Assert.That(camera.Yaw, Is.GreaterThan(yawBefore));
        }

        [UnityTest]
        public IEnumerator MoveTutorialCompletesPersistsAndDoesNotReplayAfterLoad()
        {
            TutorialManager tutorial = Object.FindAnyObjectByType<TutorialManager>();
            TutorialToastView toast = Object.FindAnyObjectByType<TutorialToastView>();
            Assert.That(tutorial, Is.Not.Null);
            Assert.That(toast, Is.Not.Null);

            Object.Destroy(tutorial.gameObject);
            yield return null;
            ServiceLocator.Unregister<IPlayerInputSource>();
            var tutorialInput = new FakePlayerInputSource();
            ServiceLocator.Register<IPlayerInputSource>(tutorialInput);
            tutorial = new GameObject("[InputDrivenTutorialManager]")
                .AddComponent<TutorialManager>();
            yield return null;

            Assert.That(tutorial.ActiveStepId, Is.EqualTo(TutorialManager.MoveStepId));
            tutorialInput.Move = Vector2.up;
            yield return null;
            tutorialInput.Move = Vector2.zero;
            Assert.That(tutorial.ActiveStepId, Is.EqualTo(TutorialManager.LookStepId));
            Assert.That(
                toast.CurrentLocalizationKey,
                Is.EqualTo(TutorialManager.LookPromptKey));

            tutorialInput.Look = Vector2.right;
            yield return null;
            tutorialInput.Look = Vector2.zero;
            Assert.That(tutorial.ActiveStepId, Is.EqualTo(TutorialManager.JumpStepId));
            tutorialInput.JumpPressedThisFrame = true;
            yield return null;
            tutorialInput.JumpPressedThisFrame = false;

            Assert.That(tutorial.IsActive, Is.False);
            Assert.That(tutorial.HasCompleted(TutorialManager.MoveTutorialId), Is.True);
            Assert.That(
                toast.CurrentLocalizationKey,
                Is.EqualTo(TutorialManager.CompletePromptKey));

            string worldPath = Path.Combine(
                _storageRoot,
                "SaveSlot_0",
                "world.json");
            Assert.That(File.Exists(worldPath), Is.True);
            StringAssert.Contains(TutorialManager.MoveTutorialId, File.ReadAllText(worldPath));

            Object.Destroy(tutorial.gameObject);
            Object.Destroy(SaveManager.Instance.gameObject);
            yield return null;

            var reloadedSave = new GameObject("[ReloadedSaveManager]")
                .AddComponent<SaveManager>();
            reloadedSave.ConfigureStorageRoot(_storageRoot);
            Assert.That(reloadedSave.LoadGame(0), Is.True);
            var reloadedTutorial = new GameObject("[ReloadedTutorialManager]")
                .AddComponent<TutorialManager>();
            yield return null;

            Assert.That(
                reloadedTutorial.HasCompleted(TutorialManager.MoveTutorialId),
                Is.True);
            Assert.That(
                reloadedTutorial.TryStart(TutorialManager.MoveTutorialId),
                Is.False);
        }

        private static void AssertBinding(InputAction action, string effectivePath)
        {
            Assert.That(
                action.bindings.Any(binding => binding.effectivePath == effectivePath),
                Is.True,
                $"{action.name} must contain binding {effectivePath}.");
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
            DestroyAll<DamageFloatingTextView>();
            DestroyAll<TutorialToastView>();
            DestroyAll<TutorialManager>();
            DestroyAll<Wendao.Systems.Loot.LootSystem>();
            DestroyAll<Wendao.UI.Cultivation.SpiritRootSelectionView>();
            DestroyAll<Wendao.UI.Cultivation.CultivationHudView>();
            DestroyAll<Wendao.UI.Skill.SkillQuickbarView>();
            DestroyAll<Wendao.UI.Inventory.InventoryPanelView>();
            DestroyAll<Wendao.UI.Common.GameToastView>();
            DestroyAll<Wendao.UI.NPC.DialogueView>();
            DestroyAll<Wendao.UI.Quest.QuestTrackerView>();
            DestroyAll<Wendao.Systems.Skill.SkillProjectile>();
            DestroyAll<Wendao.Systems.Skill.SkillManager>();
            DestroyAll<Wendao.Systems.NPC.DialogueManager>();
            DestroyAll<Wendao.Systems.Quest.QuestManager>();
            DestroyAll<Wendao.Systems.Cultivation.CultivationManager>();
            DestroyAll<Wendao.Systems.Cultivation.SpiritRootSystem>();
            DestroyAll<Wendao.Systems.Equipment.EquipmentManager>();
            DestroyAll<Wendao.Systems.Inventory.ItemUseSystem>();
            DestroyAll<Wendao.Systems.Inventory.InventoryManager>();
            DestroyAll<StatusEffectManager>();
            DestroyAll<CombatSystem>();
            DestroyAll<SceneLoader>();
            DestroyAll<Wendao.Systems.Crafting.AlchemySystem>();
            DestroyAll<Wendao.Systems.Shop.ShopSystem>();
            DestroyAll<Wendao.Systems.Crafting.GatheringSystem>();
            DestroyAll<Wendao.UI.Crafting.AlchemyPanelView>();
            DestroyAll<Wendao.UI.Shop.ShopPanelView>();
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

        private sealed class FakePlayerInputSource : IPlayerInputSource
        {
            public Vector2 Move { get; set; }
            public Vector2 Look { get; set; }
            public bool LookIsPointerDelta { get; set; }
            public bool JumpPressedThisFrame { get; set; }
            public bool JumpHeld { get; set; }
            public bool SprintHeld { get; set; }
            public bool LightAttackPressedThisFrame { get; set; }
            public bool HeavyAttackPressedThisFrame { get; set; }
            public bool DodgePressedThisFrame { get; set; }
            public bool BlockHeld { get; set; }
            public bool LockOnPressedThisFrame { get; set; }
            public bool Skill1PressedThisFrame { get; set; }
            public bool Skill2PressedThisFrame { get; set; }
            public bool Skill3PressedThisFrame { get; set; }
            public bool Skill4PressedThisFrame { get; set; }
            public bool InteractPressedThisFrame { get; set; }
            public bool OpenInventoryPressedThisFrame { get; set; }
            public bool OpenCharacterPressedThisFrame { get; set; }
            public bool OpenSkillPressedThisFrame { get; set; }
            public bool OpenQuestPressedThisFrame { get; set; }
            public bool OpenMapPressedThisFrame { get; set; }
            public bool PausePressedThisFrame { get; set; }
            public bool MountPressedThisFrame { get; set; }
            public bool IsEnabled { get; private set; } = true;

            public void SetEnabled(bool enabled)
            {
                IsEnabled = enabled;
            }
        }
    }
}
