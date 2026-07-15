using UnityEngine;
using UnityEngine.SceneManagement;
using Wendao.Data;
using Wendao.Systems.Quest;
using Wendao.Systems.Shop;
using Wendao.Systems.World;

namespace Wendao.Entities.NPC
{
    public static class NpcRuntimeBootstrap
    {
        public const string YaoLaoObjectName = "NPC_YaoLao_Greybox";
        public const string ZhangguiObjectName = "NPC_Zhanggui_Greybox";
        public const string CangwuGuardObjectName = "NPC_CangwuGuard_Greybox";
        public const string DandingGuideObjectName = "NPC_DandingGuide_Greybox";
        public const string TrainerObjectName = "NPC_Trainer_Greybox";
        public const string HermitObjectName = "NPC_Hermit_Greybox";
        public const string BlackwindEchoB1ObjectName =
            "NPC_BlackwindEcho_B1_Greybox";
        public const string BlackwindEchoB5ObjectName =
            "NPC_BlackwindEcho_B5_Greybox";

        public static void Install()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureForScene(SceneManager.GetActiveScene());
        }

        public static NPCController EnsureForScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            if (scene.name == SceneLoader.CangwuMapSceneName)
            {
                NPCController guard = EnsureCangwuGuardForScene(scene);
                EnsureHermitForScene(scene);
                return guard;
            }

            if (scene.name == SceneLoader.BlackwindDungeonSceneName)
            {
                return EnsureBlackwindEchoesForScene(scene);
            }

            if (scene.name != SceneLoader.DefaultMapSceneName)
            {
                return null;
            }

            NPCData data = ConfigDatabase.Instance?.GetNpc(
                QuestContentIds.YaoLaoNpc);
            if (data == null)
            {
                return null;
            }

            NPCController existing = FindInScene(
                scene,
                YaoLaoObjectName,
                QuestContentIds.YaoLaoNpc);
            if (existing != null)
            {
                existing.ConfigureData(data);
                EnsureZhangguiForScene(scene);
                EnsureQingshiSideNpcsForScene(scene);
                return existing;
            }

