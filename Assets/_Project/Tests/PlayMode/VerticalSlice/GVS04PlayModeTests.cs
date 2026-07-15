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
using Wendao.Systems.Skill;
using Wendao.Systems.Tutorial;
using Wendao.Systems.World;
using Wendao.UI.Combat;
using Wendao.UI.Common;
using Wendao.UI.Inventory;
using Wendao.UI.SceneFlow;
using Wendao.UI.Skill;
using Wendao.UI.Tutorial;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class GVS04PlayModeTests
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
                "WendaoGVS04Tests_" + Guid.NewGuid().ToString("N"));
            SaveManager.Instance.ConfigureStorageRoot(_storageRoot);
            ServiceLocator.Get<ISpiritRootService>().ChooseRoot(SpiritRootType.Fire);
            Assert.That(SaveManager.Instance.SaveGame(0), Is.True);

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
        public void QiBoltContentInputDefaultLoadoutAndQuickbarMatchContract()
        {
            SkillData skill = ConfigDatabase.Instance.GetSkill(
                SkillContentIds.BasicQiBolt);
            Assert.That(skill, Is.Not.Null);
            Assert.That(skill.DisplayName, Is.EqualTo("引气弹"));
            Assert.That(skill.Type, Is.EqualTo(SkillType.Active));
            Assert.That(skill.Element, Is.EqualTo(SkillElement.None));
            Assert.That(skill.BaseDamage, Is.EqualTo(30f));
            Assert.That(skill.BaseManaCost, Is.EqualTo(10f));
            Assert.That(skill.BaseCooldown, Is.EqualTo(1.5f));
            Assert.That(skill.CastTime, Is.EqualTo(0.2f));
            Assert.That(skill.RecoveryTime, Is.EqualTo(0.3f));
            Assert.That(skill.Range, Is.EqualTo(12f));
            Assert.That(skill.IsProjectile, Is.True);

            ISkillService skillService = ServiceLocator.Get<ISkillService>();
            Assert.That(skillService.Learned, Has.Count.EqualTo(1));
            Assert.That(
                skillService.Learned[0].SkillId,
                Is.EqualTo(SkillContentIds.BasicQiBolt));
            Assert.That(skillService.EquippedIds, Has.Length.EqualTo(4));
            Assert.That(
                skillService.EquippedIds[0],
                Is.EqualTo(SkillContentIds.BasicQiBolt));

            InputActionAsset asset = Resources.Load<InputActionAsset>(
                PlayerInputReader.DefaultActionAssetResourcePath);
            InputAction skillOne = asset
                .FindActionMap(PlayerInputReader.PlayerActionMapName, true)
                .FindAction("Skill1", true);
            AssertBinding(skillOne, "<Keyboard>/1");
            AssertBinding(skillOne, "<Gamepad>/dpad/up");

            PlayerStats stats = Object.FindAnyObjectByType<PlayerStats>();
            PlayerSkillController controller =
                Object.FindAnyObjectByType<PlayerSkillController>();
            SkillQuickbarView quickbar =
                Object.FindAnyObjectByType<SkillQuickbarView>();
            Assert.That(stats, Is.Not.Null);
            Assert.That(controller, Is.Not.Null);
            Assert.That(stats.MaxMana, Is.EqualTo(50f));
            Assert.That(stats.CurrentMana, Is.EqualTo(50f));
            Assert.That(quickbar, Is.Not.Null);
            quickbar.Refresh();
            Assert.That(
                quickbar.CurrentSkillId,
                Is.EqualTo(SkillContentIds.BasicQiBolt));
            Assert.That(
                quickbar.CurrentSkillNameLocalizationKey,
                Is.EqualTo(SkillQuickbarView.SkillNameLocalizationKey));
            StringAssert.Contains("引气弹", quickbar.SlotText);
            StringAssert.Contains("50/50", quickbar.ManaText);
        }

        [UnityTest]
        public IEnumerator PressingSkillOneDealsDamageConsumesManaAndStartsCooldown()
        {
            SkillManager skillManager = Object.FindAnyObjectByType<SkillManager>();
            PlayerController player = Object.FindAnyObjectByType<PlayerController>();
            PlayerSkillController skillController =
                Object.FindAnyObjectByType<PlayerSkillController>();
            PlayerCombatController combatController =
                Object.FindAnyObjectByType<PlayerCombatController>();
            PlayerStats stats = Object.FindAnyObjectByType<PlayerStats>();
            TrainingDummy dummy = Object.FindAnyObjectByType<TrainingDummy>();
            SkillQuickbarView quickbar =
                Object.FindAnyObjectByType<SkillQuickbarView>();
            Assert.That(skillManager, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(skillController, Is.Not.Null);
            Assert.That(stats, Is.Not.Null);
            Assert.That(dummy, Is.Not.Null);

            stats.ConfigureMana(50f);
            dummy.ConfigureStats(100f, 0f);
            player.TeleportTo(Vector3.zero, Quaternion.identity);
            dummy.transform.SetPositionAndRotation(
                new Vector3(0f, 1f, 3f),
                Quaternion.identity);
            Physics.SyncTransforms();

            ServiceLocator.Unregister<IPlayerInputSource>();
            var input = new FakePlayerInputSource();
            ServiceLocator.Register<IPlayerInputSource>(input);
            player.SetInputSource(input);
            skillController.SetInputSource(input);
            combatController.SetInputSource(input);

            var castCalls = 0;
            var damageCalls = 0;
            SkillCastInfo observedCast = default;
            DamageInfo observedDamage = default;
            Action<SkillCastInfo> castHandler = info =>
            {
                observedCast = info;
                castCalls++;
            };
            Action<DamageInfo> damageHandler = info =>
            {
                if (info.SkillId == SkillContentIds.BasicQiBolt)
                {
                    observedDamage = info;
                    damageCalls++;
                }
            };
            EventBus.Subscribe(SkillEvents.SkillCast, castHandler);
            EventBus.Subscribe(CombatEvents.DamageApplied, damageHandler);

            try
            {
                input.Skill1PressedThisFrame = true;
                yield return null;
                input.Skill1PressedThisFrame = false;

                float deadline = Time.realtimeSinceStartup + 2f;
                while (dummy.CurrentHp >= 100f
                    && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.That(dummy.CurrentHp, Is.LessThan(100f));
            }
            finally
            {
                EventBus.Unsubscribe(SkillEvents.SkillCast, castHandler);
                EventBus.Unsubscribe(CombatEvents.DamageApplied, damageHandler);
            }

            Assert.That(castCalls, Is.EqualTo(1));
            Assert.That(damageCalls, Is.EqualTo(1));
            Assert.That(
                observedCast.SkillId,
                Is.EqualTo(SkillContentIds.BasicQiBolt));
            Assert.That(
                observedDamage.SkillId,
                Is.EqualTo(SkillContentIds.BasicQiBolt));
            Assert.That(observedDamage.Amount, Is.GreaterThan(0f));
            Assert.That(stats.CurrentMana, Is.EqualTo(40f).Within(0.001f));
            Assert.That(skillManager.GetCooldownRemaining(0), Is.GreaterThan(0f));

            float recoveryDeadline = Time.realtimeSinceStartup + 1f;
            while (skillManager.IsCasting
                && Time.realtimeSinceStartup < recoveryDeadline)
            {
                yield return null;
            }

            Assert.That(skillManager.IsCasting, Is.False);
            Assert.That(
                skillManager.TryCast(
                    0,
                    dummy.transform.position,
                    dummy.gameObject),
                Is.False,
                "Cooldown must prevent immediate repeat casting.");
            quickbar.Refresh();
            Assert.That(quickbar.CooldownText, Is.Not.Empty);

            skillManager.TickCooldowns(10f);
            player.ForceState(PlayerState.Idle);
            Assert.That(skillManager.CanCast(0), Is.True);
        }

        [Test]
        public void InsufficientManaRejectsCastWithoutStateOrCooldownAndShowsToast()
        {
            SkillManager skillManager = Object.FindAnyObjectByType<SkillManager>();
            PlayerController player = Object.FindAnyObjectByType<PlayerController>();
            PlayerStats stats = Object.FindAnyObjectByType<PlayerStats>();
            TrainingDummy dummy = Object.FindAnyObjectByType<TrainingDummy>();
            GameToastView toast = Object.FindAnyObjectByType<GameToastView>();
            Assert.That(skillManager, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(stats, Is.Not.Null);
            Assert.That(dummy, Is.Not.Null);
            Assert.That(toast, Is.Not.Null);

            skillManager.TickCooldowns(10f);
            player.ForceState(PlayerState.Idle);
            stats.SetMana(0f);
            Assert.That(
                skillManager.TryCast(
                    0,
                    dummy.transform.position,
                    dummy.gameObject),
                Is.False);

            Assert.That(stats.CurrentMana, Is.Zero);
            Assert.That(player.State, Is.EqualTo(PlayerState.Idle));
            Assert.That(skillManager.IsCasting, Is.False);
            Assert.That(skillManager.GetCooldownRemaining(0), Is.Zero);
            Assert.That(
                toast.CurrentLocalizationKey,
                Is.EqualTo(SkillManager.ManaInsufficientToastKey));
            Assert.That(
                Object.FindObjectsByType<SkillProjectile>(FindObjectsInactive.Include),
                Is.Empty);
        }

        [Test]
        public void SkillLoadoutAndCooldownRoundTripThroughSkillsModule()
        {
            SkillManager skillManager = Object.FindAnyObjectByType<SkillManager>();
            Assert.That(skillManager, Is.Not.Null);
            SkillRuntime runtime = skillManager.Learned[0];
            runtime.CooldownRemaining = 0.75f;
            Assert.That(SaveManager.Instance.SaveGame(0), Is.True);

            string skillPath = Path.Combine(
                _storageRoot,
                "SaveSlot_0",
                SkillManager.SaveModuleName + ".json");
            Assert.That(File.Exists(skillPath), Is.True);
            StringAssert.Contains(
                SkillContentIds.BasicQiBolt,
                File.ReadAllText(skillPath));

            skillManager.EquippedIds[0] = string.Empty;
            runtime.CooldownRemaining = 0f;
            Assert.That(SaveManager.Instance.LoadGame(0), Is.True);

            Assert.That(skillManager.Learned, Has.Count.EqualTo(1));
            Assert.That(
                skillManager.EquippedIds[0],
                Is.EqualTo(SkillContentIds.BasicQiBolt));
            Assert.That(
                skillManager.Learned[0].CooldownRemaining,
                Is.EqualTo(0.75f).Within(0.001f));
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

            Assert.That(found, Is.True, $"{action.name} must contain {effectivePath}.");
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
            DestroyAll<SkillQuickbarView>();
            DestroyAll<Wendao.UI.Cultivation.SpiritRootSelectionView>();
            DestroyAll<Wendao.UI.Cultivation.CultivationHudView>();
            DestroyAll<InventoryPanelView>();
            DestroyAll<GameToastView>();
            DestroyAll<Wendao.UI.NPC.DialogueView>();
            DestroyAll<Wendao.UI.Quest.QuestTrackerView>();
            DestroyAll<DamageFloatingTextView>();
            DestroyAll<TutorialToastView>();
            DestroyAll<TutorialManager>();
            DestroyAll<Wendao.Systems.Loot.LootSystem>();
            DestroyAll<SkillProjectile>();
            DestroyAll<SkillManager>();
            DestroyAll<Wendao.Systems.NPC.DialogueManager>();
            DestroyAll<Wendao.Systems.Quest.QuestManager>();
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
