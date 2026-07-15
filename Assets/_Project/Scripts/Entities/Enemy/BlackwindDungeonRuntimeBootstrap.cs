using UnityEngine;
using UnityEngine.SceneManagement;
using Wendao.Systems.World;

namespace Wendao.Entities.Enemy
{
    public static class BlackwindDungeonRuntimeBootstrap
    {
        public const string EncounterRootName = "BlackwindDungeonEncounters";

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

        public static BlackwindDungeonEncounterController EnsureForScene(
            Scene scene)
        {
            if (!scene.IsValid()
                || !scene.isLoaded
                || scene.name != SceneLoader.BlackwindDungeonSceneName)
            {
                return null;
            }

            BlackwindDungeonFactory.EnsureCreated(scene);
            GameObject root = FindRoot(scene, EncounterRootName);
            if (root == null)
            {
                root = new GameObject(EncounterRootName);
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            BlackwindDungeonEncounterController controller =
                root.GetComponent<BlackwindDungeonEncounterController>()
                ?? root.AddComponent<BlackwindDungeonEncounterController>();
            controller.Configure(scene);
            return controller;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene);
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
