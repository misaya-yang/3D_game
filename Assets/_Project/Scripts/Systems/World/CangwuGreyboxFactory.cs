using UnityEngine;
using UnityEngine.SceneManagement;
using Wendao.Systems.Crafting;
using Wendao.Systems.Inventory;

namespace Wendao.Systems.World
{
    public static class CangwuGreyboxFactory
    {
        public const string RootName = "Map_Cangwu_Greybox";
        public const string GroundName = "Greybox_CangwuGround";
        public const string DefaultSpawnName = "CangwuGateSpawn";
        public const string AreaRootName = "AreaMarkers_Cangwu";
        public const string GatePlatformAreaName = "Area_CangwuGatePlatform";
        public const string MountainRoadAreaName = "Area_CangwuMountainRoad";
        public const string MistValleyAreaName = "Area_CangwuMistValley";
        public const string CaveAreaName = "Area_CangwuCave";
        public const string ThunderTerraceAreaName = "Area_CangwuThunderTerrace";
        public const string GatherableRootName = "Gatherables_Cangwu";
        public const int RequiredAreaCount = 5;
        public const int RequiredSpawnPointCount = 5;
        public const int RequiredGatherableCount = 8;
        public const int RequiredChestCount = 3;

        public static void EnsureCreated(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            GameObject root = FindRoot(scene, RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root, scene);
                EnsureGround(root.transform);
                EnsureLighting(root.transform);
            }

            EnsureSpawn(root.transform);
            EnsureAreas(root.transform);
            EnsureLandmarks(root.transform);
            EnsureGatherables(root.transform);
            EnsureChests(root.transform);
            EnsureNavigation(root);
        }