            NPCController controller = CreateNpc(
                scene,
                data,
                YaoLaoObjectName,
                new Vector3(-2f, 1f, 1.5f),
                Quaternion.Euler(0f, 125f, 0f),
                new Color(0.28f, 0.46f, 0.36f, 1f));
            EnsureZhangguiForScene(scene);
            EnsureQingshiSideNpcsForScene(scene);
            return controller;
        }

        public static NPCController EnsureQingshiSideNpcsForScene(Scene scene)
        {
            if (!scene.IsValid()
                || !scene.isLoaded
                || scene.name != SceneLoader.DefaultMapSceneName)
            {
                return null;
            }

            NPCController guide = EnsureQuestNpc(
                scene,
                QuestContentIds.DandingGuideNpc,
                DandingGuideObjectName,
                new Vector3(-5f, 1f, 3.5f),
                new Color(0.58f, 0.42f, 0.22f, 1f));
            EnsureQuestNpc(
                scene,
                QuestContentIds.TrainerNpc,
                TrainerObjectName,
                new Vector3(7f, 1f, -1.5f),
                new Color(0.42f, 0.32f, 0.26f, 1f));
            return guide;
        }

        public static NPCController EnsureHermitForScene(Scene scene)
        {
            if (!scene.IsValid()
                || !scene.isLoaded
                || scene.name != SceneLoader.CangwuMapSceneName)
            {
                return null;
            }

            return EnsureQuestNpc(
                scene,
                QuestContentIds.HermitNpc,
                HermitObjectName,
                new Vector3(15f, 1f, 18f),
                new Color(0.36f, 0.48f, 0.4f, 1f));
        }

        public static NPCController EnsureCangwuGuardForScene(Scene scene)
        {
            if (!scene.IsValid()
                || !scene.isLoaded
                || scene.name != SceneLoader.CangwuMapSceneName)
            {
                return null;
            }

            NPCData data = ConfigDatabase.Instance?.GetNpc(
                QuestContentIds.CangwuGuardNpc);
            if (data == null)
            {
                return null;
            }

            NPCController existing = FindInScene(
                scene,
                CangwuGuardObjectName,
                QuestContentIds.CangwuGuardNpc);
            if (existing != null)
            {
                existing.ConfigureData(data);
                return existing;
            }

            return CreateNpc(
                scene,
                data,
                CangwuGuardObjectName,
                new Vector3(2f, 1f, -12f),
                Quaternion.Euler(0f, 210f, 0f),
                new Color(0.28f, 0.42f, 0.55f, 1f));
        }

        public static NPCController EnsureBlackwindEchoesForScene(Scene scene)
        {
            if (!scene.IsValid()
                || !scene.isLoaded
                || scene.name != SceneLoader.BlackwindDungeonSceneName)
            {
                return null;
            }

            NPCData data = ConfigDatabase.Instance?.GetNpc(
                QuestContentIds.BlackwindEchoNpc);
            if (data == null)
            {
                return null;
            }

            NPCController first = EnsureBlackwindEcho(
                scene,
                data,
                BlackwindEchoB1ObjectName,
                BlackwindDungeonFactory.GetFloorCenter(1)
                    + new Vector3(-3f, 1f, -2f));
            EnsureBlackwindEcho(
                scene,
                data,
                BlackwindEchoB5ObjectName,
                BlackwindDungeonFactory.GetFloorCenter(5)
                    + new Vector3(3f, 1f, 2f));
            return first;
        }

        private static NPCController EnsureBlackwindEcho(
            Scene scene,
            NPCData data,
            string objectName,
            Vector3 position)
        {
            NPCController existing = FindInScene(
                scene,
                objectName,
                data.Id,
                false);
            if (existing != null)
            {
                existing.ConfigureData(data);
                return existing;
            }

            return CreateNpc(
                scene,
                data,
                objectName,
                position,
                Quaternion.identity,
                new Color(0.35f, 0.65f, 0.72f, 0.7f));
        }

        private static NPCController EnsureQuestNpc(
            Scene scene,
            string npcId,
            string objectName,
            Vector3 position,
            Color color)
        {
            NPCData data = ConfigDatabase.Instance?.GetNpc(npcId);
            if (data == null)
            {
                return null;
            }

            NPCController existing = FindInScene(scene, objectName, npcId);
            if (existing != null)
            {
                existing.ConfigureData(data);
                return existing;
            }

            return CreateNpc(
                scene,
                data,
                objectName,
                position,
                Quaternion.identity,
                color);
        }

        public static NPCController EnsureZhangguiForScene(Scene scene)
        {
            if (!scene.IsValid()
                || !scene.isLoaded
                || scene.name != SceneLoader.DefaultMapSceneName)
            {
                return null;
            }

            NPCData data = ConfigDatabase.Instance?.GetNpc(
                ShopContentIds.ZhangguiNpc);
            if (data == null)
            {
                return null;
            }

            NPCController existing = FindInScene(
                scene,
                ZhangguiObjectName,
                ShopContentIds.ZhangguiNpc);
            if (existing != null)
            {
                existing.ConfigureData(data);
                return existing;
            }

            return CreateNpc(
                scene,
                data,
                ZhangguiObjectName,
                new Vector3(3f, 1f, 2f),
                Quaternion.Euler(0f, 225f, 0f),
                new Color(0.52f, 0.34f, 0.18f, 1f));
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene);
        }

        private static NPCController FindInScene(
            Scene scene,
            string objectName,
            string npcId,
            bool matchNpcId = true)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                NPCController[] controllers =
                    root.GetComponentsInChildren<NPCController>(true);
                for (int index = 0; index < controllers.Length; index++)
                {
                    NPCController controller = controllers[index];
                    if (controller != null
                        && (controller.gameObject.name == objectName
                            || (matchNpcId && controller.Data?.Id == npcId)))
                    {
                        return controller;
                    }
                }
            }

            return null;
        }

        private static NPCController CreateNpc(
            Scene scene,
            NPCData data,
            string objectName,
            Vector3 position,
            Quaternion rotation,
            Color color)
        {
            GameObject npcObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npcObject.name = objectName;
            npcObject.transform.SetPositionAndRotation(position, rotation);
            SceneManager.MoveGameObjectToScene(npcObject, scene);
            ApplyMaterial(npcObject.GetComponent<Renderer>(), objectName, color);
            NPCController controller = npcObject.AddComponent<NPCController>();
            controller.ConfigureData(data);
            return controller;
        }

        private static void ApplyMaterial(
            Renderer renderer,
            string objectName,
            Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (renderer == null || shader == null)
            {
                return;
            }

            renderer.sharedMaterial = new Material(shader)
            {
                name = objectName + "_Runtime",
                color = color
            };
        }
    }
}
