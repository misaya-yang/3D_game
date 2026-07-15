using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.Player;
using Wendao.Systems.Combat;
using Wendao.Systems.Crafting;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Input;
using Wendao.Systems.Inventory;
using Wendao.Systems.World;
using Wendao.UI.Common;
using Wendao.UI.SceneFlow;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0304PlayModeTests
    {
        private string _storageRoot;
        private SaveManager _save;
        private GatheringSystem _gathering;
        private IInventoryService _inventory;
        private PlayerController _player;
        private GatherableObject[] _nodes;

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
                "WendaoG0304Tests_" + Guid.NewGuid().ToString("N"));
            _save = SaveManager.Instance;
            _save.ConfigureStorageRoot(_storageRoot);
            ServiceLocator.Get<ISpiritRootService>()
                .ChooseRoot(SpiritRootType.Fire);
            Assert.That(_save.SaveGame(0), Is.True, _save.LastError);

            AsyncOperation load = SceneManager.LoadSceneAsync(
                SceneLoader.DefaultMapSceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;

            EnterPlayingState();
            _player = PlayerRuntimeBootstrap.EnsureForScene(
                SceneManager.GetActiveScene());
            SceneUiBootstrap.EnsureForScene(SceneManager.GetActiveScene());
            yield return null;

            _gathering = Object.FindAnyObjectByType<GatheringSystem>();
            _inventory = ServiceLocator.Get<IInventoryService>();
            _nodes = Object.FindObjectsByType<GatherableObject>(FindObjectsInactive.Include);
            Assert.That(_gathering, Is.Not.Null);
            Assert.That(_inventory, Is.Not.Null);
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

            if (!string.IsNullOrEmpty(_storageRoot)
                && Directory.Exists(_storageRoot))
            {
                Directory.Delete(_storageRoot, true);
            }
        }

        [Test]
        public void QingshiHerbCreekContainsSixStableGatheringNodes()
        {
            Assert.That(
                _nodes,
                Has.Length.GreaterThanOrEqualTo(
                    QingshiGreyboxFactory.RequiredGatherableCount));
            var nodeIds = new HashSet<string>(StringComparer.Ordinal);
            var qingxinCount = 0;
            var spiritDustCount = 0;
            foreach (GatherableObject node in _nodes)
            {
                Assert.That(nodeIds.Add(node.NodeId), Is.True, node.NodeId);
                Assert.That(node.RequiredLevel, Is.EqualTo(1));
                Assert.That(node.MinCount, Is.EqualTo(1));
                Assert.That(node.MaxCount, Is.EqualTo(2));
                Assert.That(node.IsAvailable, Is.True);
                if (node.ItemId == InventoryContentIds.QingxinGrass)
                {
                    qingxinCount++;
                    Assert.That(node.RespawnSeconds, Is.EqualTo(30f));
                }
                else if (node.ItemId == InventoryContentIds.SpiritDust)
                {
                    spiritDustCount++;
                    Assert.That(node.RespawnSeconds, Is.EqualTo(45f));
                }
                else
                {
                    Assert.Fail("Unexpected gathering item: " + node.ItemId);
                }
            }

            Assert.That(nodeIds, Is.SupersetOf(GatheringContentIds.QingshiNodes));
            Assert.That(qingxinCount, Is.EqualTo(4));
            Assert.That(spiritDustCount, Is.EqualTo(2));
        }

        [Test]
        public void OnePointFiveSecondReadAwardsGatherLootAndPersistsInventory()
        {
            GatherableObject node = FindNode(GatheringContentIds.QingshiQingxin01);
            MovePlayerNear(node);
            _gathering.SetCountProvider((_, maximumExclusive) =>
                maximumExclusive - 1);
            ItemAcquireInfo observed = default;
            var acquireCalls = 0;
            Action<ItemAcquireInfo> handler = info =>
            {
                if (info.Source == AcquireSource.Gather)
                {
                    observed = info;
                    acquireCalls++;
                }
            };
            EventBus.Subscribe(InventoryEvents.ItemAcquired, handler);
            try
            {
                Assert.That(node.TryInteract(), Is.True);
                Assert.That(_gathering.IsGathering, Is.True);
                Assert.That(
                    ServiceLocator.Get<IPlayerInputSource>().IsEnabled,
                    Is.False);
                _gathering.TickGathering(1.49f);
                Assert.That(acquireCalls, Is.Zero);
                Assert.That(_gathering.Progress01, Is.GreaterThan(0.99f));
                _gathering.TickGathering(0.01f);
            }
            finally
            {
                EventBus.Unsubscribe(InventoryEvents.ItemAcquired, handler);
            }

            Assert.That(acquireCalls, Is.EqualTo(1));
            Assert.That(observed.ItemId, Is.EqualTo(InventoryContentIds.QingxinGrass));
            Assert.That(observed.Count, Is.EqualTo(2));
            Assert.That(observed.Source, Is.EqualTo(AcquireSource.Gather));
            Assert.That(
                _inventory.CountItem(InventoryContentIds.QingxinGrass),
                Is.EqualTo(2));
            Assert.That(_gathering.IsGathering, Is.False);
            Assert.That(node.IsAvailable, Is.False);
            Assert.That(node.RespawnRemaining, Is.EqualTo(30f));
            Assert.That(
                ServiceLocator.Get<IPlayerInputSource>().IsEnabled,
                Is.True);

            string inventoryJson = File.ReadAllText(Path.Combine(
                _storageRoot,
                "SaveSlot_0",
                InventoryManager.SaveModuleName + ".json"));
            StringAssert.Contains(InventoryContentIds.QingxinGrass, inventoryJson);
        }

        [Test]
        public void PlayerDamageInterruptsReadWithoutLootOrCooldown()
        {
            GatherableObject node = FindNode(GatheringContentIds.QingshiQingxin02);
            MovePlayerNear(node);
            GameToastView toast = Object.FindAnyObjectByType<GameToastView>();
            Assert.That(toast, Is.Not.Null);
            Assert.That(node.TryInteract(), Is.True);
            _gathering.TickGathering(0.6f);

            EventBus.Publish(
                CombatEvents.PlayerDamaged,
                new DamageInfo
                {
                    Target = _player.gameObject,
                    Amount = 1f,
                    Type = DamageType.Physical
                });

            Assert.That(_gathering.IsGathering, Is.False);
            Assert.That(node.IsReserved, Is.False);
            Assert.That(node.IsAvailable, Is.True);
            Assert.That(node.RespawnRemaining, Is.Zero);
            Assert.That(
                _inventory.CountItem(InventoryContentIds.QingxinGrass),
                Is.Zero);
            Assert.That(
                ServiceLocator.Get<IPlayerInputSource>().IsEnabled,
                Is.True);
            Assert.That(
                toast.CurrentLocalizationKey,
                Is.EqualTo(GatheringSystem.InterruptedToastKey));
        }

        [Test]
        public void DepletedNodeReturnsOnlyAfterItsRespawnTime()
        {
            GatherableObject node = FindNode(
                GatheringContentIds.QingshiSpiritDust01);
            node.Configure(
                node.NodeId,
                node.ItemId,
                1,
                1,
                1,
                0.25f);
            MovePlayerNear(node);
            _gathering.SetCountProvider((minimum, _) => minimum);
            Assert.That(node.TryInteract(), Is.True);
            _gathering.TickGathering(GatheringSystem.GatherDurationSeconds);
            Assert.That(node.IsAvailable, Is.False);
            Assert.That(node.RespawnRemaining, Is.EqualTo(0.25f));

            node.TickRespawn(0.24f);
            Assert.That(node.IsAvailable, Is.False);
            Assert.That(node.RespawnRemaining, Is.EqualTo(0.01f).Within(0.0001f));
            node.TickRespawn(0.01f);
            Assert.That(node.IsAvailable, Is.True);
            Assert.That(node.RespawnRemaining, Is.Zero);
            Assert.That(node.TryInteract(), Is.True);
            Assert.That(_gathering.CancelActiveGather(), Is.True);
        }

        private GatherableObject FindNode(string nodeId)
        {
            foreach (GatherableObject node in _nodes)
            {
                if (string.Equals(node.NodeId, nodeId, StringComparison.Ordinal))
                {
                    return node;
                }
            }

            Assert.Fail("Missing gathering node: " + nodeId);
            return null;
        }

        private void MovePlayerNear(GatherableObject node)
        {
            _player.TeleportTo(
                node.transform.position + new Vector3(0f, 0f, -1f),
                Quaternion.identity);
            Physics.SyncTransforms();
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
            DestroyAll<GatheringSystem>();
            DestroyAll<Wendao.Systems.Crafting.AlchemySystem>();
            DestroyAll<Wendao.Systems.Shop.ShopSystem>();
            DestroyAll<Wendao.Entities.Player.PlayerController>();
            DestroyAll<Wendao.Systems.Skill.SkillManager>();
            DestroyAll<Wendao.Systems.Equipment.RefineSystem>();
            DestroyAll<Wendao.Systems.Cultivation.BodyRefinementManager>();
            DestroyAll<Wendao.Systems.Cultivation.CultivationManager>();
            DestroyAll<Wendao.Systems.Cultivation.SpiritRootSystem>();
            DestroyAll<Wendao.Systems.Equipment.EquipmentManager>();
            DestroyAll<ItemUseSystem>();
            DestroyAll<InventoryManager>();
            DestroyAll<Wendao.Systems.Combat.StatusEffectManager>();
            DestroyAll<Wendao.Systems.Combat.CombatSystem>();
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