        private static void EnsureGround(Transform root)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = GroundName;
            ground.transform.SetParent(root, false);
            ground.transform.localPosition = new Vector3(0f, -0.5f, 2f);
            ground.transform.localScale = new Vector3(38f, 1f, 46f);
            ApplyMaterial(
                ground.GetComponent<Renderer>(),
                new Color(0.19f, 0.27f, 0.22f, 1f),
                "mat_cangwu_ground");
        }

        private static void EnsureLighting(Transform root)
        {
            if (Camera.main == null)
            {
                var cameraObject = new GameObject("Greybox_CangwuCamera");
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetParent(root, false);
                cameraObject.transform.localPosition = new Vector3(0f, 15f, -17f);
                cameraObject.transform.LookAt(new Vector3(0f, 0f, 5f));
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.28f, 0.34f, 0.32f, 1f);
            }

            var lightObject = new GameObject("Greybox_CangwuSun");
            lightObject.transform.SetParent(root, false);
            lightObject.transform.localRotation = Quaternion.Euler(52f, -35f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.05f;
            light.shadows = LightShadows.Soft;
        }

        private static void EnsureSpawn(Transform root)
        {
            Transform spawn = FindChildRecursive(root, DefaultSpawnName);
            if (spawn == null)
            {
                var spawnObject = new GameObject(DefaultSpawnName);
                spawnObject.transform.SetParent(root, false);
                spawn = spawnObject.transform;
            }

            spawn.localPosition = new Vector3(0f, 0f, -15f);
            RespawnPoint respawn = spawn.GetComponent<RespawnPoint>()
                ?? spawn.gameObject.AddComponent<RespawnPoint>();
            respawn.Configure(MapContentIds.CangwuGateTeleport);
            TeleportPoint teleport = spawn.GetComponent<TeleportPoint>()
                ?? spawn.gameObject.AddComponent<TeleportPoint>();
            teleport.Configure(
                MapContentIds.CangwuGateTeleport,
                MapContentIds.CangwuMap);

            Transform ring = spawn.Find("TeleportRing_Greybox");
            if (ring == null)
            {
                GameObject ringObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ringObject.name = "TeleportRing_Greybox";
                ringObject.transform.SetParent(spawn, false);
                ring = ringObject.transform;
            }

            ring.localPosition = new Vector3(0f, 0.08f, 0f);
            ring.localScale = new Vector3(0.86f, 0.035f, 0.86f);
            Collider ringCollider = ring.GetComponent<Collider>();
            if (ringCollider != null)
            {
                ringCollider.enabled = false;
            }

            ApplyMaterial(
                ring.GetComponent<Renderer>(),
                new Color(0.08f, 0.24f, 0.22f, 1f),
                "mat_cangwu_teleport");
        }

        private static void EnsureAreas(Transform root)
        {
            Transform areaRoot = FindChildRecursive(root, AreaRootName);
            if (areaRoot == null)
            {
                var areaObject = new GameObject(AreaRootName);
                areaObject.transform.SetParent(root, false);
                areaRoot = areaObject.transform;
            }

            EnsureArea(
                areaRoot,
                GatePlatformAreaName,
                "area_cangwu_gate_platform",
                "area_name_cangwu_gate_platform",
                "山门平台",
                new Vector3(0f, 0f, -13f),
                new Vector2(14f, 7f),
                new Color(0.35f, 0.39f, 0.43f, 0.65f));
            EnsureArea(
                areaRoot,
                MountainRoadAreaName,
                "area_cangwu_mountain_road",
                "area_name_cangwu_mountain_road",
                "盘山道",
                new Vector3(9f, 0f, -3f),
                new Vector2(10f, 13f),
                new Color(0.38f, 0.31f, 0.22f, 0.65f));
            EnsureArea(
                areaRoot,
                MistValleyAreaName,
                "area_cangwu_mist_valley",
                "area_name_cangwu_mist_valley",
                "云雾谷",
                new Vector3(-8f, 0f, 2f),
                new Vector2(13f, 11f),
                new Color(0.33f, 0.46f, 0.45f, 0.65f));
            EnsureArea(
                areaRoot,
                CaveAreaName,
                "area_cangwu_cave",
                "area_name_cangwu_cave",
                "洞府",
                new Vector3(-8f, 0f, 13f),
                new Vector2(11f, 8f),
                new Color(0.28f, 0.24f, 0.34f, 0.65f));
            EnsureArea(
                areaRoot,
                ThunderTerraceAreaName,
                "area_cangwu_thunder_terrace",
                "area_name_cangwu_thunder_terrace",
                "雷台远眺",
                new Vector3(8f, 0f, 14f),
                new Vector2(12f, 9f),
                new Color(0.32f, 0.27f, 0.46f, 0.65f));
        }

        private static void EnsureArea(
            Transform parent,
            string objectName,
            string areaId,
            string localizationKey,
            string defaultName,
            Vector3 position,
            Vector2 footprint,
            Color color)
        {
            Transform area = FindChildRecursive(parent, objectName);
            if (area == null)
            {
                var areaObject = new GameObject(objectName);
                areaObject.transform.SetParent(parent, false);
                area = areaObject.transform;
            }

            area.localPosition = position;
            WorldAreaMarker marker = area.GetComponent<WorldAreaMarker>()
                ?? area.gameObject.AddComponent<WorldAreaMarker>();
            marker.Configure(
                areaId,
                localizationKey,
                defaultName,
                footprint,
                color);
        }

        private static void EnsureLandmarks(Transform root)
        {
            EnsureLandmark(
                root,
                "Cangwu_AlchemyFurnace_Greybox",
                PrimitiveType.Cylinder,
                new Vector3(-4f, 0.7f, -13f),
                new Vector3(0.8f, 0.7f, 0.8f),
                new Color(0.34f, 0.18f, 0.12f, 1f));
            EnsureLandmark(
                root,
                "Cangwu_QuestBoard_Greybox",
                PrimitiveType.Cube,
                new Vector3(4f, 1f, -13f),
                new Vector3(1.5f, 2f, 0.25f),
                new Color(0.38f, 0.25f, 0.12f, 1f));
            GameObject entrance = EnsureLandmark(
                root,
                "Cangwu_BlackwindEntrance_Greybox",
                PrimitiveType.Cube,
                new Vector3(8f, 1.5f, 17f),
                new Vector3(3f, 3f, 0.5f),
                new Color(0.12f, 0.09f, 0.17f, 1f));
            if (entrance.GetComponent<BlackwindDungeonGate>() == null)
            {
                entrance.AddComponent<BlackwindDungeonGate>();
            }

            BoxCollider entranceTrigger = entrance.GetComponent<BoxCollider>();
            if (entranceTrigger != null)
            {
                entranceTrigger.enabled = true;
                entranceTrigger.isTrigger = true;
            }
        }

        private static void EnsureGatherables(Transform root)
        {
            Transform container = FindChildRecursive(root, GatherableRootName);
            if (container == null)
            {
                var containerObject = new GameObject(GatherableRootName);
                containerObject.transform.SetParent(root, false);
                container = containerObject.transform;
            }

            EnsureGatherable(container, GatheringContentIds.CangwuQingxin01,
                InventoryContentIds.QingxinGrass, new Vector3(-12f, 0.45f, -1f));
            EnsureGatherable(container, GatheringContentIds.CangwuQingxin02,
                InventoryContentIds.QingxinGrass, new Vector3(-9f, 0.45f, 4f));
            EnsureGatherable(container, GatheringContentIds.CangwuQingxin03,
                InventoryContentIds.QingxinGrass, new Vector3(-5f, 0.45f, 6f));
            EnsureGatherable(container, GatheringContentIds.CangwuQingxin04,
                InventoryContentIds.QingxinGrass, new Vector3(4f, 0.45f, 10f));
            EnsureGatherable(container, GatheringContentIds.CangwuSpiritDust01,
                InventoryContentIds.SpiritDust, new Vector3(-12f, 0.38f, 10f));
            EnsureGatherable(container, GatheringContentIds.CangwuSpiritDust02,
                InventoryContentIds.SpiritDust, new Vector3(-7f, 0.38f, 15f));
            EnsureGatherable(container, GatheringContentIds.CangwuSpiritDust03,
                InventoryContentIds.SpiritDust, new Vector3(7f, 0.38f, 16f));
            EnsureGatherable(container, GatheringContentIds.CangwuSpiritDust04,
                InventoryContentIds.SpiritDust, new Vector3(11f, 0.38f, 11f));
        }

        private static void EnsureGatherable(
            Transform parent,
            string nodeId,
            string itemId,
            Vector3 position)
        {
            Transform existing = FindChildRecursive(parent, nodeId);
            GameObject node;
            if (existing == null)
            {
                node = GameObject.CreatePrimitive(
                    itemId == InventoryContentIds.SpiritDust
                        ? PrimitiveType.Sphere
                        : PrimitiveType.Capsule);
                node.name = nodeId;
                node.transform.SetParent(parent, false);
            }
            else
            {
                node = existing.gameObject;
            }

            node.transform.localPosition = position;
            node.transform.localScale = itemId == InventoryContentIds.SpiritDust
                ? new Vector3(0.55f, 0.38f, 0.55f)
                : new Vector3(0.38f, 0.45f, 0.38f);
            ApplyMaterial(
                node.GetComponent<Renderer>(),
                itemId == InventoryContentIds.SpiritDust
                    ? new Color(0.48f, 0.76f, 0.84f, 1f)
                    : new Color(0.24f, 0.65f, 0.25f, 1f),
                nodeId);
            GatherableObject gatherable = node.GetComponent<GatherableObject>()
                ?? node.AddComponent<GatherableObject>();
            gatherable.Configure(
                nodeId,
                itemId,
                1,
                2,
                1,
                itemId == InventoryContentIds.SpiritDust ? 45f : 30f);
        }

        private static void EnsureChests(Transform root)
        {
            EnsureChest(root, MapContentIds.CangwuMistChest,
                new Vector3(-13f, 0.45f, 3f));
            EnsureChest(root, MapContentIds.CangwuCaveChest,
                new Vector3(-9f, 0.45f, 16f));
            EnsureChest(root, MapContentIds.CangwuTerraceChest,
                new Vector3(12f, 0.45f, 17f));
        }

        private static void EnsureChest(
            Transform parent,
            string chestId,
            Vector3 position)
        {
            Transform existing = FindChildRecursive(parent, chestId);
            GameObject chest;
            if (existing == null)
            {
                chest = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chest.name = chestId;
                chest.transform.SetParent(parent, false);
            }
            else
            {
                chest = existing.gameObject;
            }

            chest.transform.localPosition = position;
            chest.transform.localScale = new Vector3(0.9f, 0.6f, 0.65f);
            Collider collider = chest.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            ApplyMaterial(
                chest.GetComponent<Renderer>(),
                new Color(0.42f, 0.25f, 0.1f, 1f),
                chestId);
        }

        private static GameObject EnsureLandmark(
            Transform root,
            string objectName,
            PrimitiveType primitive,
            Vector3 position,
            Vector3 scale,
            Color color)
        {
            Transform landmark = FindChildRecursive(root, objectName);
            if (landmark == null)
            {
                GameObject created = GameObject.CreatePrimitive(primitive);
                created.name = objectName;
                created.transform.SetParent(root, false);
                landmark = created.transform;
            }

            landmark.localPosition = position;
            landmark.localScale = scale;
            Collider collider = landmark.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            ApplyMaterial(landmark.GetComponent<Renderer>(), color, objectName);
            return landmark.gameObject;
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

        private static void EnsureNavigation(GameObject root)
        {
            CangwuNavigationSurface surface =
                root.GetComponent<CangwuNavigationSurface>()
                ?? root.AddComponent<CangwuNavigationSurface>();
            if (!surface.IsBuilt)
            {
                surface.Rebuild();
            }
        }

        private static Transform FindChildRecursive(Transform current, string objectName)
        {
            if (current.name == objectName)
            {
                return current;
            }

            for (int index = 0; index < current.childCount; index++)
            {
                Transform result = FindChildRecursive(
                    current.GetChild(index),
                    objectName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static void ApplyMaterial(Renderer renderer, Color color, string name)
        {
            if (renderer == null)
            {
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");
            if (shader == null)
            {
                return;
            }

            string materialName = name + "_GreyboxMaterial";
            Material current = renderer.sharedMaterial;
            if (current != null && current.name == materialName)
            {
                current.color = color;
                return;
            }

            renderer.sharedMaterial = new Material(shader)
            {
                name = materialName,
                color = color,
                hideFlags = HideFlags.DontSave
            };
        }
    }
}
