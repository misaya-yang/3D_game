using UnityEngine;
using UnityEngine.SceneManagement;
using Wendao.Systems.World;

namespace Wendao.Entities.Enemy
{
    public static class TrainingDummyRuntimeBootstrap
    {
        public const string DummyObjectName = "TrainingDummy_Greybox";

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

        public static TrainingDummy EnsureForScene(Scene scene)
        {
            if (!scene.IsValid()
                || !scene.isLoaded
                || scene.name != SceneLoader.DefaultMapSceneName)
            {
                return null;
            }

            TrainingDummy existing = FindInScene(scene);
            if (existing != null)
            {
                return existing;
            }

            GameObject dummyObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dummyObject.name = DummyObjectName;
            dummyObject.transform.SetPositionAndRotation(
                new Vector3(0f, 1f, 2.2f),
                Quaternion.identity);
            dummyObject.transform.localScale = new Vector3(0.55f, 1f, 0.55f);
            SceneManager.MoveGameObjectToScene(dummyObject, scene);
            ApplyMaterial(dummyObject.GetComponent<Renderer>());
            return dummyObject.AddComponent<TrainingDummy>();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene);
        }

        private static TrainingDummy FindInScene(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                TrainingDummy result = root.GetComponentInChildren<TrainingDummy>(true);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static void ApplyMaterial(Renderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                return;
            }

            renderer.sharedMaterial = new Material(shader)
            {
                name = "TrainingDummy_Greybox_Runtime",
                color = new Color(0.53f, 0.36f, 0.2f, 1f)
            };
        }
    }
}
