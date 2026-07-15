using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
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
using Wendao.Systems.World;
using Wendao.UI.SceneFlow;
using Wendao.UI.Skill;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0302PlayModeTests
    {
        private string _storageRoot;
        private SaveManager _save;
        private SkillManager _skills;
        private IInventoryService _inventory;
        private SkillPanelView _panel;
        private SkillQuickbarView _quickbar;

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
                "WendaoG0302Tests_" + Guid.NewGuid().ToString("N"));
            _save = SaveManager.Instance;
            _save.ConfigureStorageRoot(_storageRoot);
            ServiceLocator.Get<ISpiritRootService>()
                .ChooseRoot(SpiritRootType.Fire);
            _save.Profile.Realm = (int)RealmType.GoldenCore;
            _save.Profile.SubStage = 1;
            Assert.That(_save.SaveGame(0), Is.True, _save.LastError);

            AsyncOperation load = SceneManager.LoadSceneAsync(
                SceneLoader.DefaultMapSceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;

            EnterPlayingState();
            PlayerRuntimeBootstrap.EnsureForScene(SceneManager.GetActiveScene());
            TrainingDummyRuntimeBootstrap.EnsureForScene(
                SceneManager.GetActiveScene());
            SceneUiBootstrap.EnsureForScene(SceneManager.GetActiveScene());
            yield return null;

            _skills = Object.FindAnyObjectByType<SkillManager>();
            _inventory = ServiceLocator.Get<IInventoryService>();
            _panel = Object.FindAnyObjectByType<SkillPanelView>();
            _quickbar = Object.FindAnyObjectByType<SkillQuickbarView>();
            Assert.That(_skills, Is.Not.Null);
            Assert.That(_inventory, Is.Not.Null);
            Assert.That(_panel, Is.Not.Null);
            Assert.That(_quickbar, Is.Not.Null);
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
        public void SevenContentSkillsCanBeLearnedAndAllFourInputSlotsAreBound()
        {
            Assert.That(SkillContentIds.All, Has.Length.EqualTo(7));
            var activeCount = 0;
            foreach (string skillId in SkillContentIds.All)
            {
                SkillData skill = ConfigDatabase.Instance.GetSkill(skillId);
                Assert.That(skill, Is.Not.Null, skillId);
                Assert.That(skill.DisplayName, Is.Not.Empty, skillId);
                Assert.That(skill.Description, Is.Not.Empty, skillId);
                if (skill.Type != SkillType.Passive)
                {
                    activeCount++;
                }

                if (!string.Equals(
                        skillId,
                        SkillContentIds.BasicQiBolt,
                        StringComparison.Ordinal))
                {
                    Assert.That(_skills.Learn(skillId), Is.True, skillId);
                }
            }

            Assert.That(activeCount, Is.EqualTo(6));
            Assert.That(_skills.Learned, Has.Count.EqualTo(7));
            Assert.That(
                ConfigDatabase.Instance.GetItem(InventoryContentIds.SkillScroll),
                Is.Not.Null);

            InputActionMap playerMap = Resources.Load<InputActionAsset>(
                    PlayerInputReader.DefaultActionAssetResourcePath)
                .FindActionMap(PlayerInputReader.PlayerActionMapName, true);
            string[] keyboardPaths = { "<Keyboard>/1", "<Keyboard>/2", "<Keyboard>/3", "<Keyboard>/4" };
            string[] gamepadPaths =
            {
                "<Gamepad>/dpad/up",
                "<Gamepad>/dpad/right",
                "<Gamepad>/dpad/down",
                "<Gamepad>/dpad/left"
            };
            for (int index = 0; index < SkillManager.BarSlotCount; index++)
            {
                InputAction action = playerMap.FindAction($"Skill{index + 1}", true);
                AssertBinding(action, keyboardPaths[index]);
                AssertBinding(action, gamepadPaths[index]);
            }

            AssertBinding(
                playerMap.FindAction("OpenSkill", true),
                "<Keyboard>/k");
            _panel.Refresh();
            Assert.That(_panel.LearnedButtonCount, Is.EqualTo(7));
            StringAssert.Contains("引气弹", _panel.GetRowText(0));
        }

        [Test]
        public void DraggingLearnedSkillsEquipsAllFourQuickbarSlotsAndPersists()
        {
            string[] equipped =
            {
                SkillContentIds.FireEmber,
                SkillContentIds.IceNeedle,
                SkillContentIds.LightningChain,
                SkillContentIds.WindSlash
            };
            foreach (string skillId in equipped)
            {
                Assert.That(_skills.Learn(skillId), Is.True, skillId);
            }

            _panel.Refresh();
            _quickbar.Refresh();
            Assert.That(_quickbar.SlotCount, Is.EqualTo(4));
            for (int index = 0; index < equipped.Length; index++)
            {
                SkillDragSource source = _panel.GetDragSource(equipped[index]);
                SkillQuickbarSlotDropTarget target = _quickbar.GetDropTarget(index);
                Assert.That(source, Is.Not.Null, equipped[index]);
                Assert.That(target, Is.Not.Null, index.ToString());

                var eventData = new PointerEventData(EventSystem.current)
                {
                    pointerDrag = source.gameObject
                };
                source.OnBeginDrag(eventData);
                Assert.That(source.IsDragging, Is.True);
                target.OnDrop(eventData);
                source.OnEndDrag(eventData);
                Assert.That(source.IsDragging, Is.False);
                Assert.That(target.LastDroppedSkillId, Is.EqualTo(equipped[index]));
                Assert.That(_skills.EquippedIds[index], Is.EqualTo(equipped[index]));
            }

            string skillPath = Path.Combine(
                _storageRoot,
                "SaveSlot_0",
                SkillManager.SaveModuleName + ".json");
            string saved = File.ReadAllText(skillPath);
            foreach (string skillId in equipped)
            {
                StringAssert.Contains(skillId, saved);
            }

            Array.Clear(_skills.EquippedIds, 0, _skills.EquippedIds.Length);
            Assert.That(_save.LoadGame(0), Is.True, _save.LastError);
            Assert.That(_skills.EquippedIds, Is.EqualTo(equipped));

            Assert.That(_skills.Learn(SkillContentIds.IronSkin), Is.True);
            _panel.Refresh();
            SkillDragSource passive = _panel.GetDragSource(SkillContentIds.IronSkin);
            var passiveDrop = new PointerEventData(EventSystem.current)
            {
                pointerDrag = passive.gameObject
            };
            _quickbar.GetDropTarget(0).OnDrop(passiveDrop);
            Assert.That(_skills.EquippedIds[0], Is.EqualTo(equipped[0]));
        }

        [Test]
        public void SkillPanelLocksAndRestoresGameplayInput()
        {
            IPlayerInputSource input = ServiceLocator.Get<IPlayerInputSource>();
            Assert.That(input.IsEnabled, Is.True);
            Assert.That(_panel.IsOpen, Is.False);

            _panel.SetOpen(true);
            Assert.That(_panel.IsOpen, Is.True);
            Assert.That(input.IsEnabled, Is.False);
            Assert.That(_panel.LearnedButtonCount, Is.GreaterThanOrEqualTo(1));

            _panel.SetOpen(false);
            Assert.That(_panel.IsOpen, Is.False);
            Assert.That(input.IsEnabled, Is.True);
        }

        [UnityTest]
        public IEnumerator SkillFourInputCastsTheSkillEquippedInSlotFour()
        {
            Assert.That(_skills.Learn(SkillContentIds.WindSlash), Is.True);
            Assert.That(_skills.Equip(SkillContentIds.WindSlash, 3), Is.True);

            PlayerController player = Object.FindAnyObjectByType<PlayerController>();
            PlayerSkillController controller =
                Object.FindAnyObjectByType<PlayerSkillController>();
            PlayerActionBuffer buffer = player.GetComponent<PlayerActionBuffer>();
            PlayerStats stats = player.GetComponent<PlayerStats>();
            TrainingDummy dummy = Object.FindAnyObjectByType<TrainingDummy>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(buffer, Is.Not.Null);
            Assert.That(dummy, Is.Not.Null);

            player.TeleportTo(Vector3.zero, Quaternion.identity);
            dummy.transform.SetPositionAndRotation(
                new Vector3(0f, 1f, 3f),
                Quaternion.identity);
            dummy.ConfigureStats(300f, 0f);
            stats.ConfigureMana(100f);
            controller.SetSkillService(_skills);
            player.ForceState(PlayerState.Idle);
            Physics.SyncTransforms();

            SkillCastInfo observed = default;
            var castCount = 0;
            Action<SkillCastInfo> handler = info =>
            {
                if (info.SkillId == SkillContentIds.WindSlash)
                {
                    observed = info;
                    castCount++;
                }
            };
            EventBus.Subscribe(SkillEvents.SkillCast, handler);
            try
            {
                buffer.EnqueueBufferedAction(BufferedActionType.Skill4);
                Assert.That(buffer.IsConsumptionEnabled, Is.True);
                Assert.That(
                    buffer.HasBufferedAction(BufferedActionType.Skill4),
                    Is.True);
                Assert.That(controller.TryProcessBufferedSkillInput(), Is.True);
                float deadline = Time.realtimeSinceStartup + 2f;
                while (castCount == 0 && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }
            }
            finally
            {
                EventBus.Unsubscribe(SkillEvents.SkillCast, handler);
            }

            Assert.That(castCount, Is.EqualTo(1));
            Assert.That(observed.SkillId, Is.EqualTo(SkillContentIds.WindSlash));
            Assert.That(stats.CurrentMana, Is.EqualTo(82f).Within(0.001f));
            Assert.That(_skills.GetCooldownRemaining(3), Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator SkillScrollUpgradeConsumesLevelCostRaisesDamageAndPersists()
        {
            Assert.That(
                _inventory.AddItem(
                    InventoryContentIds.SkillScroll,
                    6,
                    AcquireSource.Cheat),
                Is.True);
            SkillRuntime runtime = _skills.Learned[0];
            var upgradeCount = 0;
            SkillUpgradeInfo observedUpgrade = default;
            Action<SkillUpgradeInfo> upgradeHandler = info =>
            {
                observedUpgrade = info;
                upgradeCount++;
            };
            EventBus.Subscribe(SkillEvents.SkillUpgraded, upgradeHandler);
            try
            {
                Assert.That(_skills.TryUpgrade(runtime.SkillId), Is.True);
                Assert.That(_skills.TryUpgrade(runtime.SkillId), Is.True);
                Assert.That(_skills.TryUpgrade(runtime.SkillId), Is.True);
                Assert.That(_skills.TryUpgrade(runtime.SkillId), Is.False);
            }
            finally
            {
                EventBus.Unsubscribe(SkillEvents.SkillUpgraded, upgradeHandler);
            }

            Assert.That(runtime.Level, Is.EqualTo(4));
            Assert.That(upgradeCount, Is.EqualTo(3));
            Assert.That(observedUpgrade.NewLevel, Is.EqualTo(4));
            Assert.That(
                _inventory.CountItem(InventoryContentIds.SkillScroll),
                Is.Zero);

            string skillPath = Path.Combine(
                _storageRoot,
                "SaveSlot_0",
                SkillManager.SaveModuleName + ".json");
            StringAssert.Contains("\"level\": 4", File.ReadAllText(skillPath));
            runtime.Level = 1;
            Assert.That(_save.LoadGame(0), Is.True, _save.LastError);
            runtime = _skills.Learned[0];
            Assert.That(runtime.Level, Is.EqualTo(4));

            PlayerController player = Object.FindAnyObjectByType<PlayerController>();
            PlayerStats stats = player.GetComponent<PlayerStats>();
            TrainingDummy dummy = Object.FindAnyObjectByType<TrainingDummy>();
            player.TeleportTo(Vector3.zero, Quaternion.identity);
            dummy.transform.SetPositionAndRotation(
                new Vector3(0f, 1f, 3f),
                Quaternion.identity);
            dummy.ConfigureStats(1000f, 0f);
            stats.ConfigureBaseStats(
                stats.MaxHp,
                stats.Attack,
                stats.Defense,
                0f,
                stats.CritDamage);
            stats.ConfigureMana(100f);
            Physics.SyncTransforms();

            ICombatService combat = ServiceLocator.Get<ICombatService>();
            DamageInfo levelOne = combat.ComputeDamage(
                new DamageRequest
                {
                    Source = player.gameObject,
                    BaseDamage = 30f,
                    Type = DamageType.Physical,
                    Element = ElementType.None,
                    Multiplier = 1f,
                    CanCrit = false,
                    SkillId = SkillContentIds.BasicQiBolt
                },
                dummy);
            DamageInfo observedDamage = default;
            var damageCount = 0;
            Action<DamageInfo> damageHandler = info =>
            {
                if (info.SkillId == SkillContentIds.BasicQiBolt)
                {
                    observedDamage = info;
                    damageCount++;
                }
            };
            EventBus.Subscribe(CombatEvents.DamageApplied, damageHandler);
            try
            {
                Assert.That(
                    _skills.TryCast(
                        0,
                        dummy.transform.position,
                        dummy.gameObject),
                    Is.True);
                float deadline = Time.realtimeSinceStartup + 2f;
                while (damageCount == 0 && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }
            }
            finally
            {
                EventBus.Unsubscribe(CombatEvents.DamageApplied, damageHandler);
            }

            Assert.That(damageCount, Is.EqualTo(1));
            Assert.That(
                observedDamage.Amount,
                Is.EqualTo(levelOne.Amount * 1.5f).Within(0.01f));

            Assert.That(
                _inventory.AddItem(
                    InventoryContentIds.SkillScroll,
                    10,
                    AcquireSource.Cheat),
                Is.True);
            runtime.Level = ConfigDatabase.Instance
                .GetSkill(runtime.SkillId)
                .MaxLevel;
            int before = _inventory.CountItem(InventoryContentIds.SkillScroll);
            Assert.That(_skills.TryUpgrade(runtime.SkillId), Is.False);
            Assert.That(
                _inventory.CountItem(InventoryContentIds.SkillScroll),
                Is.EqualTo(before));
        }

        private static void AssertBinding(InputAction action, string effectivePath)
        {
            foreach (InputBinding binding in action.bindings)
            {
                if (binding.effectivePath == effectivePath)
                {
                    return;
                }
            }

            Assert.Fail($"{action.name} must contain {effectivePath}.");
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
            DestroyAll<SkillPanelView>();
            DestroyAll<SkillQuickbarView>();
            DestroyAll<SkillProjectile>();
            DestroyAll<Wendao.UI.Inventory.InventoryPanelView>();
            DestroyAll<Wendao.UI.Cultivation.CharacterPanelView>();
            DestroyAll<Wendao.UI.Cultivation.BreakthroughCeremonyView>();
            DestroyAll<Wendao.UI.Cultivation.CultivationHudView>();
            DestroyAll<Wendao.UI.Cultivation.SpiritRootSelectionView>();
            DestroyAll<Wendao.UI.Common.GameToastView>();
            DestroyAll<Wendao.UI.Combat.DamageFloatingTextView>();
            DestroyAll<Wendao.UI.Combat.DeathView>();
            DestroyAll<Wendao.UI.NPC.DialogueView>();
            DestroyAll<Wendao.UI.Quest.QuestTrackerView>();
            DestroyAll<Wendao.UI.Tutorial.TutorialToastView>();
            DestroyAll<Wendao.Systems.Tutorial.TutorialManager>();
            DestroyAll<Wendao.Systems.Loot.WorldItemPickup>();
            DestroyAll<Wendao.Systems.Loot.LootSystem>();
            DestroyAll<EnemyBrain>();
            DestroyAll<EnemySpawner>();
            DestroyAll<Wendao.Entities.NPC.NPCController>();
            DestroyAll<PlayerController>();
            DestroyAll<TrainingDummy>();
            DestroyAll<ThirdPersonCamera>();
            DestroyAll<SkillManager>();
            DestroyAll<RefineSystem>();
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
