using System;
using System.Collections;
using System.Collections.Generic;
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
    public sealed class G0501PlayModeTests
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
            WolfRuntimeBootstrap.EnsureForScene(scene)?.SpawnAllNow();
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
        public void QingshiHasFiveDistinctLocalizedAreasAndTwoChests()
        {
            WorldAreaMarker[] markers = Object.FindObjectsByType<WorldAreaMarker>();
            Assert.That(markers,
                Has.Length.EqualTo(QingshiGreyboxFactory.RequiredAreaCount));
            var areaIds = new HashSet<string>(StringComparer.Ordinal);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (WorldAreaMarker marker in markers)
            {
                Assert.That(areaIds.Add(marker.AreaId), Is.True, marker.AreaId);
                Assert.That(keys.Add(marker.NameLocalizationKey),
                    Is.True,
                    marker.NameLocalizationKey);
                Assert.That(marker.DefaultName, Is.Not.Empty);
                Assert.That(marker.Footprint.x, Is.GreaterThanOrEqualTo(5f));
                Assert.That(marker.transform.Find(WorldAreaMarker.PlateName),
                    Is.Not.Null);
                Assert.That(marker.transform.Find(WorldAreaMarker.LabelName),
                    Is.Not.Null);
            }

            Assert.That(areaIds, Is.EquivalentTo(new[]
            {
                "area_qingshi_town",
                "area_qingshi_training_ground",
                "area_qingshi_east_wilderness",
                "area_qingshi_herb_creek",
                "area_qingshi_secret_path"
            }));
            Assert.That(GameObject.Find(
                QingshiGreyboxFactory.ChestWildernessId), Is.Not.Null);
            Assert.That(GameObject.Find(
                QingshiGreyboxFactory.ChestSecretPathId), Is.Not.Null);
        }

        [Test]
        public void TownAndTrainingGroundAreSafeButWildernessIsNot()
        {
            ISafeZoneService safeZones =
                ServiceLocator.Get<ISafeZoneService>();
            SafeZoneSystem system = Object.FindAnyObjectByType<SafeZoneSystem>();
            SafeZone zone = Object.FindAnyObjectByType<SafeZone>();

            Assert.That(system, Is.Not.Null);
            Assert.That(system.ActiveZoneCount, Is.EqualTo(1));
            Assert.That(zone.Id, Is.EqualTo(QingshiGreyboxFactory.SafeZoneId));
            Assert.That(safeZones.IsPositionSafe(
                new Vector3(0f, 0f, -9f)), Is.True);
            Assert.That(safeZones.IsPositionSafe(
                new Vector3(0f, 0f, -3f)), Is.True);
            Assert.That(safeZones.IsPositionSafe(
                new Vector3(8f, 0f, 4f)), Is.False);
            Assert.That(safeZones.GetRecoveryMultiplier(
                new Vector3(0f, 0f, -3f)), Is.EqualTo(2f));
            Assert.That(safeZones.GetRecoveryMultiplier(
                new Vector3(8f, 0f, 4f)), Is.EqualTo(1f));
        }

        [Test]
        public void EnemyCannotAggroSafePlayerAndReturnsWhenPlayerEntersZone()
        {
            EnemyBrain wolf = FindWolfAndDisableOthers();
            PlayerController player =
                Object.FindAnyObjectByType<PlayerController>();

            player.TeleportTo(new Vector3(0f, 0f, -3f), Quaternion.identity);
            wolf.ForceState(EnemyBrainState.Idle);
            wolf.OnAggro(player.gameObject);
            Assert.That(wolf.Target, Is.Null);
            Assert.That(wolf.State, Is.EqualTo(EnemyBrainState.Idle));

            player.TeleportTo(new Vector3(8f, 0f, 4f), Quaternion.identity);
            wolf.OnAggro(player.gameObject);
            Assert.That(wolf.Target, Is.EqualTo(player.gameObject));
            Assert.That(wolf.State, Is.EqualTo(EnemyBrainState.Alert));

            player.TeleportTo(new Vector3(0f, 0f, -3f), Quaternion.identity);
            wolf.TickAI(0.1f);
            Assert.That(wolf.Target, Is.Null);
            Assert.That(wolf.State, Is.EqualTo(EnemyBrainState.Return));
        }

        [Test]
        public void SafeZoneBlocksEnemyDamageAndDoublesRecoveryRate()
        {
            EnemyBrain wolf = FindWolfAndDisableOthers();
            PlayerController player =
                Object.FindAnyObjectByType<PlayerController>();
            PlayerStats stats = player.GetComponent<PlayerStats>();
            stats.ConfigureBaseStats(1000f, 20f, 0f);
            GameManager.Instance.SetCombatFlag(false);

            stats.SetHp(500f);
            player.TeleportTo(new Vector3(8f, 0f, 4f), Quaternion.identity);
            stats.TickRecovery(1f);
            float wildernessGain = stats.CurrentHp - 500f;

            stats.SetHp(500f);
            player.TeleportTo(new Vector3(0f, 0f, -3f), Quaternion.identity);
            stats.TickRecovery(1f);
            float safeGain = stats.CurrentHp - 500f;
            Assert.That(wildernessGain, Is.GreaterThan(0f));
            Assert.That(safeGain,
                Is.EqualTo(wildernessGain * 2f).Within(0.01f));

            stats.SetHp(500f);
            ServiceLocator.Get<ICombatService>().DealDamage(
                stats,
                new DamageRequest
                {
                    Source = wolf.gameObject,
                    BaseDamage = 100f,
                    Type = DamageType.True,
                    Multiplier = 1f,
                    CanCrit = false
                });
            Assert.That(stats.CurrentHp, Is.EqualTo(500f));

            player.TeleportTo(new Vector3(8f, 0f, 4f), Quaternion.identity);
            ServiceLocator.Get<ICombatService>().DealDamage(
                stats,
                new DamageRequest
                {
                    Source = wolf.gameObject,
                    BaseDamage = 100f,
                    Type = DamageType.True,
                    Multiplier = 1f,
                    CanCrit = false
                });
            Assert.That(stats.CurrentHp, Is.LessThan(500f));
        }

        private static EnemyBrain FindWolfAndDisableOthers()
        {
            EnemyBrain[] enemies = Object.FindObjectsByType<EnemyBrain>();
            EnemyBrain wolf = Array.Find(
                enemies,
                enemy => enemy != null
                    && enemy.Data != null
                    && enemy.Data.Id == EnemyContentIds.GreyWolf);
            Assert.That(wolf, Is.Not.Null);
            foreach (EnemyBrain enemy in enemies)
            {
                if (enemy != wolf)
                {
                    enemy.enabled = false;
                }
            }

            return wolf;
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
        }

        private static void DestroyRuntimeObjects()
        {
            DestroyAll<BlackwindDungeonSystem>();
            DestroyAll<MapTravelSystem>();
            DestroyAll<Wendao.UI.Combat.DeathView>();
            DestroyAll<SafeZoneSystem>();
            DestroyAll<BossArenaController>();
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
            T[] instances = Object.FindObjectsByType<T>(
                FindObjectsInactive.Include);
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
