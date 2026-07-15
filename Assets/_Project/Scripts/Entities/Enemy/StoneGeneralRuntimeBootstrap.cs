using UnityEngine;
using UnityEngine.SceneManagement;
using Wendao.Data;
using Wendao.Systems.Enemy;
using Wendao.Systems.World;

namespace Wendao.Entities.Enemy
{
    public static class StoneGeneralRuntimeBootstrap
    {
        public const string ArenaObjectName = "Arena_StoneGeneral_Runtime";
        public const string BossObjectName = "Boss_StoneGeneral_Runtime";
        public static readonly Vector3 ArenaCenter =
            new Vector3(-8f, 0f, -7f);

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

        public static EnemyBrain EnsureForScene(Scene scene)
        {
            if (!scene.IsValid()
                || !scene.isLoaded
                || scene.name != SceneLoader.DefaultMapSceneName)
            {
                return null;
            }

            QingshiGreyboxFactory.EnsureCreated(scene);
            GameObject arena = FindRoot(scene, ArenaObjectName);
            if (arena == null)
            {
                arena = CreateArena(scene);
            }

            EnemyBrain boss = arena.GetComponentInChildren<EnemyBrain>(true);
            if (boss == null || boss.Data?.Id != EnemyContentIds.StoneGeneral)
            {
                EnemyData data = ConfigDatabase.Instance?.GetEnemy(
                    EnemyContentIds.StoneGeneral);
                boss = EnemySpawner.CreateRuntimeEnemy(
                    data,
                    ArenaCenter,
                    scene,
                    arena.transform,
                    BossObjectName);
            }

            BossArenaController controller =
                arena.GetComponent<BossArenaController>();
            if (controller == null)
            {
                controller = arena.AddComponent<BossArenaController>();
            }

            controller.Configure(
                boss,
                ArenaCenter,
                BossArenaController.DefaultArenaRadius);
            return boss;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene);
        }

        private static GameObject CreateArena(Scene scene)
        {
            var arena = new GameObject(ArenaObjectName);
            arena.transform.position = ArenaCenter;
            SceneManager.MoveGameObjectToScene(arena, scene);
            SphereCollider trigger = arena.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = BossArenaController.DefaultArenaRadius;

            for (int index = 0; index < 4; index++)
            {
                float angle = index * Mathf.PI * 0.5f;
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = $"ArenaMarker_{index + 1}";
                marker.transform.SetParent(arena.transform, false);
                marker.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * BossArenaController.DefaultArenaRadius,
                    0.15f,
                    Mathf.Sin(angle) * BossArenaController.DefaultArenaRadius);
                marker.transform.localScale = new Vector3(0.2f, 0.15f, 0.2f);
                Collider collider = marker.GetComponent<Collider>();
                if (collider != null)
                {
                    collider.enabled = false;
                }
            }

            return arena;
        }

        private static GameObject FindRoot(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == objectName)
                {
                    return root;
                }
            }

            return null;
        }
    }
}
