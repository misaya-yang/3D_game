using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.Enemy;
using Wendao.Entities.Player;
using Wendao.Systems.Combat;
using Wendao.Systems.Enemy;
using Wendao.Systems.World;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0402PlayModeTests
    {
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
            AsyncOperation load = SceneManager.LoadSceneAsync(
                SceneLoader.DefaultMapSceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;

            EnterPlayingState();
            Scene scene = SceneManager.GetActiveScene();
            QingshiGreyboxFactory.EnsureCreated(scene);
            PlayerRuntimeBootstrap.EnsureForScene(scene);
            EliteWolfRuntimeBootstrap.EnsureForScene(scene)?.SpawnAllNow();
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
        }

        [Test]
        public void EliteWolfContentSpawnAndVisualMatchSpec()
        {
            EnemyData grey = ConfigDatabase.Instance.GetEnemy(
                EnemyContentIds.GreyWolf);
            EnemyData elite = ConfigDatabase.Instance.GetEnemy(
                EnemyContentIds.EliteWolf);
            EnemySpawner spawner = FindEliteSpawner();
            EnemyBrain brain = FindEliteWolf();

            Assert.That(grey, Is.Not.Null);
            Assert.That(elite, Is.Not.Null);
            Assert.That(elite.DisplayName, Is.EqualTo("灰爪"));
            Assert.That(elite.Rank, Is.EqualTo(EnemyRank.Elite));
            Assert.That(elite.Realm, Is.EqualTo(RealmType.QiCondensation));
            Assert.That(elite.SubStage, Is.EqualTo(6));
            Assert.That(elite.MaxHp, Is.EqualTo(800f));
            Assert.That(elite.Attack, Is.EqualTo(28f));
            Assert.That(elite.MaxHp / grey.MaxHp, Is.EqualTo(10f));
            Assert.That(elite.Attack / grey.Attack, Is.EqualTo(3.5f));
            Assert.That(elite.CultivationXpReward, Is.EqualTo(120f));
            Assert.That(elite.LootTable, Has.Length.EqualTo(1));
            Assert.That(
                elite.LootTable[0].ItemId,
                Is.EqualTo(Wendao.Systems.Inventory.InventoryContentIds.BeastCore01));
            Assert.That(elite.LootTable[0].DropChance, Is.EqualTo(1f));
            Assert.That(
                elite.SkillIds,
                Is.EqualTo(new[] { EnemyContentIds.EliteWolfCharge }));

            Assert.That(spawner, Is.Not.Null);
            Assert.That(spawner.MaxAlive, Is.EqualTo(1));
            Assert.That(spawner.RespawnSeconds, Is.EqualTo(15f));
            Assert.That(spawner.AliveCount, Is.EqualTo(1));
            Assert.That(
                Vector3.Distance(
                    spawner.transform.position,
                    EliteWolfRuntimeBootstrap.SpawnPosition),
                Is.LessThan(0.01f));

            Assert.That(brain, Is.Not.Null);
            Assert.That(brain.NameLocalizationKey,
                Is.EqualTo("enemy_name_enemy_wolf_elite"));
            Assert.That(brain.CurrentHp, Is.EqualTo(800f));
            Assert.That(
                brain.transform.Find("EliteWolfVisual_Greybox"),
                Is.Not.Null);
        }

        [Test]
        public void EliteChargeClosesDistanceDealsSkillDamageAndStartsCooldown()
        {
            EnemyBrain elite = FindEliteWolfAndDisableOthers();
            PlayerController player =
                Object.FindAnyObjectByType<PlayerController>();
            PlayerStats stats = player.GetComponent<PlayerStats>();
            player.TeleportTo(
                elite.SpawnPosition + Vector3.back * 5f,
                Quaternion.identity);
            stats.SetHp(stats.MaxHp);

            float distanceBefore = HorizontalDistance(
                elite.transform.position,
                player.transform.position);
            float hpBefore = stats.CurrentHp;
            elite.OnAggro(player.gameObject);
            elite.TickAI(EnemyBrain.AlertDurationSeconds);
            Assert.That(elite.State, Is.EqualTo(EnemyBrainState.Chase));
            elite.TickAI(0.01f);
            Assert.That(elite.State, Is.EqualTo(EnemyBrainState.Skill));
            Assert.That(
                elite.ActiveSkillId,
                Is.EqualTo(EnemyContentIds.EliteWolfCharge));

            elite.TickAI(EnemyBrain.EliteChargeWindupSeconds);
            Assert.That(elite.IsCharging, Is.True);
            float nearestDistance = distanceBefore;
            for (int index = 0; index < 8; index++)
            {
                elite.TickAI(0.1f);
                nearestDistance = Mathf.Min(
                    nearestDistance,
                    HorizontalDistance(
                        elite.transform.position,
                        player.transform.position));
            }

            Assert.That(nearestDistance, Is.LessThan(distanceBefore - 1f));
            Assert.That(stats.CurrentHp, Is.LessThan(hpBefore));
            Assert.That(elite.SkillUseCount, Is.EqualTo(1));
            Assert.That(
                elite.LastSkillId,
                Is.EqualTo(EnemyContentIds.EliteWolfCharge));
            Assert.That(elite.SkillCooldownRemaining, Is.GreaterThan(5f));
            Assert.That(elite.State,
                Is.EqualTo(EnemyBrainState.Attack)
                    .Or.EqualTo(EnemyBrainState.Chase));
        }

        private static void EnterPlayingState()
        {
            GameManager manager = GameManager.Instance;
            Assert.That(manager, Is.Not.Null);
            if (manager.State == GameState.Boot)
            {
                Assert.That(manager.TrySetState(GameState.MainMenu), Is.True);
            }

            if (manager.State == GameState.MainMenu)
            {
                Assert.That(manager.TrySetState(GameState.Loading), Is.True);
            }

            if (manager.State == GameState.Loading)
            {
                Assert.That(manager.TrySetState(GameState.Playing), Is.True);
            }

            Assert.That(manager.State, Is.EqualTo(GameState.Playing));
        }

        private static EnemySpawner FindEliteSpawner()
        {
            EnemySpawner[] spawners = Object.FindObjectsByType<EnemySpawner>();
            for (int index = 0; index < spawners.Length; index++)
            {
                if (spawners[index].EnemyId == EnemyContentIds.EliteWolf)
                {
                    return spawners[index];
                }
            }

            return null;
        }

        private static EnemyBrain FindEliteWolf()
        {
            EnemyBrain[] enemies = Object.FindObjectsByType<EnemyBrain>();
            for (int index = 0; index < enemies.Length; index++)
            {
                if (enemies[index].Data != null
                    && enemies[index].Data.Id == EnemyContentIds.EliteWolf)
                {
                    return enemies[index];
                }
            }

            return null;
        }

        private static EnemyBrain FindEliteWolfAndDisableOthers()
        {
            EnemyBrain selected = FindEliteWolf();
            Assert.That(selected, Is.Not.Null);
            EnemyBrain[] enemies = Object.FindObjectsByType<EnemyBrain>();
            for (int index = 0; index < enemies.Length; index++)
            {
                if (enemies[index] != selected)
                {
                    enemies[index].enabled = false;
                }
            }

            return selected;
        }

        private static float HorizontalDistance(Vector3 left, Vector3 right)
        {
            left.y = 0f;
            right.y = 0f;
            return Vector3.Distance(left, right);
        }

        private static void DestroyRuntimeObjects()
        {
            DestroyAll<EnemyBrain>();
            DestroyAll<EnemySpawner>();
            DestroyAll<PlayerController>();
            DestroyAll<Wendao.Entities.NPC.NPCController>();
            DestroyAll<TrainingDummy>();
            DestroyAll<Wendao.CameraSystem.ThirdPersonCamera>();
            DestroyAll<Wendao.Systems.NPC.DialogueManager>();
            DestroyAll<Wendao.Systems.Quest.QuestManager>();
            DestroyAll<Wendao.Systems.Loot.LootSystem>();
            DestroyAll<Wendao.Systems.Cultivation.BodyRefinementManager>();
            DestroyAll<Wendao.Systems.Cultivation.CultivationManager>();
            DestroyAll<Wendao.Systems.Cultivation.SpiritRootSystem>();
            DestroyAll<Wendao.Systems.Equipment.RefineSystem>();
            DestroyAll<Wendao.Systems.Equipment.EquipmentManager>();
            DestroyAll<Wendao.Systems.Inventory.ItemUseSystem>();
            DestroyAll<Wendao.Systems.Inventory.InventoryManager>();
            DestroyAll<Wendao.Systems.Crafting.AlchemySystem>();
            DestroyAll<Wendao.Systems.Crafting.GatheringSystem>();
            DestroyAll<Wendao.Systems.Shop.ShopSystem>();
            DestroyAll<Wendao.Systems.Skill.SkillManager>();
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
