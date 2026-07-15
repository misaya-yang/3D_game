using UnityEngine;
using UnityEngine.SceneManagement;
using Wendao.Systems.Enemy;
using Wendao.Systems.World;

namespace Wendao.Entities.Enemy
{
    public static class CangwuTrialRuntimeBootstrap
    {
        public const string SpawnerObjectName =
            "Spawner_CangwuMountainRoadTrial";
        public const string MistSpawnerObjectName =
            "Spawner_CangwuMistValley";
        public const int RequiredSpawnPointCount =
            CangwuGreyboxFactory.RequiredSpawnPointCount;

        private static readonly Vector3 SpawnPosition =
            new Vector3(9f, 0f, -3f);

        private static readonly Vector3[] SpawnOffsets =
        {
            new Vector3(-2f, 0f, -2f),
            new Vector3(2f, 0f, 0f),
            new Vector3(0f, 0f, 2.5f)
        };

        private static readonly Vector3[] PatrolOffsets =
        {
            Vector3.zero,
            new Vector3(3f, 0f, 1f),
            new Vector3(-2f, 0f, 3f)
        };

        private static readonly Vector3 MistSpawnPosition =
            new Vector3(-8f, 0f, 3f);

        private static readonly Vector3[] MistSpawnOffsets =
        {
            new Vector3(-2f, 0f, -1.5f),
            new Vector3(2f, 0f, 1.5f)
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
                || scene.name != SceneLoader.CangwuMapSceneName)
            {
                return null;
            }

            CangwuGreyboxFactory.EnsureCreated(scene);
            EnemySpawner primary = EnsureSpawner(
                scene,
                SpawnerObjectName,
                SpawnPosition,
                3,
                SpawnOffsets,
                PatrolOffsets);
            EnsureSpawner(
                scene,
                MistSpawnerObjectName,
                MistSpawnPosition,
                2,
                MistSpawnOffsets,
                PatrolOffsets);
            return primary;
        }

        private static EnemySpawner EnsureSpawner(
            Scene scene,
            string objectName,
            Vector3 position,
            int count,
            Vector3[] spawnOffsets,
            Vector3[] patrolOffsets)
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
                count,
                8f,
                spawnOffsets,
                patrolOffsets);
            }
            else
            {
                spawner.transform.position = position;
                spawner.SetPatrolOffsets(patrolOffsets);
            }

            return spawner;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene);
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

                EnemySpawner[] spawners = root.GetComponentsInChildren<EnemySpawner>(
                    true);
                for (int index = 0; index < spawners.Length; index++)
                {
                    if (spawners[index].gameObject.name == objectName)
                    {
                        return spawners[index];
                    }
                }
            }

            return null;
        }
    }
}
