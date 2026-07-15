using UnityEngine;
using UnityEngine.SceneManagement;
using Wendao.Systems.Enemy;
using Wendao.Systems.World;

namespace Wendao.Entities.Enemy
{
    public static class BanditRuntimeBootstrap
    {
        public const string SpawnerObjectName = "Spawner_QingshiBandits";

        private static readonly Vector3[] SpawnOffsets =
        {
            new Vector3(-2f, 0f, -1f),
            new Vector3(2f, 0f, -1f),
            new Vector3(-1f, 0f, 2f),
            new Vector3(2f, 0f, 2f)
        };

        private static readonly Vector3[] PatrolOffsets =
        {
            Vector3.zero,
            new Vector3(3f, 0f, 0f),
            new Vector3(2f, 0f, 3f),
            new Vector3(-2f, 0f, 2f)
        };

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

            EnemySpawner existing = FindInScene(scene);
            if (existing != null)
            {
                return existing;
            }

            var spawnerObject = new GameObject(SpawnerObjectName);
            spawnerObject.transform.position = new Vector3(-16f, 0f, -12f);
            SceneManager.MoveGameObjectToScene(spawnerObject, scene);
            EnemySpawner spawner = spawnerObject.AddComponent<EnemySpawner>();
            spawner.Configure(
                EnemyContentIds.Bandit,
                4,
                10f,
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
