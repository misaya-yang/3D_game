using System;
using System.Collections;
using System.IO;
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
using Wendao.Systems.Inventory;
using Wendao.Systems.World;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0203PlayModeTests
    {
        private string _storageRoot;
        private BodyRefinementManager _body;
        private CombatSystem _combat;
        private IEquipmentService _equipment;
        private IInventoryService _inventory;
        private PlayerStats _player;

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
                "WendaoG0203Tests_" + Guid.NewGuid().ToString("N"));
            SaveManager.Instance.ConfigureStorageRoot(_storageRoot);
            Assert.That(SaveManager.Instance.SaveGame(0), Is.True);
            SaveManager.Instance.Profile.SpiritRoot = SpiritRootType.Fire.ToString();

            AsyncOperation load = SceneManager.LoadSceneAsync(
                SceneLoader.DefaultMapSceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;

            EnterPlayingState();
            PlayerRuntimeBootstrap.EnsureForScene(SceneManager.GetActiveScene());
            yield return null;

            _body = Object.FindAnyObjectByType<BodyRefinementManager>();
            _combat = Object.FindAnyObjectByType<CombatSystem>();
            _equipment = ServiceLocator.Get<IEquipmentService>();
            _inventory = ServiceLocator.Get<IInventoryService>();
            _player = Object.FindAnyObjectByType<PlayerStats>();

            Assert.That(_body, Is.Not.Null);
            Assert.That(_combat, Is.Not.Null);
            Assert.That(_equipment, Is.Not.Null);
            Assert.That(_inventory, Is.Not.Null);
            Assert.That(_player, Is.Not.Null);
            Assert.That(_player.MaxHp, Is.EqualTo(100f).Within(0.001f));
            Assert.That(_player.Attack, Is.EqualTo(10f).Within(0.001f));
            Assert.That(_player.Defense, Is.EqualTo(5f).Within(0.001f));
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
        public void RealmEquipmentTitleAndBuffSourcesAggregateInDocumentedOrder()
        {
            EquipWoodSword();
            _player.SetTitleBonus(new StatBlock
            {
                Attack = 5f,
                Defense = 8f,
                CritRate = 0.01f,
                CritDamage = 0f
            });
            _player.SetBuffBonus(new StatBlock
            {
                MaxHp = 10f,
                Attack = 2f,
                CritDamage = 0f
            });

            Assert.That(_player.BaseFromRealm.MaxHp, Is.EqualTo(100f));
            Assert.That(_player.BaseFromRealm.MaxMana, Is.EqualTo(50f));
            Assert.That(_player.BaseFromRealm.Attack, Is.EqualTo(10f));
            Assert.That(_player.BaseFromRealm.Defense, Is.EqualTo(5f));
            Assert.That(_player.FromEquipment.Attack, Is.EqualTo(8f));
            Assert.That(_player.FromTitle.Attack, Is.EqualTo(5f));
            Assert.That(_player.FromBuffs.Attack, Is.EqualTo(2f));
            Assert.That(_player.Final.MaxHp, Is.EqualTo(110f).Within(0.001f));
            Assert.That(_player.Final.Attack, Is.EqualTo(25f).Within(0.001f));
            Assert.That(_player.Final.Defense, Is.EqualTo(13f).Within(0.001f));
            Assert.That(_player.Final.CritRate, Is.EqualTo(0.01f).Within(0.0001f));
            Assert.That(_player.Final.CritDamage, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(_player.Final.FireBonus, Is.EqualTo(0.15f).Within(0.0001f));
        }

        [Test]
        public void RecalculateRefreshesFinalAndCombatConsumesFinalAttackAndDefense()
        {
            EquipWoodSword();
            _player.SetTitleBonus(new StatBlock
            {
                Attack = 5f,
                Defense = 10f,
                CritDamage = 0f
            });

            DamageRequest outgoing = new DamageRequest
            {
                Source = _player.gameObject,
                BaseDamage = 100f,
                Type = DamageType.Physical,
                Element = ElementType.None,
                Multiplier = 1f,
                CanCrit = false,
                SkillId = string.Empty
            };
            Assert.That(_player.Final.Attack, Is.EqualTo(23f).Within(0.001f));
            Assert.That(
                _combat.ComputeDamage(outgoing, null).Amount,
                Is.EqualTo(123f).Within(0.001f));

            _player.FromTitle.Attack = 15f;
            _player.Recalculate();
            Assert.That(_player.Final.Attack, Is.EqualTo(33f).Within(0.001f));
            Assert.That(_player.Attack, Is.EqualTo(_player.Final.Attack));
            Assert.That(
                _combat.ComputeDamage(outgoing, null).Amount,
                Is.EqualTo(133f).Within(0.001f));

            DamageRequest incoming = outgoing;
            incoming.Source = null;
            incoming.IgnoreAttackScaling = true;
            float expectedAfterDefense = 100f * 100f / 115f;
            Assert.That(_player.Final.Defense, Is.EqualTo(15f).Within(0.001f));
            Assert.That(
                _combat.ComputeDamage(incoming, _player).Amount,
                Is.EqualTo(expectedAfterDefense).Within(0.001f));
        }

        [Test]
        public void BodyHpPercentAppliesAfterFixedSourcesAndBeforeFlatBuffs()
        {
            _player.SetTitleBonus(new StatBlock
            {
                MaxHp = 20f,
                CritDamage = 0f
            });
            _player.SetBuffBonus(new StatBlock
            {
                MaxHp = 10f,
                CritDamage = 0f
            });
            _body.AddBodyXp(1000f);
            Assert.That(_body.TryLevelUp(), Is.True);

            Assert.That(_player.MaxHp, Is.EqualTo(154f).Within(0.001f));
            Assert.That(_player.Final.MaxHp, Is.EqualTo(154f).Within(0.001f));
            Assert.That(
                _player.MaxHp,
                Is.EqualTo((100f + 20f) * 1.2f + 10f).Within(0.001f));
        }

        private void EquipWoodSword()
        {
            Assert.That(
                _inventory.AddItem(
                    InventoryContentIds.WoodSword,
                    1,
                    AcquireSource.Cheat),
                Is.True);
            int slot = FindSlot(InventoryContentIds.WoodSword);
            Assert.That(slot, Is.GreaterThanOrEqualTo(0));
            Assert.That(_equipment.EquipFromInventory(slot), Is.True);
        }

        private int FindSlot(string itemId)
        {
            for (int index = 0; index < _inventory.Slots.Count; index++)
            {
                if (string.Equals(
                        _inventory.Slots[index]?.ItemId,
                        itemId,
                        StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
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
