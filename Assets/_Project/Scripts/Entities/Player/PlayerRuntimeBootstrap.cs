using UnityEngine;
using UnityEngine.SceneManagement;
using Wendao.CameraSystem;
using Wendao.Core;
using Wendao.Entities.Enemy;
using Wendao.Entities.NPC;
using Wendao.Systems.World;

namespace Wendao.Entities.Player
{
    public static class PlayerRuntimeBootstrap
    {
        public const string PlayerPrefabResourcePath = "Prefabs/Player/Player_Greybox";
        public const string PlayerObjectName = "Player_Greybox";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallAtRuntime()
        {
            Install();
        }

        public static void Install()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            TrainingDummyRuntimeBootstrap.Install();
            NpcRuntimeBootstrap.Install();
            WolfRuntimeBootstrap.Install();
            EliteWolfRuntimeBootstrap.Install();
            BanditRuntimeBootstrap.Install();
            CangwuTrialRuntimeBootstrap.Install();
            StoneGeneralRuntimeBootstrap.Install();
            BlackwindDungeonRuntimeBootstrap.Install();
            EnsureForScene(SceneManager.GetActiveScene());
        }

        public static PlayerController EnsureForScene(Scene scene)
        {
            if (!scene.IsValid()
                || !scene.isLoaded
                || (scene.name != SceneLoader.DefaultMapSceneName
                    && scene.name != SceneLoader.CangwuMapSceneName
                    && scene.name != SceneLoader.BlackwindDungeonSceneName))
            {
                return null;
            }

            bool isQingshi = scene.name == SceneLoader.DefaultMapSceneName;
            if (isQingshi)
            {
                QingshiGreyboxFactory.EnsureCreated(scene);
                NpcRuntimeBootstrap.EnsureForScene(scene);
            }
            else if (scene.name == SceneLoader.CangwuMapSceneName)
            {
                CangwuGreyboxFactory.EnsureCreated(scene);
            }
            else
            {
                BlackwindDungeonFactory.EnsureCreated(scene);
            }

            PlayerController player = FindInScene<PlayerController>(scene);
            if (player == null)
            {
                player = CreatePlayer(scene);
            }

            EnsureGameplayComponents(player.gameObject);

            EnsureCamera(scene, player.transform);
            if (isQingshi)
            {
                WolfRuntimeBootstrap.EnsureForScene(scene);
                EliteWolfRuntimeBootstrap.EnsureForScene(scene);
                BanditRuntimeBootstrap.EnsureForScene(scene);
                StoneGeneralRuntimeBootstrap.EnsureForScene(scene);
            }
            else if (scene.name == SceneLoader.BlackwindDungeonSceneName)
            {
                BlackwindDungeonRuntimeBootstrap.EnsureForScene(scene);
            }
            else if (scene.name == SceneLoader.CangwuMapSceneName)
            {
                CangwuTrialRuntimeBootstrap.EnsureForScene(scene);
                NpcRuntimeBootstrap.EnsureForScene(scene);
            }

            return player;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene);
        }

        private static PlayerController CreatePlayer(Scene scene)
        {
            GameObject prefab = Resources.Load<GameObject>(PlayerPrefabResourcePath);
            GameObject playerObject = prefab != null
                ? Object.Instantiate(prefab)
                : new GameObject(PlayerObjectName);
            playerObject.name = PlayerObjectName;
            SceneManager.MoveGameObjectToScene(playerObject, scene);

            EnsureGameplayComponents(playerObject);

            PlayerController player = playerObject.GetComponent<PlayerController>();

            Transform spawn = FindRequestedSpawn(scene);
            Vector3 position = spawn != null ? spawn.position : Vector3.zero;
            Quaternion rotation = spawn != null ? spawn.rotation : Quaternion.identity;
            player.TeleportTo(position, rotation);
            return player;
        }

        private static Transform FindRequestedSpawn(Scene scene)
        {
            string requestedSpawnId = SceneLoader.Instance?.PendingSpawnId;
            if (!string.IsNullOrWhiteSpace(requestedSpawnId))
            {
                RespawnPoint[] points = Object.FindObjectsByType<RespawnPoint>(FindObjectsInactive.Include);
                foreach (RespawnPoint point in points)
                {
                    if (point != null
                        && point.gameObject.scene == scene
                        && point.Id == requestedSpawnId)
                    {
                        return point.transform;
                    }
                }
            }

            string defaultSpawnName;
            if (scene.name == SceneLoader.CangwuMapSceneName)
            {
                defaultSpawnName = CangwuGreyboxFactory.DefaultSpawnName;
            }
            else if (scene.name == SceneLoader.BlackwindDungeonSceneName)
            {
                int floor = ServiceLocator.TryGet<IBlackwindDungeonService>(
                        out IBlackwindDungeonService dungeon)
                    ? dungeon.CurrentFloor
                    : 1;
                defaultSpawnName = BlackwindDungeonFactory.GetSpawnName(floor);
            }
            else
            {
                defaultSpawnName = QingshiGreyboxFactory.DefaultSpawnName;
            }

            return FindTransform(scene, defaultSpawnName);
        }

        private static void EnsureGameplayComponents(GameObject playerObject)
        {
            if (playerObject.GetComponent<CharacterController>() == null)
            {
                playerObject.AddComponent<CharacterController>();
            }

            if (playerObject.GetComponent<PlayerInputReader>() == null)
            {
                playerObject.AddComponent<PlayerInputReader>();
            }

            if (playerObject.GetComponent<PlayerController>() == null)
            {
                playerObject.AddComponent<PlayerController>();
            }

            if (playerObject.GetComponent<PlayerActionBuffer>() == null)
            {
                playerObject.AddComponent<PlayerActionBuffer>();
            }

            if (playerObject.GetComponent<PlayerStats>() == null)
            {
                playerObject.AddComponent<PlayerStats>();
            }

            if (playerObject.GetComponent<PlayerCombatController>() == null)
            {
                playerObject.AddComponent<PlayerCombatController>();
            }

            if (playerObject.GetComponent<PlayerTargetingController>() == null)
            {
                playerObject.AddComponent<PlayerTargetingController>();
            }

            if (playerObject.GetComponent<PlayerSkillController>() == null)
            {
                playerObject.AddComponent<PlayerSkillController>();
            }
        }

        private static void EnsureCamera(Scene scene, Transform target)
        {
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("GameplayCamera");
                cameraObject.tag = "MainCamera";
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                camera = cameraObject.AddComponent<UnityEngine.Camera>();
            }

            ThirdPersonCamera thirdPersonCamera =
                camera.GetComponent<ThirdPersonCamera>();
            if (thirdPersonCamera == null)
            {
                thirdPersonCamera = camera.gameObject.AddComponent<ThirdPersonCamera>();
            }

            thirdPersonCamera.SetTarget(target);
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T result = root.GetComponentInChildren<T>(true);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static Transform FindTransform(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform result = FindTransformRecursive(root.transform, objectName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static Transform FindTransformRecursive(Transform current, string objectName)
        {
            if (current.name == objectName)
            {
                return current;
            }

            for (int i = 0; i < current.childCount; i++)
            {
                Transform result = FindTransformRecursive(current.GetChild(i), objectName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }
}
