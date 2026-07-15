using System;
using System.Collections;
using System.IO;
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
using Wendao.UI.Common;
using Wendao.UI.SceneFlow;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0503PlayModeTests
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
                "WendaoG0503Tests_" + Guid.NewGuid().ToString("N"));
            SaveManager.Instance.ConfigureStorageRoot(_storageRoot);

            AsyncOperation load = SceneManager.LoadSceneAsync(
                SceneLoader.CangwuMapSceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            EnterPlayingState();
            CangwuGreyboxFactory.EnsureCreated(SceneManager.GetActiveScene());
            PlayerRuntimeBootstrap.EnsureForScene(SceneManager.GetActiveScene());
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
            if (!string.IsNullOrEmpty(_storageRoot)
                && Directory.Exists(_storageRoot))
            {
                Directory.Delete(_storageRoot, true);
            }
        }

        [UnityTest]
        public IEnumerator GoldenCoreGateBlocksFoundationAndLoadsB1ForGoldenCore()
        {
            SaveManager save = SaveManager.Instance;
            PlayerController player = Object.FindAnyObjectByType<PlayerController>();
            BlackwindDungeonGate gate =
                Object.FindAnyObjectByType<BlackwindDungeonGate>();
            GameToastView toast = Object.FindAnyObjectByType<GameToastView>();
            Assert.That(gate, Is.Not.Null);
            Assert.That(toast, Is.Not.Null);

            save.Profile.Realm = (int)RealmType.Foundation;
            Assert.That(gate.TryEnter(player.gameObject), Is.False);
            Assert.That(toast.CurrentLocalizationKey,
                Is.EqualTo(BlackwindDungeonGate.LockedLocalizationKey));
            Assert.That(toast.IsVisible, Is.True);
            Assert.That(SceneManager.GetActiveScene().name,
                Is.EqualTo(SceneLoader.CangwuMapSceneName));

            save.Profile.Realm = (int)RealmType.GoldenCore;
            Assert.That(gate.TryEnter(player.gameObject), Is.True);
            float deadline = Time.realtimeSinceStartup + 15f;
            while ((SceneLoader.Instance.IsLoading
                    || SceneManager.GetActiveScene().name
                        != SceneLoader.BlackwindDungeonSceneName)
                && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(SceneManager.GetActiveScene().name,
                Is.EqualTo(SceneLoader.BlackwindDungeonSceneName));
            Assert.That(SceneLoader.Instance.LastLoadedMapId,
                Is.EqualTo(MapContentIds.BlackwindMap));
            Assert.That(ServiceLocator.Get<IBlackwindDungeonService>().CurrentFloor,
                Is.EqualTo(1));
            Assert.That(Vector3.Distance(
                Object.FindAnyObjectByType<PlayerController>().transform.position,
                GameObject.Find(BlackwindDungeonFactory.GetSpawnName(1))
                    .transform.position), Is.LessThan(0.1f));
        }

        [UnityTest]
        public IEnumerator FiveFloorRuntimeFlowSpawnsMechanicsAndDefeatsBoss()
        {
            yield return LoadDungeon(0);
            IBlackwindDungeonService dungeon =
                ServiceLocator.Get<IBlackwindDungeonService>();
            BlackwindDungeonEncounterController encounters =
                Object.FindAnyObjectByType<BlackwindDungeonEncounterController>();
            PlayerController player = Object.FindAnyObjectByType<PlayerController>();

            WorldAreaMarker[] areas = Object.FindObjectsByType<WorldAreaMarker>();
            Assert.That(areas, Has.Length.EqualTo(BlackwindDungeonFactory.FloorCount));
            Assert.That(encounters.GetSpawnedCount(1),
                Is.EqualTo(BlackwindDungeonEncounterController.FirstFloorWaveSize));

            KillAllAlive(EnemyContentIds.BlackwindSpawn);
            Assert.That(encounters.ActiveWave, Is.EqualTo(2));
            Assert.That(encounters.GetSpawnedCount(1),
                Is.EqualTo(BlackwindDungeonEncounterController.FirstFloorWaveSize));
            KillAllAlive(EnemyContentIds.BlackwindSpawn);
            Assert.That(dungeon.IsFloorComplete(1), Is.False,
                "B1 also requires its pressure plate.");
            Assert.That(Object.FindAnyObjectByType<BlackwindPressurePlate>()
                .TryActivate(player.gameObject), Is.True);
            Assert.That(dungeon.IsFloorComplete(1), Is.True);
            AssertDoorOpen(1);

            Assert.That(dungeon.EnterFloor(2), Is.True);
            Assert.That(encounters.GetSpawnedCount(2), Is.EqualTo(1));
            KillAllAlive(EnemyContentIds.EliteWolf);
            Assert.That(dungeon.IsFloorComplete(2), Is.True);
            AssertDoorOpen(2);

            Assert.That(dungeon.EnterFloor(3), Is.True);
            Assert.That(GameObject.Find(BlackwindDungeonFactory.BranchChestName),
                Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<BlackwindHazard>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<BlackwindFloorCompletionTrigger>()
                .TryComplete(player.gameObject), Is.True);
            Assert.That(dungeon.IsFloorComplete(3), Is.True);
            AssertDoorOpen(3);

            Assert.That(dungeon.EnterFloor(4), Is.True);
            Assert.That(encounters.GetSpawnedCount(4),
                Is.EqualTo(BlackwindDungeonEncounterController.FourthFloorEnemyCount));
            Assert.That(Object.FindAnyObjectByType<BlackwindHealingSpring>(),
                Is.Not.Null);
            KillAllAlive(EnemyContentIds.BlackwindSpawn);
            Assert.That(dungeon.IsFloorComplete(4), Is.True);
            AssertDoorOpen(4);

            Assert.That(dungeon.EnterFloor(5), Is.True);
            EnemyBrain boss = FindAliveEnemy(EnemyContentIds.StoneGeneral);
            Assert.That(boss, Is.Not.Null);
            Assert.That(boss.Data.BossPhases, Has.Length.EqualTo(3));
            Kill(boss);
            Assert.That(dungeon.IsRunCompleted, Is.True);
            Assert.That(dungeon.Checkpoint, Is.EqualTo(4));
            Assert.That(dungeon.IsFloorComplete(5), Is.True);
            Assert.That(Object.FindAnyObjectByType<BlackwindReturnGate>(),
                Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator CompletingB2PersistsTwoAndReentryStartsAtB3()
        {
            yield return LoadDungeon(0);
            IBlackwindDungeonService dungeon =
                ServiceLocator.Get<IBlackwindDungeonService>();
            Assert.That(dungeon.NotifyCombatObjectiveCleared(1), Is.True);
            Assert.That(dungeon.ActivatePressurePlate(), Is.True);
            Assert.That(dungeon.EnterFloor(2), Is.True);
            Assert.That(dungeon.NotifyCombatObjectiveCleared(2), Is.True);
            Assert.That(SaveManager.Instance.World.DungeonCheckpoint[
                MapContentIds.BlackwindMap], Is.EqualTo(2));
            Assert.That(SaveManager.Instance.SaveGame(0),
                Is.True,
                SaveManager.Instance.LastError);

            dungeon.EndRun();
            dungeon.BeginRun();
            Assert.That(dungeon.CurrentFloor, Is.EqualTo(3));
            Assert.That(dungeon.IsFloorComplete(1), Is.True);
            Assert.That(dungeon.IsFloorComplete(2), Is.True);
            Assert.That(dungeon.IsFloorComplete(3), Is.False);

            SaveManager.Instance.World.DungeonCheckpoint[
                MapContentIds.BlackwindMap] = 0;
            Assert.That(SaveManager.Instance.LoadGame(0),
                Is.True,
                SaveManager.Instance.LastError);
            dungeon.EndRun();
            dungeon.BeginRun();
            Assert.That(dungeon.Checkpoint, Is.EqualTo(2));
            Assert.That(dungeon.CurrentFloor, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator B5FailureKeepsCheckpointAndSpringResetsPerRun()
        {
            yield return LoadDungeon(3);
            IBlackwindDungeonService dungeon =
                ServiceLocator.Get<IBlackwindDungeonService>();
            PlayerStats stats = Object.FindAnyObjectByType<PlayerStats>();
            stats.ConfigureBaseStats(1000f, 10f, 0f);
            stats.SetHp(100f);
            BlackwindHealingSpring spring =
                Object.FindAnyObjectByType<BlackwindHealingSpring>();

            Assert.That(dungeon.CurrentFloor, Is.EqualTo(4));
            Assert.That(spring.TryDrink(stats.gameObject), Is.True);
            Assert.That(stats.CurrentHp, Is.EqualTo(600f));
            Assert.That(spring.TryDrink(stats.gameObject), Is.False);
            Assert.That(dungeon.IsHealingSpringUsed, Is.True);

            Assert.That(dungeon.NotifyCombatObjectiveCleared(4), Is.True);
            Assert.That(dungeon.Checkpoint, Is.EqualTo(4));
            Assert.That(dungeon.EnterFloor(5), Is.True);
            EventBus.Publish(
                CombatEvents.PlayerDied,
                new DeathInfo { Victim = stats.gameObject });

            Assert.That(dungeon.Checkpoint, Is.EqualTo(4));
            Assert.That(dungeon.CurrentFloor, Is.EqualTo(5));
            Assert.That(dungeon.IsRunCompleted, Is.False);
            Assert.That(dungeon.IsFloorComplete(5), Is.False);
            Assert.That(dungeon.IsHealingSpringUsed, Is.False);

            dungeon.EndRun();
            dungeon.BeginRun();
            Assert.That(dungeon.CurrentFloor, Is.EqualTo(5));
            Assert.That(dungeon.IsHealingSpringUsed, Is.False);
        }

        private static IEnumerator LoadDungeon(int checkpoint)
        {
            SaveManager.Instance.World.DungeonCheckpoint[
                MapContentIds.BlackwindMap] = checkpoint;
            AsyncOperation load = SceneManager.LoadSceneAsync(
                SceneLoader.BlackwindDungeonSceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            BlackwindDungeonFactory.EnsureCreated(SceneManager.GetActiveScene());
            PlayerRuntimeBootstrap.EnsureForScene(SceneManager.GetActiveScene());
            BlackwindDungeonRuntimeBootstrap.EnsureForScene(
                SceneManager.GetActiveScene());
            SceneUiBootstrap.EnsureForScene(SceneManager.GetActiveScene());
            yield return null;
        }

        private static void KillAllAlive(string enemyId)
        {
            EnemyBrain[] enemies = Object.FindObjectsByType<EnemyBrain>(FindObjectsInactive.Exclude);
            foreach (EnemyBrain enemy in enemies)
            {
                if (enemy != null
                    && !enemy.IsDead
                    && enemy.Data?.Id == enemyId
                    && enemy.gameObject.scene == SceneManager.GetActiveScene())
                {
                    Kill(enemy);
                }
            }
        }

        private static EnemyBrain FindAliveEnemy(string enemyId)
        {
            EnemyBrain[] enemies = Object.FindObjectsByType<EnemyBrain>(FindObjectsInactive.Exclude);
            foreach (EnemyBrain enemy in enemies)
            {
                if (enemy != null
                    && !enemy.IsDead
                    && enemy.Data?.Id == enemyId
                    && enemy.gameObject.scene == SceneManager.GetActiveScene())
                {
                    return enemy;
                }
            }

            return null;
        }

        private static void Kill(EnemyBrain enemy)
        {
            var killingBlow = new DamageInfo
            {
                Source = Object.FindAnyObjectByType<PlayerController>()?.gameObject,
                Amount = enemy.MaxHp + 1f,
                Type = DamageType.True
            };
            enemy.ApplyDamage(killingBlow);
            enemy.HandleDeath(killingBlow);
        }

        private static void AssertDoorOpen(int floor)
        {
            BlackwindDungeonDoor[] doors =
                Object.FindObjectsByType<BlackwindDungeonDoor>();
            BlackwindDungeonDoor door = Array.Find(
                doors,
                candidate => candidate.RequiredFloor == floor);
            Assert.That(door, Is.Not.Null);
            door.Refresh();
            Assert.That(door.IsOpen, Is.True);
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
            DestroyAll<Wendao.Systems.World.SafeZoneSystem>();
            DestroyAll<BossArenaController>();
            DestroyAll<EnemyBrain>();
            DestroyAll<EnemySpawner>();
            DestroyAll<PlayerController>();
            DestroyAll<Wendao.Entities.NPC.NPCController>();
            DestroyAll<Wendao.Entities.Enemy.TrainingDummy>();
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
