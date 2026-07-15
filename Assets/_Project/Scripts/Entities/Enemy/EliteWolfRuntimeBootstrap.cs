using UnityEngine;
using UnityEngine.SceneManagement;
using Wendao.Systems.Enemy;
using Wendao.Systems.World;

namespace Wendao.Entities.Enemy
{
    public static class EliteWolfRuntimeBootstrap
    {
        public const string SpawnerObjectName = "Spawner_EliteWolf_HerbCreek";
        public static readonly Vector3 SpawnPosition =
            new Vector3(-5f, 0f, 11f);

        private static readonly Vector3[] SpawnOffsets =
        {
            Vector3.zero
        };

        private static readonly Vector3[] PatrolOffsets =
        {
            Vector3.zero,
            new Vector3(2f, 0f, -1f),
            new Vector3(1f, 0f, -3f),
            new Vector3(-2f, 0f, -2f)
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
            EnemySpawner existing = FindInScene(scene);
            if (existing != null)
            {
                existing.transform.position = SpawnPosition;
                existing.SetPatrolOffsets(PatrolOffsets);
                return existing;
            }

            var spawnerObject = new GameObject(SpawnerObjectName);
            spawnerObject.transform.position = SpawnPosition;
            SceneManager.MoveGameObjectToScene(spawnerObject, scene);
            EnemySpawner spawner = spawnerObject.AddComponent<EnemySpawner>();
            spawner.Configure(
                EnemyContentIds.EliteWolf,
                1,
                15f,
                SpawnOffsets,
                PatrolOffsets);
            return spawner;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene);
        }

        private static EnemySpawner FindInScene(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                EnemySpawner spawner = root.GetComponentInChildren<EnemySpawner>(
                    true);
                if (spawner != null
                    && spawner.gameObject.name == SpawnerObjectName)
                {
                    return spawner;
                }
            }

            return null;
        }
    }
}
