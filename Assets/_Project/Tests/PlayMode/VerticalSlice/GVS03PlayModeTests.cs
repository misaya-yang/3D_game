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
using Wendao.Systems.Equipment;
using Wendao.Systems.Input;
using Wendao.Systems.Inventory;
using Wendao.Systems.Tutorial;
using Wendao.Systems.World;
using Wendao.UI.Combat;
using Wendao.UI.Common;
using Wendao.UI.Inventory;
using Wendao.UI.SceneFlow;
using Wendao.UI.Tutorial;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class GVS03PlayModeTests
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
                "WendaoGVS03Tests_" + Guid.NewGuid().ToString("N"));
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
        public void BuiltInContentAndInventoryInputMatchAuthoritativeIdsAndValues()
        {
            ItemData potion = ConfigDatabase.Instance.GetItem(
                InventoryContentIds.HealPotion01);
            ItemData swordItem = ConfigDatabase.Instance.GetItem(
                InventoryContentIds.WoodSword);
            EquipmentData sword = ConfigDatabase.Instance.GetEquipment(
                InventoryContentIds.WoodSword);
            Assert.That(potion, Is.Not.Null);
            Assert.That(potion.MaxStack, Is.EqualTo(20));
            Assert.That(potion.UseEffects, Has.Length.EqualTo(1));
            Assert.That(potion.UseEffects[0].EffectType, Is.EqualTo(UseEffectType.Heal));
            Assert.That(potion.UseEffects[0].Value, Is.EqualTo(80f));
            Assert.That(swordItem, Is.Not.Null);
            Assert.That(swordItem.Type, Is.EqualTo(ItemType.Equipment));
            Assert.That(sword, Is.Not.Null);
            Assert.That(sword.Slot, Is.EqualTo(EquipmentSlot.Weapon));
            Assert.That(sword.BaseStats.Attack, Is.EqualTo(8f));

            IInventoryService inventory = ServiceLocator.Get<IInventoryService>();
            Assert.That(inventory.Slots, Has.Count.EqualTo(InventoryManager.Capacity));
            Assert.That(InventoryManager.Capacity, Is.EqualTo(50));

            InputActionAsset asset = Resources.Load<InputActionAsset>(
                PlayerInputReader.DefaultActionAssetResourcePath);
            InputAction openInventory = asset
                .FindActionMap(PlayerInputReader.PlayerActionMapName, true)
                .FindAction("OpenInventory", true);
            AssertBinding(openInventory, "<Keyboard>/b");
            AssertBinding(openInventory, "<Gamepad>/select");
        }

        [Test]
        public void HealPotionRestoresHpPublishesEventsAndIsNotConsumedAtFullHp()
        {
            IInventoryService inventory = ServiceLocator.Get<IInventoryService>();
            IItemUseService itemUse = ServiceLocator.Get<IItemUseService>();
            PlayerStats stats = Object.FindAnyObjectByType<PlayerStats>();
            GameToastView toast = Object.FindAnyObjectByType<GameToastView>();
            Assert.That(stats, Is.Not.Null);
            Assert.That(toast, Is.Not.Null);
            stats.ConfigureBaseStats(100f, 10f, 5f);
            stats.SetHp(20f);

            Assert.That(
                inventory.AddItem(
                    InventoryContentIds.HealPotion01,
                    2,
                    AcquireSource.Cheat),
                Is.True);
            int potionSlot = FindSlot(inventory, InventoryContentIds.HealPotion01);
            Assert.That(potionSlot, Is.GreaterThanOrEqualTo(0));

            var healedCalls = 0;
            var usedCalls = 0;
            Action<HealInfo> healed = _ => healedCalls++;
            Action<ItemUseInfo> used = _ => usedCalls++;
            EventBus.Subscribe(CombatEvents.PlayerHealed, healed);
            EventBus.Subscribe(InventoryEvents.ItemUsed, used);
            try
            {
                Assert.That(itemUse.CanUse(potionSlot), Is.True);
                Assert.That(itemUse.Use(potionSlot), Is.True);
            }
            finally
            {
                EventBus.Unsubscribe(CombatEvents.PlayerHealed, healed);
                EventBus.Unsubscribe(InventoryEvents.ItemUsed, used);
            }

            Assert.That(stats.CurrentHp, Is.EqualTo(100f));
            Assert.That(
                inventory.CountItem(InventoryContentIds.HealPotion01),
                Is.EqualTo(1));
            Assert.That(healedCalls, Is.EqualTo(1));
            Assert.That(usedCalls, Is.EqualTo(1));

            Assert.That(itemUse.CanUse(potionSlot), Is.False);
            Assert.That(itemUse.Use(potionSlot), Is.False);
            Assert.That(
                inventory.CountItem(InventoryContentIds.HealPotion01),
                Is.EqualTo(1));
            Assert.That(toast.CurrentLocalizationKey, Is.EqualTo(ItemUseSystem.FullHpToastKey));
        }

        [Test]
        public void EquippingWoodSwordRaisesAttackAndChangesComputedDamage()
        {
            IInventoryService inventory = ServiceLocator.Get<IInventoryService>();
            IEquipmentService equipment = ServiceLocator.Get<IEquipmentService>();
            ICombatService combat = ServiceLocator.Get<ICombatService>();
            PlayerStats stats = Object.FindAnyObjectByType<PlayerStats>();
            TrainingDummy dummy = Object.FindAnyObjectByType<TrainingDummy>();
            Assert.That(stats, Is.Not.Null);
            Assert.That(dummy, Is.Not.Null);
            stats.ConfigureBaseStats(100f, 10f, 5f, 0f, 1.5f);
            dummy.ConfigureStats(100f, 0f);

            var request = new DamageRequest
            {
                Source = stats.gameObject,
                BaseDamage = 10f,
                Type = DamageType.Physical,
                Element = ElementType.None,
                Multiplier = 1f,
                CanCrit = false,
                SkillId = string.Empty
            };
            float before = combat.ComputeDamage(request, dummy).Amount;

            Assert.That(
                inventory.AddItem(
                    InventoryContentIds.WoodSword,
                    1,
                    AcquireSource.Cheat),
                Is.True);
            int swordSlot = FindSlot(inventory, InventoryContentIds.WoodSword);
            Assert.That(equipment.EquipFromInventory(swordSlot), Is.True);

            float after = combat.ComputeDamage(request, dummy).Amount;
            Assert.That(stats.Attack, Is.EqualTo(18f).Within(0.001f));
            Assert.That(after, Is.EqualTo(11.8f).Within(0.001f));
            Assert.That(after, Is.GreaterThan(before));
            Assert.That(equipment.Worn.ContainsKey(EquipmentSlot.Weapon), Is.True);
            Assert.That(inventory.CountItem(InventoryContentIds.WoodSword), Is.Zero);
        }

        [Test]
        public void InventoryEquipmentAndCurrencyRoundTripThroughSeparateSaveModules()
        {
            IInventoryService inventory = ServiceLocator.Get<IInventoryService>();
            IEquipmentService equipment = ServiceLocator.Get<IEquipmentService>();
            PlayerStats stats = Object.FindAnyObjectByType<PlayerStats>();
            Assert.That(stats, Is.Not.Null);
            stats.ConfigureBaseStats(100f, 10f, 5f);

            Assert.That(
                inventory.AddItem(
                    InventoryContentIds.HealPotion01,
                    3,
                    AcquireSource.Cheat),
                Is.True);
            Assert.That(
                inventory.AddItem(
                    InventoryContentIds.WoodSword,
                    1,
                    AcquireSource.Cheat),
                Is.True);
            Assert.That(
                equipment.EquipFromInventory(
                    FindSlot(inventory, InventoryContentIds.WoodSword)),
                Is.True);
            Assert.That(inventory.AddSpiritStones(11), Is.True);
            Assert.That(SaveManager.Instance.SaveGame(0), Is.True);

            string inventoryPath = Path.Combine(
                _storageRoot,
                "SaveSlot_0",
                InventoryManager.SaveModuleName + ".json");
            string equipmentPath = Path.Combine(
                _storageRoot,
                "SaveSlot_0",
                EquipmentManager.SaveModuleName + ".json");
            Assert.That(File.Exists(inventoryPath), Is.True);
            Assert.That(File.Exists(equipmentPath), Is.True);
            StringAssert.Contains(
                InventoryContentIds.HealPotion01,
                File.ReadAllText(inventoryPath));
            StringAssert.Contains(
                InventoryContentIds.WoodSword,
                File.ReadAllText(equipmentPath));

            Assert.That(
                inventory.RemoveItem(InventoryContentIds.HealPotion01, 3),
                Is.True);
            Assert.That(equipment.Unequip(EquipmentSlot.Weapon), Is.True);
            Assert.That(inventory.RemoveItem(InventoryContentIds.WoodSword, 1), Is.True);
            Assert.That(inventory.AddSpiritStones(-11), Is.True);
            Assert.That(stats.Attack, Is.EqualTo(10f));

            Assert.That(SaveManager.Instance.LoadGame(0), Is.True);
            Assert.That(
                inventory.CountItem(InventoryContentIds.HealPotion01),
                Is.EqualTo(3));
            Assert.That(inventory.SpiritStones, Is.EqualTo(11));
            Assert.That(equipment.Worn.ContainsKey(EquipmentSlot.Weapon), Is.True);
            Assert.That(stats.Attack, Is.EqualTo(18f).Within(0.001f));
        }

        [Test]
        public void MinimalInventoryUiHasFiftySlotsAndSuspendsGameplayInput()
        {
            IInventoryService inventory = ServiceLocator.Get<IInventoryService>();
            IPlayerInputSource input = ServiceLocator.Get<IPlayerInputSource>();
            InventoryPanelView panel = Object.FindAnyObjectByType<InventoryPanelView>();
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.SlotButtonCount, Is.EqualTo(50));

            Assert.That(
                inventory.AddItem(
                    InventoryContentIds.HealPotion01,
                    1,
                    AcquireSource.Cheat),
                Is.True);
            int slot = FindSlot(inventory, InventoryContentIds.HealPotion01);
            panel.SetOpen(true);
            panel.SelectSlot(slot);

            Assert.That(panel.IsOpen, Is.True);
            Assert.That(input.IsEnabled, Is.False);
            Assert.That(panel.SelectedSlot, Is.EqualTo(slot));
            Assert.That(
                panel.GetSlotLocalizationKey(slot),
                Is.EqualTo("item_name_item_potion_heal_01"));

            panel.SetOpen(false);
            Assert.That(panel.IsOpen, Is.False);
            Assert.That(input.IsEnabled, Is.True);
        }

        private static int FindSlot(IInventoryService inventory, string itemId)
        {
            for (int index = 0; index < inventory.Slots.Count; index++)
            {
                if (string.Equals(
                        inventory.Slots[index].ItemId,
                        itemId,
                        StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
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
            DestroyAll<Wendao.UI.Shop.ShopPanelView>();
            DestroyAll<Wendao.Systems.Shop.ShopSystem>();
            DestroyAll<Wendao.Systems.Loot.WorldItemPickup>();
            DestroyAll<EnemyBrain>();
            DestroyAll<EnemySpawner>();
            DestroyAll<PlayerController>();
            DestroyAll<Wendao.Entities.NPC.NPCController>();
            DestroyAll<TrainingDummy>();
            DestroyAll<ThirdPersonCamera>();
            DestroyAll<InventoryPanelView>();
            DestroyAll<Wendao.UI.Cultivation.SpiritRootSelectionView>();
            DestroyAll<Wendao.UI.Cultivation.CultivationHudView>();
            DestroyAll<Wendao.UI.Skill.SkillQuickbarView>();
            DestroyAll<GameToastView>();
            DestroyAll<Wendao.UI.NPC.DialogueView>();
            DestroyAll<Wendao.UI.Quest.QuestTrackerView>();
            DestroyAll<DamageFloatingTextView>();
            DestroyAll<TutorialToastView>();
            DestroyAll<TutorialManager>();
            DestroyAll<Wendao.Systems.Loot.LootSystem>();
            DestroyAll<Wendao.Systems.Skill.SkillProjectile>();
            DestroyAll<Wendao.Systems.Skill.SkillManager>();
            DestroyAll<Wendao.Systems.NPC.DialogueManager>();
            DestroyAll<Wendao.Systems.Quest.QuestManager>();
            DestroyAll<Wendao.Systems.Cultivation.CultivationManager>();
            DestroyAll<Wendao.Systems.Cultivation.SpiritRootSystem>();
            DestroyAll<EquipmentManager>();
            DestroyAll<ItemUseSystem>();
            DestroyAll<InventoryManager>();
            DestroyAll<Wendao.Systems.Crafting.AlchemySystem>();
            DestroyAll<Wendao.Systems.Crafting.GatheringSystem>();
            DestroyAll<Wendao.UI.Crafting.AlchemyPanelView>();
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
