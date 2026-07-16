using UnityEngine;
using UnityEngine.SceneManagement;
using Wendao.Systems.Crafting;
using Wendao.Systems.World;

namespace Wendao.Entities.Visuals
{
    public static class AlchemyFurnaceRuntimeBootstrap
    {
        public const string QingshiFurnaceId = "furnace_qingshi_town";
        public const string CangwuFurnaceId = "furnace_cangwu_gate";
        public const string QingshiFurnaceObjectName =
            "Furnace_QingshiTown";
        public const string CangwuFurnaceObjectName =
            "Furnace_CangwuGate";
        public const string DisplayNameLocalizationKey =
            "world_name_alchemy_furnace";
        public const string DisplayNameDefaultValue = "青铜丹炉";

        public static void Install()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureForScene(SceneManager.GetActiveScene());
        }

        public static AlchemyFurnaceInteractable EnsureForScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            if (scene.name == SceneLoader.DefaultMapSceneName)
            {
                return EnsureFurnace(
                    scene,
                    QingshiFurnaceId,
                    QingshiFurnaceObjectName,
                    new Vector3(-4.2f, 0.05f, -0.8f),
                    18f);
            }

            if (scene.name == SceneLoader.CangwuMapSceneName)
            {
                return EnsureFurnace(
                    scene,
                    CangwuFurnaceId,
                    CangwuFurnaceObjectName,
                    new Vector3(5.2f, 0.05f, -11.5f),
                    -22f);
            }

            return null;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene);
        }

        private static AlchemyFurnaceInteractable EnsureFurnace(
            Scene scene,
            string furnaceId,
            string objectName,
            Vector3 position,
            float yaw)
        {
            AlchemyFurnaceInteractable existing = FindInScene(
                scene,
                furnaceId);
            if (existing != null)
            {
                existing.Configure(
                    furnaceId,
                    DisplayNameLocalizationKey,
                    DisplayNameDefaultValue);
                EnsureVisuals(existing.transform, yaw);
                return existing;
            }

            var root = new GameObject(objectName);
            root.transform.SetPositionAndRotation(
                position,
                Quaternion.Euler(0f, yaw, 0f));
            SceneManager.MoveGameObjectToScene(root, scene);

            SphereCollider trigger = root.AddComponent<SphereCollider>();
            trigger.radius = 1.2f;
            trigger.center = new Vector3(0f, 0.9f, 0f);
            trigger.isTrigger = true;

            AlchemyFurnaceInteractable furnace =
                root.AddComponent<AlchemyFurnaceInteractable>();
            furnace.Configure(
                furnaceId,
                DisplayNameLocalizationKey,
                DisplayNameDefaultValue);
            EnsureVisuals(root.transform, 0f);
            return furnace;
        }

        private static void EnsureVisuals(Transform root, float yaw)
        {
            if (root.Find("AlchemyHearth") == null)
            {
                BudgetVisualFactory.CreateResourceProp(
                    root,
                    BudgetArtCatalog.AlchemyHearth,
                    "AlchemyHearth",
                    Vector3.zero,
                    1.65f,
                    true,
                    yaw,
                    new Color(0.92f, 0.78f, 0.58f, 1f),
                    BudgetMaterialProfile.Stone);
            }

            if (root.Find("AlchemyPot") == null)
            {
                BudgetVisualFactory.CreateResourceProp(
                    root,
                    BudgetArtCatalog.AlchemyPot,
                    "AlchemyPot",
                    new Vector3(0f, 0.34f, 0f),
                    0.95f,
                    true,
                    yaw,
                    new Color(0.62f, 0.49f, 0.28f, 1f),
                    BudgetMaterialProfile.Accent);
            }

            Transform glow = root.Find("FurnaceGlow");
            if (glow == null)
            {
                var glowObject = new GameObject("FurnaceGlow");
                glowObject.transform.SetParent(root, false);
                glowObject.transform.localPosition = new Vector3(0f, 0.45f, 0f);
                Light light = glowObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 3.2f;
                light.intensity = 1.15f;
                light.color = new Color(1f, 0.48f, 0.16f, 1f);
                light.shadows = LightShadows.None;
            }
        }

        private static AlchemyFurnaceInteractable FindInScene(
            Scene scene,
            string furnaceId)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                AlchemyFurnaceInteractable[] furnaces =
                    root.GetComponentsInChildren<AlchemyFurnaceInteractable>(
                        true);
                for (int index = 0; index < furnaces.Length; index++)
                {
                    if (furnaces[index] != null
                        && furnaces[index].FurnaceId == furnaceId)
                    {
                        return furnaces[index];
                    }
                }
            }

            return null;
        }
    }
}
