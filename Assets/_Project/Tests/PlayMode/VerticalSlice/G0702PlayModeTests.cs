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
using Wendao.Systems.Content;
using Wendao.Systems.Crafting;
using Wendao.Systems.World;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0702PlayModeTests
    {
        private ConfigDatabase _database;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EventBus.Clear();
            DestroyRuntimeObjects();
            yield return null;
            ServiceLocator.Clear();
            _database = new GameObject("[G0702Config]")
                .AddComponent<ConfigDatabase>();
            _database.LoadAll();
            Assert.That(_database.IsSafeMode, Is.False, _database.LastError);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            EventBus.Clear();
            DestroyRuntimeObjects();
            yield return null;
            ServiceLocator.Clear();
        }

        [Test]
        public void MvpContentGraphHasStableIdsAndLocalizedDefaults()
        {
            MvpContentAuditResult audit = MvpContentAudit.Evaluate(_database);
            Assert.That(
                audit.IsValid,
                Is.True,
                string.Join("\n", audit.Issues));
        }

        [Test]
        public void MainStoryEconomyAndFireRootPacingStayInsideTargets()
        {
            MvpBalanceAuditResult audit = MvpBalanceAudit.Evaluate(_database);

            Assert.That(audit.IsValid, Is.True, audit.FailureReason);
            Assert.That(audit.MainQuestSpiritStones, Is.EqualTo(85));
            Assert.That(audit.GuaranteedAchievementSpiritStones, Is.EqualTo(150));
            Assert.That(audit.CompletionMinimum, Is.EqualTo(251));
            Assert.That(audit.CompletionMaximum, Is.EqualTo(293));
            Assert.That(
                audit.FireXpBeforeFoundation,
                Is.GreaterThanOrEqualTo(audit.RequiredXpBeforeFoundation));
            Assert.That(
                audit.FireXpBeforeGoldenCore,
                Is.GreaterThanOrEqualTo(audit.RequiredXpBeforeGoldenCore));
            Assert.That(audit.EstimatedMainStoryMinutes, Is.EqualTo(195));
        }

        [Test]
        public void EastFieldFifteenMinuteProxyIsReproducible()
        {
            MvpBalanceAuditResult audit = MvpBalanceAudit.Evaluate(_database);

            Assert.That(audit.EastField15MinuteMinimumStones, Is.Zero);
            Assert.That(audit.EastField15MinuteMaximumStones, Is.EqualTo(14));
            Assert.That(
                audit.EastField15MinuteCultivationXp,
                Is.EqualTo(115.5f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator CangwuMeetsSpawnerGatherableAndChestDensity()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByName(
                SceneLoader.CangwuMapSceneName);
            bool createdScene = !scene.IsValid() || !scene.isLoaded;
            if (createdScene)
            {
                scene = SceneManager.CreateScene(SceneLoader.CangwuMapSceneName);
            }

            SceneManager.SetActiveScene(scene);
            CangwuGreyboxFactory.EnsureCreated(scene);
            CangwuTrialRuntimeBootstrap.EnsureForScene(scene);
            yield return null;

            GatherableObject[] gatherables =
                Object.FindObjectsByType<GatherableObject>(FindObjectsInactive.Include);
            var cangwuNodeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (GatherableObject gatherable in gatherables)
            {
                if (gatherable.gameObject.scene == scene)
                {
                    cangwuNodeIds.Add(gatherable.NodeId);
                }
            }

            Assert.That(cangwuNodeIds,
                Is.EquivalentTo(GatheringContentIds.CangwuNodes));
            Assert.That(
                CountNamedObjects(
                    scene,
                    MapContentIds.CangwuMistChest,
                    MapContentIds.CangwuCaveChest,
                    MapContentIds.CangwuTerraceChest),
                Is.EqualTo(CangwuGreyboxFactory.RequiredChestCount));

            EnemySpawner[] spawners = Object.FindObjectsByType<EnemySpawner>(FindObjectsInactive.Include);
            var spawnPointCount = 0;
            foreach (EnemySpawner spawner in spawners)
            {
                if (spawner.gameObject.scene == scene)
                {
                    spawnPointCount += spawner.MaxAlive;
                }
            }

            Assert.That(spawnPointCount,
                Is.EqualTo(CangwuTrialRuntimeBootstrap.RequiredSpawnPointCount));

            if (previous.IsValid()
                && previous.isLoaded
                && previous.handle != scene.handle)
            {
                SceneManager.SetActiveScene(previous);
            }

            if (createdScene)
            {
                yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        [UnityTest]
        public IEnumerator BlackwindContainsThreeStableChestIds()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByName(
                SceneLoader.BlackwindDungeonSceneName);
            bool createdScene = !scene.IsValid() || !scene.isLoaded;
            if (createdScene)
            {
                scene = SceneManager.CreateScene(
                    SceneLoader.BlackwindDungeonSceneName);
            }

            SceneManager.SetActiveScene(scene);
            BlackwindDungeonFactory.EnsureCreated(scene);
            yield return null;

            Assert.That(
                CountNamedObjects(
                    scene,
                    MapContentIds.BlackwindEntryChest,
                    MapContentIds.BlackwindBranchChest,
                    MapContentIds.BlackwindDeepChest),
                Is.EqualTo(BlackwindDungeonFactory.RequiredChestCount));

            if (previous.IsValid()
                && previous.isLoaded
                && previous.handle != scene.handle)
            {
                SceneManager.SetActiveScene(previous);
            }

            if (createdScene)
            {
                yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        private static int CountNamedObjects(Scene scene, params string[] names)
        {
            var expected = new HashSet<string>(names, StringComparer.Ordinal);
            var found = new HashSet<string>(StringComparer.Ordinal);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                CollectNames(root.transform, expected, found);
            }

            return found.Count;
        }

        private static void CollectNames(
            Transform current,
            HashSet<string> expected,
            HashSet<string> found)
        {
            if (expected.Contains(current.name))
            {
                found.Add(current.name);
            }

            for (int index = 0; index < current.childCount; index++)
            {
                CollectNames(current.GetChild(index), expected, found);
            }
        }

        private static void DestroyRuntimeObjects()
        {
            DestroyAll<EnemySpawner>();
            DestroyAll<ConfigDatabase>();
            GameObject[] objects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            foreach (GameObject gameObject in objects)
            {
                if (gameObject != null
                    && gameObject.name.StartsWith("[G0702", StringComparison.Ordinal))
                {
                    Object.Destroy(gameObject);
                }
            }
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
