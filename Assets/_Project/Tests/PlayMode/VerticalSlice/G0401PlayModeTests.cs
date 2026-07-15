using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.Enemy;
using Wendao.Entities.Player;
using Wendao.Systems.Combat;
using Wendao.Systems.World;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0401PlayModeTests
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
            EnemySpawner primary = WolfRuntimeBootstrap.EnsureForScene(scene);
            Assert.That(primary, Is.Not.Null);
            EnemySpawner[] spawners = Object.FindObjectsByType<EnemySpawner>();
            for (int index = 0; index < spawners.Length; index++)
            {
                spawners[index].SpawnAllNow();
            }

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
        public void RuntimeNavMeshAndThreeSpawnAreasAreReady()
        {
            QingshiNavigationSurface surface =
                Object.FindAnyObjectByType<QingshiNavigationSurface>();
            EnemySpawner[] spawners = FindGreyWolfSpawners();
            EnemyBrain[] wolves = FindGreyWolves();

            Assert.That(surface, Is.Not.Null);
            Assert.That(surface.IsBuilt, Is.True);
            Assert.That(surface.BuildSourceCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(
                spawners,
                Has.Length.EqualTo(WolfRuntimeBootstrap.SpawnAreaCount));
            Assert.That(
                wolves,
                Has.Length.EqualTo(WolfRuntimeBootstrap.TotalConfiguredAlive));
            Assert.That(SumMaxAlive(spawners), Is.EqualTo(7));
            Assert.That(
                System.Array.TrueForAll(
                    wolves,
                    wolf => wolf.IsOnNavMesh
                        && wolf.GetComponent<NavMeshAgent>() != null
                        && wolf.PatrolPointCount >= 4),
                Is.True);
        }

        [Test]
        public void WolfPatrolsThenShowsPointFourSecondAlertBeforeChasing()
        {
            EnemyBrain wolf = GetFirstWolfAndDisableOthers();
            PlayerController player =
                Object.FindAnyObjectByType<PlayerController>();
            player.TeleportTo(new Vector3(-12f, 0f, -12f), Quaternion.identity);

            wolf.ForceState(EnemyBrainState.Idle);
            wolf.TickAI(EnemyBrain.IdleBeforePatrolSeconds + 0.01f);
            Assert.That(wolf.State, Is.EqualTo(EnemyBrainState.Patrol));
            Vector3 beforePatrol = wolf.transform.position;
            wolf.TickAI(0.25f);
            Assert.That(
                HorizontalDistance(wolf.transform.position, beforePatrol),
                Is.GreaterThan(0.1f));

            player.TeleportTo(
                wolf.transform.position + Vector3.back * 5f,
                Quaternion.identity);
            wolf.TickAI(0.01f);
            Assert.That(wolf.State, Is.EqualTo(EnemyBrainState.Alert));
            Assert.That(wolf.IsAlertIndicatorVisible, Is.True);
            wolf.TickAI(EnemyBrain.AlertDurationSeconds - 0.02f);
            Assert.That(wolf.State, Is.EqualTo(EnemyBrainState.Alert));
            wolf.TickAI(0.02f);
            Assert.That(wolf.State, Is.EqualTo(EnemyBrainState.Chase));
            Assert.That(wolf.IsAlertIndicatorVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator WolfUsesCompleteNavMeshPathAroundSolidObstacle()
        {
            EnemySpawner[] spawners = Object.FindObjectsByType<EnemySpawner>();
            for (int index = 0; index < spawners.Length; index++)
            {
                spawners[index].enabled = false;
            }

            EnemyBrain[] existing = Object.FindObjectsByType<EnemyBrain>();
            for (int index = 0; index < existing.Length; index++)
            {
                Object.Destroy(existing[index].gameObject);
            }

            yield return null;

            QingshiNavigationSurface surface =
                Object.FindAnyObjectByType<QingshiNavigationSurface>();
            Assert.That(surface, Is.Not.Null);
            GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = "G0401_NavigationObstacle";
            obstacle.transform.position = new Vector3(0f, 1f, -4f);
            obstacle.transform.localScale = new Vector3(2.4f, 2f, 6f);
            obstacle.transform.SetParent(surface.transform, true);
            Assert.That(surface.Rebuild(), Is.True);

            PlayerController player =
                Object.FindAnyObjectByType<PlayerController>();
            player.TeleportTo(new Vector3(5f, 0f, -4f), Quaternion.identity);
            PlayerStats stats = player.GetComponent<PlayerStats>();
            stats.SetHp(stats.MaxHp);

            EnemyData data = ConfigDatabase.Instance.GetEnemy(
                Wendao.Systems.Enemy.EnemyContentIds.GreyWolf);
            EnemyBrain wolf = EnemySpawner.CreateRuntimeEnemy(
                data,
                new Vector3(-5f, 0f, -4f),
                SceneManager.GetActiveScene(),
                null,
                "Enemy_G0401_NavigationProbe");
            Assert.That(wolf, Is.Not.Null);
            Assert.That(wolf.IsOnNavMesh, Is.True);

            wolf.OnAggro(player.gameObject);
            wolf.TickAI(EnemyBrain.AlertDurationSeconds);
            Assert.That(wolf.State, Is.EqualTo(EnemyBrainState.Chase));
            wolf.TickAI(0.1f);
            Assert.That(wolf.LastPathStatus, Is.EqualTo(NavMeshPathStatus.PathComplete));
            Assert.That(wolf.LastPathCornerCount, Is.GreaterThanOrEqualTo(3));

            Bounds forbidden = obstacle.GetComponent<Collider>().bounds;
            forbidden.Expand(new Vector3(0.6f, 0f, 0.6f));
            float greatestDetour = 0f;
            for (int index = 0; index < 45; index++)
            {
                wolf.TickAI(0.1f);
                Vector3 position = wolf.transform.position;
                greatestDetour = Mathf.Max(
                    greatestDetour,
                    Mathf.Abs(position.z + 4f));
                Assert.That(
                    IsInsideHorizontalBounds(position, forbidden),
                    Is.False,
                    "Wolf entered the solid obstacle instead of following NavMesh.");
            }

            Assert.That(greatestDetour, Is.GreaterThan(3.1f));
            Assert.That(wolf.transform.position.x, Is.GreaterThan(0f));
        }

        [Test]
        public void DeadTargetMakesWolfReturnHomeAndRestoreFullHealth()
        {
            EnemyBrain wolf = GetFirstWolfAndDisableOthers();
            PlayerController player =
                Object.FindAnyObjectByType<PlayerController>();
            PlayerStats stats = player.GetComponent<PlayerStats>();
            Vector3 spawn = wolf.SpawnPosition;
            player.TeleportTo(spawn + Vector3.back * 5f, Quaternion.identity);
            stats.SetHp(stats.MaxHp);

            wolf.OnAggro(player.gameObject);
            wolf.TickAI(EnemyBrain.AlertDurationSeconds);
            wolf.ApplyDamage(
                new DamageInfo
                {
                    Source = player.gameObject,
                    Amount = 20f,
                    Type = DamageType.True
                });
            Assert.That(wolf.CurrentHp, Is.LessThan(wolf.MaxHp));
            stats.SetHp(0f);
            wolf.TickAI(0.01f);
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
            return System.Array.FindAll(
                Object.FindObjectsByType<EnemyBrain>(),
                brain => brain != null
                    && brain.Data != null
                    && brain.Data.Id
                        == Wendao.Systems.Enemy.EnemyContentIds.GreyWolf);
        }

        private static EnemySpawner[] FindGreyWolfSpawners()
        {
            return System.Array.FindAll(
                Object.FindObjectsByType<EnemySpawner>(),
                spawner => spawner != null
                    && spawner.EnemyId
                        == Wendao.Systems.Enemy.EnemyContentIds.GreyWolf);
        }

        private static int SumMaxAlive(EnemySpawner[] spawners)
        {
            int total = 0;
            for (int index = 0; index < spawners.Length; index++)
            {
                total += spawners[index].MaxAlive;
            }

            return total;
        }

        private static bool IsInsideHorizontalBounds(
            Vector3 position,
            Bounds bounds)
        {
            return position.x > bounds.min.x
                && position.x < bounds.max.x
                && position.z > bounds.min.z
                && position.z < bounds.max.z;
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
