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
using Wendao.Entities.NPC;
using Wendao.Entities.Player;
using Wendao.Systems.Combat;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Enemy;
using Wendao.Systems.Equipment;
using Wendao.Systems.Inventory;
using Wendao.Systems.Loot;
using Wendao.Systems.NPC;
using Wendao.Systems.Quest;
using Wendao.Systems.Skill;
using Wendao.Systems.Tutorial;
using Wendao.Systems.World;
using Wendao.UI.Combat;
using Wendao.UI.Common;
using Wendao.UI.Cultivation;
using Wendao.UI.Inventory;
using Wendao.UI.NPC;
using Wendao.UI.Quest;
using Wendao.UI.SceneFlow;
using Wendao.UI.Skill;
using Wendao.UI.Tutorial;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class GVS07PlayModeTests
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
                "WendaoGVS07Tests_" + Guid.NewGuid().ToString("N"));
            SaveManager.Instance.ConfigureStorageRoot(_storageRoot);
            ServiceLocator.Get<ISpiritRootService>()
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
                Assert.That(
                    gameManager.TrySetState(GameState.MainMenu),
                    Is.True);
            }

            if (gameManager.State == GameState.MainMenu)
            {
                Assert.That(
                    gameManager.TrySetState(GameState.Loading),
                    Is.True);
            }

            if (gameManager.State == GameState.Loading)
            {
                Assert.That(
                    gameManager.TrySetState(GameState.Playing),
                    Is.True);
            }

            PlayerRuntimeBootstrap.EnsureForScene(SceneManager.GetActiveScene());
            SceneUiBootstrap.EnsureForScene(SceneManager.GetActiveScene());
            EnemySpawner spawner = WolfRuntimeBootstrap.EnsureForScene(
                SceneManager.GetActiveScene());
            Assert.That(spawner, Is.Not.Null);
            spawner.SpawnAllNow();
            UnlockHuntQuest();
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

            if (!string.IsNullOrEmpty(_storageRoot)
                && Directory.Exists(_storageRoot))
            {
                Directory.Delete(_storageRoot, true);
            }
        }

        [Test]
        public void GreyWolfContentSpawnerAndLocalizationContractMatchSpec()
        {
            EnemyData wolf = ConfigDatabase.Instance.GetEnemy(
                EnemyContentIds.GreyWolf);
            ItemData wolfHair = ConfigDatabase.Instance.GetItem(
                InventoryContentIds.WolfHair);
            EnemySpawner spawner = FindSpawner(
                WolfRuntimeBootstrap.SpawnerObjectName);

            Assert.That(wolf, Is.Not.Null);
            Assert.That(wolf.DisplayName, Is.EqualTo("灰狼"));
            Assert.That(wolf.Rank, Is.EqualTo(EnemyRank.Normal));
            Assert.That(wolf.Realm, Is.EqualTo(RealmType.QiCondensation));
            Assert.That(wolf.SubStage, Is.EqualTo(1));
            Assert.That(wolf.MaxHp, Is.EqualTo(80f));
            Assert.That(wolf.Attack, Is.EqualTo(8f));
            Assert.That(wolf.CultivationXpReward, Is.EqualTo(15f));
            Assert.That(wolf.LootTable, Has.Length.EqualTo(1));
            Assert.That(
                wolf.LootTable[0].ItemId,
                Is.EqualTo(InventoryContentIds.WolfHair));
            Assert.That(wolf.LootTable[0].MinCount, Is.EqualTo(1));
            Assert.That(wolf.LootTable[0].MaxCount, Is.EqualTo(1));
            Assert.That(wolf.LootTable[0].DropChance, Is.EqualTo(0.4f));
            Assert.That(wolf.SkillIds, Is.Empty);
            Assert.That(wolf.BossPhases, Is.Empty);

            Assert.That(wolfHair, Is.Not.Null);
            Assert.That(wolfHair.DisplayName, Is.EqualTo("狼毫"));
            Assert.That(wolfHair.Type, Is.EqualTo(ItemType.Material));
            Assert.That(wolfHair.MaxStack, Is.EqualTo(99));
            Assert.That(
                InventoryPanelView.GetItemNameLocalizationKey(wolfHair.Id),
                Is.EqualTo("item_name_item_mat_wolf_hair"));

            Assert.That(spawner, Is.Not.Null);
            Assert.That(spawner.EnemyId, Is.EqualTo(EnemyContentIds.GreyWolf));
            Assert.That(spawner.MaxAlive, Is.EqualTo(3));
            Assert.That(spawner.AliveCount, Is.EqualTo(3));
            EnemySpawner[] spawners = FindGreyWolfSpawners();
            Assert.That(
                spawners,
                Has.Length.EqualTo(WolfRuntimeBootstrap.SpawnAreaCount));
            EnemyBrain[] spawned = FindGreyWolves();
            Assert.That(
                spawned,
                Has.Length.EqualTo(WolfRuntimeBootstrap.TotalConfiguredAlive));
            Assert.That(
                Array.TrueForAll(
                    spawned,
                    brain => brain.Data == wolf
                        && !brain.IsDead
                        && brain.NameLocalizationKey
                            == "enemy_name_enemy_wolf_gray"),
                Is.True);
        }

        [Test]
        public void WolfChasesAttacksAndReturnsHomeAtFullHealth()
        {
            EnemyBrain wolf = GetFirstWolfAndDisableOthers();
            PlayerController player = Object.FindAnyObjectByType<PlayerController>();
            PlayerStats playerStats = player.GetComponent<PlayerStats>();

            Vector3 spawn = wolf.SpawnPosition;
            player.TeleportTo(
                spawn + Vector3.back * 5f,
                Quaternion.identity);
            wolf.ForceState(EnemyBrainState.Idle);
            wolf.TickAI(0.02f);
            Assert.That(wolf.State, Is.EqualTo(EnemyBrainState.Alert));
            Assert.That(wolf.IsAlertIndicatorVisible, Is.True);
            wolf.TickAI(EnemyBrain.AlertDurationSeconds * 0.5f);
            Assert.That(wolf.State, Is.EqualTo(EnemyBrainState.Alert));
            wolf.TickAI(EnemyBrain.AlertDurationSeconds * 0.5f);
            Assert.That(wolf.State, Is.EqualTo(EnemyBrainState.Chase));
            Assert.That(wolf.IsAlertIndicatorVisible, Is.False);

            float beforeDistance = HorizontalDistance(
                wolf.transform.position,
                player.transform.position);
            wolf.TickAI(0.5f);
            Assert.That(
                HorizontalDistance(
                    wolf.transform.position,
                    player.transform.position),
                Is.LessThan(beforeDistance));

            player.TeleportTo(
                wolf.transform.position + Vector3.forward,
                Quaternion.identity);
            playerStats.SetHp(playerStats.MaxHp);
            wolf.OnAggro(player.gameObject);
            Assert.That(wolf.State, Is.EqualTo(EnemyBrainState.Alert));
            wolf.TickAI(EnemyBrain.AlertDurationSeconds);
            Assert.That(wolf.State, Is.EqualTo(EnemyBrainState.Attack));
            float hpBefore = playerStats.CurrentHp;
            wolf.TickAI(0.1f);
            Assert.That(playerStats.CurrentHp, Is.LessThan(hpBefore));

            EventBus.Publish(
                CombatEvents.PlayerDied,
                new DeathInfo { Victim = player.gameObject });
            Assert.That(wolf.State, Is.EqualTo(EnemyBrainState.Return));
            wolf.OnAggro(player.gameObject);

            wolf.ApplyDamage(
                new DamageInfo
                {
                    Amount = 20f,
                    Source = player.gameObject,
                    Type = DamageType.True
                });
            Assert.That(wolf.CurrentHp, Is.LessThan(wolf.MaxHp));
            player.TeleportTo(
                spawn + Vector3.right * (wolf.Data.DisengageRange + 2f),
                Quaternion.identity);
            wolf.TickAI(0.1f);
            Assert.That(wolf.State, Is.EqualTo(EnemyBrainState.Return));
            for (int index = 0;
                index < 7 && wolf.State == EnemyBrainState.Return;
                index++)
            {
                wolf.TickAI(0.5f);
            }

            Assert.That(wolf.State, Is.EqualTo(EnemyBrainState.Idle));
            Assert.That(wolf.CurrentHp, Is.EqualTo(wolf.MaxHp));
            Assert.That(
                HorizontalDistance(wolf.transform.position, spawn),
                Is.LessThanOrEqualTo(EnemyBrain.ReturnArrivalDistance));
        }

        [Test]
        public void KillingRealWolfAdvancesQuestGrantsXpAndDropsWolfHairOnce()
        {
            EnemyBrain wolf = GetFirstWolfAndDisableOthers();
            PlayerStats player = Object.FindAnyObjectByType<PlayerStats>();
            ICombatService combat = ServiceLocator.Get<ICombatService>();
            IQuestService quests = ServiceLocator.Get<IQuestService>();
            IInventoryService inventory = ServiceLocator.Get<IInventoryService>();
            ICultivationService cultivation =
                ServiceLocator.Get<ICultivationService>();
            LootSystem loot = Object.FindAnyObjectByType<LootSystem>();
            loot.ConfigureRandomSeed(1);
            Assert.That(quests.Accept(QuestContentIds.MainHuntWolves), Is.True);
            int wolfHairBefore = inventory.CountItem(
                InventoryContentIds.WolfHair);
            int cultivationStageBefore = cultivation.SubStage;
            float cultivationXpBefore = cultivation.CurrentXp;
            float expectedCultivationGain = wolf.Data.CultivationXpReward
                * ServiceLocator.Get<ISpiritRootService>()
                    .GetCultivationMultiplier();

            var deathCalls = 0;
            var wolfHairCalls = 0;
            Action<EnemyDeathInfo> died = info => deathCalls++;
            Action<ItemAcquireInfo> acquired = info =>
            {
                if (info.ItemId == InventoryContentIds.WolfHair)
                {
                    wolfHairCalls++;
                }
            };
            EventBus.Subscribe(CombatEvents.EnemyKilled, died);
            EventBus.Subscribe(InventoryEvents.ItemAcquired, acquired);
            try
            {
                combat.DealDamage(
                    wolf,
                    new DamageRequest
                    {
                        Source = player.gameObject,
                        BaseDamage = 1000f,
                        Type = DamageType.True,
                        Multiplier = 1f,
                        CanCrit = false,
                        SkillId = string.Empty
                    });
            }
            finally
            {
                EventBus.Unsubscribe(CombatEvents.EnemyKilled, died);
                EventBus.Unsubscribe(InventoryEvents.ItemAcquired, acquired);
            }

            Assert.That(wolf.IsDead, Is.True);
            Assert.That(deathCalls, Is.EqualTo(1));
            Assert.That(wolfHairCalls, Is.EqualTo(1));
            Assert.That(
                quests.GetObjectiveProgress(
                    QuestContentIds.MainHuntWolves,
                    0),
                Is.EqualTo(1));
            Assert.That(
                inventory.CountItem(InventoryContentIds.WolfHair),
                Is.EqualTo(wolfHairBefore + 1));
            Assert.That(cultivation.SubStage, Is.EqualTo(cultivationStageBefore));
            Assert.That(
                cultivation.CurrentXp,
                Is.EqualTo(cultivationXpBefore + expectedCultivationGain)
                    .Within(0.001f));

            wolf.HandleDeath(default);
            Assert.That(
                inventory.CountItem(InventoryContentIds.WolfHair),
                Is.EqualTo(wolfHairBefore + 1));
            Assert.That(
                cultivation.CurrentXp,
                Is.EqualTo(cultivationXpBefore + expectedCultivationGain)
                    .Within(0.001f));
        }

        [UnityTest]
        public IEnumerator FullInventoryFallsBackToCollectibleWorldPickup()
        {
            IInventoryService inventory = ServiceLocator.Get<IInventoryService>();
            LootSystem loot = Object.FindAnyObjectByType<LootSystem>();
            EnemyData wolf = ConfigDatabase.Instance.GetEnemy(
                EnemyContentIds.GreyWolf);
            Assert.That(
                inventory.AddItem(
                    InventoryContentIds.WoodSword,
                    InventoryManager.Capacity,
                    AcquireSource.Cheat),
                Is.True);
            Assert.That(inventory.Slots, Has.Count.EqualTo(InventoryManager.Capacity));
            Assert.That(
                Array.TrueForAll(
                    ToArray(inventory.Slots),
                    slot => slot != null && !slot.IsEmpty),
                Is.True);

            loot.ConfigureRandomSeed(1);
            loot.DropLoot(wolf, Vector3.zero);
            WorldItemPickup pickup =
                Object.FindAnyObjectByType<WorldItemPickup>();
            Assert.That(pickup, Is.Not.Null);
            Assert.That(pickup.ItemId, Is.EqualTo(InventoryContentIds.WolfHair));
            Assert.That(pickup.Count, Is.EqualTo(1));
            Assert.That(
                pickup.NameLocalizationKey,
                Is.EqualTo("item_name_item_mat_wolf_hair"));
            Assert.That(loot.ActiveWorldPickupCount, Is.EqualTo(1));

            PlayerController player =
                Object.FindAnyObjectByType<PlayerController>();
            player.TeleportTo(Vector3.left * 5f, Quaternion.identity);
            yield return new WaitForFixedUpdate();
            Assert.That(inventory.RemoveAt(0, 1), Is.True);
            Vector3 pickupPosition = pickup.transform.position;
            pickupPosition.y = 0f;
            player.TeleportTo(pickupPosition, Quaternion.identity);
            yield return new WaitForFixedUpdate();
            Assert.That(
                inventory.CountItem(InventoryContentIds.WolfHair),
                Is.EqualTo(1));
            yield return null;
            Assert.That(pickup == null, Is.True);
        }

        [UnityTest]
        public IEnumerator SpawnerHonorsMaxAliveAndRespawnsDeadWolf()
        {
            EnemySpawner spawner = FindSpawner(
                WolfRuntimeBootstrap.SpawnerObjectName);
            EnemySpawner[] spawners = Object.FindObjectsByType<EnemySpawner>();
            for (int index = 0; index < spawners.Length; index++)
            {
                if (spawners[index] != spawner)
                {
                    spawners[index].enabled = false;
                }
            }

            spawner.enabled = false;
            spawner.Configure(
                EnemyContentIds.GreyWolf,
                1,
                0.02f,
                new[] { Vector3.zero });
            spawner.CorpseSeconds = 0.01f;
            yield return null;
            spawner.SpawnAllNow();
            EnemyBrain original = spawner.GetComponentInChildren<EnemyBrain>();
            Assert.That(original, Is.Not.Null);
            EntityId originalId = original.GetEntityId();
            PlayerStats player = Object.FindAnyObjectByType<PlayerStats>();
            ServiceLocator.Get<ICombatService>().DealDamage(
                original,
                new DamageRequest
                {
                    Source = player.gameObject,
                    BaseDamage = 1000f,
                    Type = DamageType.True,
                    Multiplier = 1f
                });
            Assert.That(original.IsDead, Is.True);

            spawner.enabled = true;
            yield return new WaitForSeconds(0.12f);
            Assert.That(spawner.AliveCount, Is.EqualTo(1));
            Assert.That(spawner.SpawnedCount, Is.EqualTo(1));
            EnemyBrain replacement = spawner.GetComponentInChildren<EnemyBrain>();
            Assert.That(replacement, Is.Not.Null);
            Assert.That(replacement.IsDead, Is.False);
            Assert.That(replacement.CurrentHp, Is.EqualTo(replacement.MaxHp));
            Assert.That(replacement.GetEntityId(), Is.Not.EqualTo(originalId));
        }

        private static EnemyBrain GetFirstWolfAndDisableOthers()
        {
            EnemyBrain[] wolves = FindGreyWolves();
            Assert.That(wolves, Is.Not.Empty);
            EnemyBrain selected = wolves[0];
            for (int index = 1; index < wolves.Length; index++)
            {
                wolves[index].enabled = false;
            }

            return selected;
        }

        private static EnemyBrain[] FindGreyWolves()
        {
            return Array.FindAll(
                Object.FindObjectsByType<EnemyBrain>(),
                brain => brain != null
                    && brain.Data != null
                    && brain.Data.Id == EnemyContentIds.GreyWolf);
        }

        private static EnemySpawner[] FindGreyWolfSpawners()
        {
            return Array.FindAll(
                Object.FindObjectsByType<EnemySpawner>(),
                spawner => spawner != null
                    && spawner.EnemyId == EnemyContentIds.GreyWolf);
        }

        private static EnemySpawner FindSpawner(string objectName)
        {
            EnemySpawner[] spawners = Object.FindObjectsByType<EnemySpawner>(FindObjectsInactive.Include);
            for (int index = 0; index < spawners.Length; index++)
            {
                if (spawners[index] != null
                    && spawners[index].gameObject.name == objectName)
                {
                    return spawners[index];
                }
            }

            return null;
        }

        private static InventorySlot[] ToArray(
            System.Collections.Generic.IReadOnlyList<InventorySlot> slots)
        {
            var result = new InventorySlot[slots.Count];
            for (int index = 0; index < slots.Count; index++)
            {
                result[index] = slots[index];
            }

            return result;
        }

        private static float HorizontalDistance(Vector3 left, Vector3 right)
        {
            left.y = 0f;
            right.y = 0f;
            return Vector3.Distance(left, right);
        }

        private static void UnlockHuntQuest()
        {
            IQuestService quests = ServiceLocator.Get<IQuestService>();
            Assert.That(
                quests.Accept(QuestContentIds.MainRootAwakening),
                Is.True);
            quests.NotifyTalk(QuestContentIds.YaoLaoNpc);
            Assert.That(
                quests.TurnIn(QuestContentIds.MainRootAwakening),
                Is.True);
        }

        private static void DestroyRuntimeObjects()
        {
            DestroyAll<WorldItemPickup>();
            DestroyAll<EnemyBrain>();
            DestroyAll<EnemySpawner>();
            DestroyAll<PlayerController>();
            DestroyAll<NPCController>();
            DestroyAll<TrainingDummy>();
            DestroyAll<ThirdPersonCamera>();
            DestroyAll<DialogueView>();
            DestroyAll<QuestTrackerView>();
            DestroyAll<SpiritRootSelectionView>();
            DestroyAll<CultivationHudView>();
            DestroyAll<SkillQuickbarView>();
            DestroyAll<InventoryPanelView>();
            DestroyAll<GameToastView>();
            DestroyAll<DamageFloatingTextView>();
            DestroyAll<TutorialToastView>();
            DestroyAll<TutorialManager>();
            DestroyAll<SkillProjectile>();
            DestroyAll<SkillManager>();
            DestroyAll<DialogueManager>();
            DestroyAll<QuestManager>();
            DestroyAll<LootSystem>();
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
