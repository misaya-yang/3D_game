using UnityEngine;
using UnityEngine.SceneManagement;
using Wendao.Systems.Enemy;
using Wendao.Systems.World;

namespace Wendao.Entities.Enemy
{
    public static class WolfRuntimeBootstrap
    {
        public const string SpawnerObjectName = "Spawner_GreyWolf_GVS07";
        public const string CreekSpawnerObjectName = "Spawner_GreyWolf_Creek";
        public const string SouthSpawnerObjectName = "Spawner_GreyWolf_South";
        public const int SpawnAreaCount = 3;
        public const int TotalConfiguredAlive = 7;

        private static readonly Vector3[] PrimarySpawnOffsets =
        {
            new Vector3(-1.5f, 0f, -1f),
            new Vector3(1.2f, 0f, -0.2f),
            new Vector3(0f, 0f, 1.5f)
        };

        private static readonly Vector3[] PairSpawnOffsets =
        {
            new Vector3(-0.9f, 0f, -0.5f),
            new Vector3(0.9f, 0f, 0.5f)
        };

        private static readonly Vector3[] PatrolOffsets =
        {
            Vector3.zero,
            new Vector3(2f, 0f, 0f),
            new Vector3(1.5f, 0f, 2f),
            new Vector3(-1f, 0f, 1.5f)
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallAtRuntime()
        {
            Install();
        }

        public static void Install()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureForScene(SceneManager.GetActiveScene());
        }

        public static EnemySpawner EnsureForScene(Scene scene)
        {
            if (!scene.IsValid()
                || !scene.isLoaded
                || scene.name != SceneLoader.DefaultMapSceneName)
            {
                return null;
            }

            QingshiGreyboxFactory.EnsureCreated(scene);
            EnemySpawner primary = EnsureSpawner(
                scene,
                SpawnerObjectName,
                new Vector3(10f, 0f, 8f),
                3,
                PrimarySpawnOffsets);
            EnsureSpawner(
                scene,
                CreekSpawnerObjectName,
                new Vector3(-9f, 0f, 7f),
                2,
                PairSpawnOffsets);
            EnsureSpawner(
                scene,
                SouthSpawnerObjectName,
                new Vector3(7f, 0f, -9f),
                2,
                PairSpawnOffsets);
            EnsureDebugSpawnService(primary);
            return primary;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene);
        }

        private static EnemySpawner EnsureSpawner(
            Scene scene,
            string objectName,
            Vector3 position,
            int maxAlive,
            Vector3[] spawnOffsets)
        {
            EnemySpawner spawner = FindInScene(scene, objectName);
            if (spawner == null)
            {
                var spawnerObject = new GameObject(objectName);
                spawnerObject.transform.position = position;
                SceneManager.MoveGameObjectToScene(spawnerObject, scene);
                spawner = spawnerObject.AddComponent<EnemySpawner>();
                spawner.Configure(
                    EnemyContentIds.GreyWolf,
                    maxAlive,
                    8f,
                    spawnOffsets,
                    PatrolOffsets);
            }
            else
            {
                spawner.transform.position = position;
                spawner.SetPatrolOffsets(PatrolOffsets);
            }

            return spawner;
        }

        private static EnemySpawner FindInScene(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                EnemySpawner spawner = root.GetComponentInChildren<EnemySpawner>(
                    true);
                if (spawner != null
                    && spawner.gameObject.name == objectName)
                {
                    return spawner;
                }
            }

            return null;
        }

        private static void EnsureDebugSpawnService(EnemySpawner spawner)
        {
            if (spawner != null
                && spawner.GetComponent<EnemySpawnService>() == null)
            {
                spawner.gameObject.AddComponent<EnemySpawnService>();
            }
        }
    }
}
