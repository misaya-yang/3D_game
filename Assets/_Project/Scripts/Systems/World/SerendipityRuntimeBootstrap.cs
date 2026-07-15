using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wendao.Systems.World
{
    public static class SerendipityRuntimeBootstrap
    {
        public const string QingshiObjectName = "Serendipity_QingshiHerbSpirit";
        public const string CangwuSteleObjectName = "Serendipity_CangwuMistStele";
        public const string CangwuCliffObjectName = "Serendipity_CangwuCliffBox";
        public const string BlackwindObjectName = "Serendipity_BlackwindEchoCache";

        public static void Install()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureForScene(SceneManager.GetActiveScene());
        }

        public static int EnsureForScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return 0;
            }

            if (scene.name == SceneLoader.DefaultMapSceneName)
            {
                QingshiGreyboxFactory.EnsureCreated(scene);
                EnsureTrigger(
                    scene,
                    QingshiObjectName,
                    SerendipityContentIds.QingshiHerbSpirit,
                    MapContentIds.QingshiMap,
                    new Vector3(-10f, 0.8f, 10f),
                    PrimitiveType.Sphere,
                    new Color(0.38f, 0.9f, 0.48f, 0.8f));
                return 1;
            }

            if (scene.name == SceneLoader.CangwuMapSceneName)
            {
                CangwuGreyboxFactory.EnsureCreated(scene);
                EnsureTrigger(
                    scene,
                    CangwuSteleObjectName,
                    SerendipityContentIds.CangwuMistStele,
                    MapContentIds.CangwuMap,
                    new Vector3(-11f, 1f, 3f),
                    PrimitiveType.Cube,
                    new Color(0.42f, 0.7f, 0.68f, 0.8f));
                EnsureTrigger(
                    scene,
                    CangwuCliffObjectName,
                    SerendipityContentIds.CangwuCliffBox,
                    MapContentIds.CangwuMap,
                    new Vector3(11f, 0.8f, 15f),
                    PrimitiveType.Cube,
                    new Color(0.72f, 0.53f, 0.25f, 0.9f));
                return 2;
            }

            if (scene.name == SceneLoader.BlackwindDungeonSceneName)
            {
                BlackwindDungeonFactory.EnsureCreated(scene);
                EnsureTrigger(
                    scene,
                    BlackwindObjectName,
                    SerendipityContentIds.BlackwindEchoCache,
                    MapContentIds.BlackwindMap,
                    BlackwindDungeonFactory.GetFloorCenter(3)
                        + new Vector3(-4f, 0.8f, 3f),
                    PrimitiveType.Sphere,
                    new Color(0.46f, 0.31f, 0.62f, 0.85f));
                return 1;
            }

            return 0;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene);
        }

        private static void EnsureTrigger(
            Scene scene,
            string objectName,
            string serendipityId,
            string mapId,
            Vector3 position,
            PrimitiveType shape,
            Color color)
        {
            SerendipityTrigger existing = FindInScene(scene, objectName);
            if (existing != null)
            {
                existing.transform.position = position;
                return;
            }

            var triggerObject = new GameObject(objectName);
            triggerObject.transform.position = position;
            SceneManager.MoveGameObjectToScene(triggerObject, scene);
            triggerObject.AddComponent<SphereCollider>();
            SerendipityTrigger trigger =
                triggerObject.AddComponent<SerendipityTrigger>();

            GameObject visual = GameObject.CreatePrimitive(shape);
            visual.name = objectName + "_Visual";
            visual.transform.SetParent(triggerObject.transform, false);
            visual.transform.localScale = shape == PrimitiveType.Cube
                ? new Vector3(0.8f, 1.5f, 0.35f)
                : Vector3.one * 0.75f;
            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                visualCollider.enabled = false;
            }

            Renderer renderer = visual.GetComponent<Renderer>();
            ApplyMaterial(renderer, objectName, color);
            trigger.Configure(serendipityId, mapId, renderer);
        }

        private static SerendipityTrigger FindInScene(
            Scene scene,
            string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                SerendipityTrigger[] triggers =
                    root.GetComponentsInChildren<SerendipityTrigger>(true);
                for (int index = 0; index < triggers.Length; index++)
                {
                    if (triggers[index].gameObject.name == objectName)
                    {
                        return triggers[index];
                    }
                }
            }

            return null;
        }

        private static void ApplyMaterial(
            Renderer renderer,
            string objectName,
            Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");
            if (renderer == null || shader == null)
            {
                return;
            }

            renderer.sharedMaterial = new Material(shader)
            {
                name = objectName + "_Material",
                color = color,
                hideFlags = HideFlags.DontSave
            };
        }
    }
}
