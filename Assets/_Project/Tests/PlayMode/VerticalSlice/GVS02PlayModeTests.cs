using System;
using System.Collections;
using System.Collections.Generic;
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
using Wendao.Systems.Input;
using Wendao.Systems.Tutorial;
using Wendao.Systems.World;
using Wendao.UI.Combat;
using Wendao.UI.SceneFlow;
using Wendao.UI.Tutorial;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class GVS02PlayModeTests
    {
        private string _storageRoot;

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
                "WendaoGVS02Tests_" + Guid.NewGuid().ToString("N"));
            SaveManager.Instance.ConfigureStorageRoot(_storageRoot);
            ServiceLocator.Get<Wendao.Systems.Cultivation.ISpiritRootService>()
                .ChooseRoot(SpiritRootType.Fire);
            Assert.That(SaveManager.Instance.SaveGame(0), Is.True);
            SaveManager.Instance.World.TutorialsCompleted.Add(
                TutorialManager.MoveTutorialId);
            Assert.That(SaveManager.Instance.TrySaveModule("world"), Is.True);

            AsyncOperation load = SceneManager.LoadSceneAsync(
                SceneLoader.DefaultMapSceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;

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

            PlayerRuntimeBootstrap.EnsureForScene(SceneManager.GetActiveScene());
            TrainingDummyRuntimeBootstrap.EnsureForScene(SceneManager.GetActiveScene());
            SceneUiBootstrap.EnsureForScene(SceneManager.GetActiveScene());
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
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
        public void LightAttackInputSupportsMouseAndGamepad()
        {
            InputActionAsset asset = Resources.Load<InputActionAsset>(
                PlayerInputReader.DefaultActionAssetResourcePath);
            Assert.That(asset, Is.Not.Null);
            InputAction action = asset
                .FindActionMap(PlayerInputReader.PlayerActionMapName, true)
                .FindAction("LightAttack", true);

            AssertBinding(action, "<Mouse>/leftButton");
            AssertBinding(action, "<Gamepad>/rightTrigger");
        }

        [Test]
        public void DamagePipelineAppliesAttackMultiplierDefenseAndTrueDamage()
        {
            CombatSystem combat = Object.FindAnyObjectByType<CombatSystem>();
            PlayerStats playerStats = Object.FindAnyObjectByType<PlayerStats>();
            TrainingDummy dummy = Object.FindAnyObjectByType<TrainingDummy>();
            Assert.That(combat, Is.Not.Null);
            Assert.That(playerStats, Is.Not.Null);
            Assert.That(dummy, Is.Not.Null);

            playerStats.ConfigureBaseStats(100f, 20f, 5f, 0f, 1.5f);
            dummy.ConfigureStats(100f, 20f);
            var request = new DamageRequest
            {
                Source = playerStats.gameObject,
                BaseDamage = 10f,
                Type = DamageType.Physical,
                Element = ElementType.None,
                Multiplier = 2f,
                CanCrit = false,
                SkillId = string.Empty
            };

            DamageInfo physical = combat.ComputeDamage(request, dummy);
            Assert.That(physical.Amount, Is.EqualTo(20f).Within(0.001f));
            Assert.That(physical.IsCritical, Is.False);

            request.Type = DamageType.True;
            DamageInfo trueDamage = combat.ComputeDamage(request, dummy);
            Assert.That(trueDamage.Amount, Is.EqualTo(24f).Within(0.001f));

            GameObject prefab = Resources.Load<GameObject>(
                PlayerRuntimeBootstrap.PlayerPrefabResourcePath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<PlayerStats>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<PlayerCombatController>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator LightAttackKillsDummyShowsDamageAndPersistsCombatTutorial()
        {
            PlayerController player = Object.FindAnyObjectByType<PlayerController>();
            PlayerStats stats = Object.FindAnyObjectByType<PlayerStats>();
            PlayerCombatController playerCombat =
                Object.FindAnyObjectByType<PlayerCombatController>();
            TrainingDummy dummy = Object.FindAnyObjectByType<TrainingDummy>();
            TutorialManager tutorial = Object.FindAnyObjectByType<TutorialManager>();
            TutorialToastView tutorialToast =
                Object.FindAnyObjectByType<TutorialToastView>();
            DamageFloatingTextView damageView =
                Object.FindAnyObjectByType<DamageFloatingTextView>();
            Assert.That(player, Is.Not.Null);
            Assert.That(stats, Is.Not.Null);
            Assert.That(playerCombat, Is.Not.Null);
            Assert.That(dummy, Is.Not.Null);
            Assert.That(tutorial, Is.Not.Null);
            Assert.That(tutorialToast, Is.Not.Null);
            Assert.That(damageView, Is.Not.Null);

            stats.ConfigureBaseStats(100f, 10f, 5f, 0f, 1.5f);
            dummy.ConfigureStats(TrainingDummy.DefaultMaxHp, 0f);
            player.TeleportTo(Vector3.zero, Quaternion.identity);
            Physics.SyncTransforms();

            ServiceLocator.Unregister<IPlayerInputSource>();
            var input = new FakePlayerInputSource();
            ServiceLocator.Register<IPlayerInputSource>(input);
            player.SetInputSource(input);
            playerCombat.SetInputSource(input);
            tutorial.SetInputSource(input);

            Assert.That(
                tutorial.ActiveTutorialId,
                Is.EqualTo(TutorialManager.CombatTutorialId));
            Assert.That(
                tutorial.ActiveStepId,
                Is.EqualTo(TutorialManager.CombatLightStepId));
            tutorial.RepublishActivePrompt();
            Assert.That(
                tutorialToast.CurrentLocalizationKey,
                Is.EqualTo(TutorialManager.CombatLightPromptKey));

            var killCalls = 0;
            EnemyDeathInfo observedDeath = default;
            Action<EnemyDeathInfo> killedHandler = info =>
            {
                observedDeath = info;
                killCalls++;
            };
            EventBus.Subscribe(CombatEvents.EnemyKilled, killedHandler);

            try
            {
                for (int attack = 0; attack < 4 && !dummy.IsDead; attack++)
                {
                    input.LightAttackPressedThisFrame = true;
                    yield return null;
                    input.LightAttackPressedThisFrame = false;

                    float deadline = Time.realtimeSinceStartup + 2f;
                    while (playerCombat.IsAttacking
                        && Time.realtimeSinceStartup < deadline)
                    {
                        yield return null;
                    }

                    Assert.That(playerCombat.IsAttacking, Is.False);
                    if (attack == 0 && !dummy.IsDead)
                    {
                        Assert.That(
                            tutorial.ActiveStepId,
                            Is.EqualTo(TutorialManager.CombatDefeatStepId));
                        Assert.That(
                            tutorialToast.CurrentLocalizationKey,
                            Is.EqualTo(TutorialManager.CombatDefeatPromptKey));
                    }

                    yield return null;
                }
            }
            finally
            {
                EventBus.Unsubscribe(CombatEvents.EnemyKilled, killedHandler);
            }

            Assert.That(dummy.IsDead, Is.True);
            Assert.That(dummy.CurrentHp, Is.Zero);
            Assert.That(killCalls, Is.EqualTo(1));
            Assert.That(
                observedDeath.EnemyId,
                Is.EqualTo(CombatContentIds.TrainingDummyEnemyId));
            Assert.That(damageView.LastDamageAmount, Is.GreaterThan(0f));
            Assert.That(damageView.LastRenderedText, Is.Not.Empty);
            Assert.That(damageView.ActiveNumberCount, Is.GreaterThan(0));
            Assert.That(
                tutorial.HasCompleted(TutorialManager.CombatTutorialId),
                Is.True);
            Assert.That(tutorial.IsActive, Is.False);
            Assert.That(
                tutorialToast.CurrentLocalizationKey,
                Is.EqualTo(TutorialManager.CombatCompletePromptKey));

            string worldPath = Path.Combine(
                _storageRoot,
                "SaveSlot_0",
                "world.json");
            Assert.That(File.Exists(worldPath), Is.True);
            StringAssert.Contains(
                TutorialManager.CombatTutorialId,
                File.ReadAllText(worldPath));

            Object.Destroy(tutorial.gameObject);
            Object.Destroy(SaveManager.Instance.gameObject);
            yield return null;

            var reloadedSave = new GameObject("[ReloadedSaveManager]")
                .AddComponent<SaveManager>();
            reloadedSave.ConfigureStorageRoot(_storageRoot);
            Assert.That(reloadedSave.LoadGame(0), Is.True);
            var reloadedTutorial = new GameObject("[ReloadedTutorialManager]")
                .AddComponent<TutorialManager>();
            reloadedTutorial.SetInputSource(input);
            yield return null;

            Assert.That(
                reloadedTutorial.HasCompleted(TutorialManager.CombatTutorialId),
                Is.True);
            Assert.That(
                reloadedTutorial.TryStart(TutorialManager.CombatTutorialId),
                Is.False);
        }

        [Test]
        public void LethalPlayerDamagePublishesEventsAndEntersDeadState()
        {
            CombatSystem combat = Object.FindAnyObjectByType<CombatSystem>();
            PlayerStats stats = Object.FindAnyObjectByType<PlayerStats>();
            PlayerController player = Object.FindAnyObjectByType<PlayerController>();
            TrainingDummy dummy = Object.FindAnyObjectByType<TrainingDummy>();
            Assert.That(combat, Is.Not.Null);
            Assert.That(stats, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(dummy, Is.Not.Null);

            var damageCalls = 0;
            var deathCalls = 0;
            var eventOrder = new List<string>();
            DamageInfo observedDamage = default;
            DeathInfo observedDeath = default;
            Action<DamageInfo> damagedHandler = info =>
            {
                observedDamage = info;
                damageCalls++;
                eventOrder.Add("player_damaged");
            };
            Action<DamageInfo> appliedHandler = _ => eventOrder.Add("damage_applied");
            Action<DeathInfo> diedHandler = info =>
            {
                observedDeath = info;
                deathCalls++;
                eventOrder.Add("player_died");
            };
            EventBus.Subscribe(CombatEvents.PlayerDamaged, damagedHandler);
            EventBus.Subscribe(CombatEvents.DamageApplied, appliedHandler);
            EventBus.Subscribe(CombatEvents.PlayerDied, diedHandler);

            try
            {
                combat.DealDamage(
                    stats,
                    new DamageRequest
                    {
                        Source = dummy.gameObject,
                        BaseDamage = 200f,
                        Type = DamageType.True,
                        Element = ElementType.None,
                        Multiplier = 1f,
                        CanCrit = false,
                        SkillId = string.Empty
                    });
            }
            finally
            {
                EventBus.Unsubscribe(CombatEvents.PlayerDamaged, damagedHandler);
                EventBus.Unsubscribe(CombatEvents.DamageApplied, appliedHandler);
                EventBus.Unsubscribe(CombatEvents.PlayerDied, diedHandler);
            }

            Assert.That(stats.CurrentHp, Is.Zero);
            Assert.That(stats.IsDead, Is.True);
            Assert.That(player.State, Is.EqualTo(PlayerState.Dead));
            Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Dead));
            Assert.That(damageCalls, Is.EqualTo(1));
            Assert.That(deathCalls, Is.EqualTo(1));
            Assert.That(observedDamage.IsKillingBlow, Is.True);
            Assert.That(observedDeath.Victim, Is.EqualTo(player.gameObject));
            Assert.That(observedDeath.Killer, Is.EqualTo(dummy.gameObject));
            CollectionAssert.AreEqual(
                new[] { "player_damaged", "damage_applied", "player_died" },
                eventOrder);
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

        private static void AssertBinding(InputAction action, string effectivePath)
        {
            bool found = false;
            foreach (InputBinding binding in action.bindings)
            {
                if (binding.effectivePath == effectivePath)
                {
                    found = true;
                    break;
                }
            }

            Assert.That(
                found,
                Is.True,
                $"{action.name} must contain binding {effectivePath}.");
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
