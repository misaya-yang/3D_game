using UnityEngine;
using UnityEngine.SceneManagement;
using Wendao.Systems.Crafting;
using Wendao.Systems.Inventory;

namespace Wendao.Systems.World
{
    public static class QingshiGreyboxFactory
    {
        public const string RootName = "Map_Qingshi_Greybox";
        public const string GroundName = "Greybox_Ground";
        public const string DefaultSpawnName = "DefaultSpawn";
        public const string DefaultRespawnPointId = "teleport_qingshi_town";
        public const string GatherableRootName = "Area_HerbCreek_Gatherables";
        public const string AreaRootName = "AreaMarkers_Qingshi";
        public const string TownAreaName = "Area_QingshiTown";
        public const string TrainingAreaName = "Area_TrainingGround";
        public const string WildernessAreaName = "Area_EastWilderness";
        public const string HerbCreekAreaName = "Area_HerbCreek";
        public const string SecretPathAreaName = "Area_SecretPathEntrance";
        public const string SafeZoneObjectName = "SafeZone_QingshiTownTraining";
        public const string CangwuPathGateObjectName = "Gate_CangwuSecretPath";
        public const string SafeZoneId = "safezone_qingshi_town_training";
        public const string ChestWildernessId = "chest_qingshi_wilderness_01";
        public const string ChestSecretPathId = "chest_qingshi_secret_path_01";
        public const int RequiredGatherableCount = 6;
        public const int RequiredAreaCount = 5;
        public const int RequiredChestCount = 2;

        public static void EnsureCreated(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            GameObject existingRoot = FindRoot(scene, RootName);
            if (existingRoot != null)
            {
                EnsureDefaultRespawnPoint(existingRoot.transform);
                EnsureGatherables(existingRoot.transform);
                EnsureAreas(existingRoot.transform);
                EnsureNavigation(existingRoot);
                return;
            }

            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = GroundName;
            ground.transform.SetParent(root.transform, false);
            ground.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(30f, 1f, 30f);
            ApplyGroundMaterial(ground.GetComponent<Renderer>());

            var spawn = new GameObject(DefaultSpawnName);
            spawn.transform.SetParent(root.transform, false);
            spawn.transform.localPosition = Vector3.zero;
            ConfigureRespawnPoint(spawn);

            if (Camera.main == null)
            {
                var cameraObject = new GameObject("Greybox_Camera");
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetParent(root.transform, false);
                cameraObject.transform.localPosition = new Vector3(0f, 9f, -12f);
                cameraObject.transform.LookAt(new Vector3(0f, 0f, 2f));
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.13f, 0.17f, 0.16f, 1f);
            }

            var lightObject = new GameObject("Greybox_Sun");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(48f, -30f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;

            EnsureGatherables(root.transform);
            EnsureAreas(root.transform);
            EnsureNavigation(root);
        }

        private static void EnsureAreas(Transform root)
        {
            Transform container = FindChildRecursive(root, AreaRootName);
            if (container == null)
            {
                var areaRoot = new GameObject(AreaRootName);
                areaRoot.transform.SetParent(root, false);
                container = areaRoot.transform;
            }

            EnsureArea(
                container,
                TownAreaName,
                "area_qingshi_town",
                "area_name_qingshi_town",
                "青石镇",
                new Vector3(0f, 0f, -9f),
                new Vector2(10f, 6f),
                new Color(0.33f, 0.36f, 0.4f, 0.65f));
            EnsureArea(
                container,
                TrainingAreaName,
                "area_qingshi_training_ground",
                "area_name_qingshi_training_ground",
                "习武坪",
                new Vector3(0f, 0f, -3f),
                new Vector2(8f, 5f),
                new Color(0.46f, 0.35f, 0.22f, 0.65f));
            EnsureArea(
                container,
                WildernessAreaName,
                "area_qingshi_east_wilderness",
                "area_name_qingshi_east_wilderness",
                "东郊荒野",
                new Vector3(8f, 0f, 4f),
                new Vector2(10f, 11f),
                new Color(0.36f, 0.3f, 0.2f, 0.65f));
            EnsureArea(
                container,
                HerbCreekAreaName,
                "area_qingshi_herb_creek",
                "area_name_qingshi_herb_creek",
                "灵草涧",
                new Vector3(-4f, 0f, 8f),
                new Vector2(12f, 7f),
                new Color(0.16f, 0.45f, 0.31f, 0.65f));
            EnsureArea(
                container,
                SecretPathAreaName,
                "area_qingshi_secret_path",
                "area_name_qingshi_secret_path",
                "秘径入口",
                new Vector3(-11f, 0f, -5f),
                new Vector2(5f, 7f),
                new Color(0.25f, 0.2f, 0.34f, 0.65f));

            EnsureSafeZone(root);
            EnsureCangwuPathGate(root);
            EnsureChest(
                container,
                ChestWildernessId,
                new Vector3(12f, 0.45f, 3f));
            EnsureChest(
                container,
                ChestSecretPathId,
                new Vector3(-12f, 0.45f, -6f));
        }

        private static void EnsureCangwuPathGate(Transform root)
        {
            Transform existing = FindChildRecursive(
                root,
                CangwuPathGateObjectName);
            if (existing == null)
            {
                GameObject gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                gate.name = CangwuPathGateObjectName;
                gate.transform.SetParent(root, false);
                existing = gate.transform;
            }

            existing.localPosition = new Vector3(-13f, 1.5f, -5f);
            existing.localScale = new Vector3(3f, 3f, 0.6f);
            CangwuPathGate pathGate = existing.GetComponent<CangwuPathGate>()
                ?? existing.gameObject.AddComponent<CangwuPathGate>();
            BoxCollider trigger = existing.GetComponent<BoxCollider>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
                trigger.size = Vector3.one;
            }

            ApplyGatherableMaterial(
                existing.GetComponent<Renderer>(),
                new Color(0.26f, 0.2f, 0.34f, 0.9f),
                MapContentIds.CangwuPathOpenFlag);
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
            Transform existing = FindChildRecursive(parent, objectName);
            WorldAreaMarker marker;
            if (existing == null)
            {
                var areaObject = new GameObject(objectName);
                areaObject.transform.SetParent(parent, false);
                existing = areaObject.transform;
            }

            existing.localPosition = position;
            marker = existing.GetComponent<WorldAreaMarker>()
                ?? existing.gameObject.AddComponent<WorldAreaMarker>();
            marker.Configure(
                areaId,
                localizationKey,
                defaultName,
                footprint,
                color);
        }

        private static void EnsureSafeZone(Transform root)
        {
            Transform existing = FindChildRecursive(root, SafeZoneObjectName);
            if (existing == null)
            {
                var zoneObject = new GameObject(SafeZoneObjectName);
                zoneObject.transform.SetParent(root, false);
                existing = zoneObject.transform;
            }

            existing.localPosition = new Vector3(0f, 1f, -6f);
            SafeZone zone = existing.GetComponent<SafeZone>()
                ?? existing.gameObject.AddComponent<SafeZone>();
            zone.Configure(
                SafeZoneId,
                new Vector3(12f, 4f, 12f),
                SafeZone.DefaultRecoveryMultiplier);
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

            ApplyGatherableMaterial(
                chest.GetComponent<Renderer>(),
                new Color(0.42f, 0.25f, 0.1f, 1f),
                chestId);
        }

        private static void EnsureNavigation(GameObject root)
        {
            QingshiNavigationSurface surface =
                root.GetComponent<QingshiNavigationSurface>();
            if (surface == null)
            {
                surface = root.AddComponent<QingshiNavigationSurface>();
            }

            if (!surface.IsBuilt)
            {
                surface.Rebuild();
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

            EnsureGatherable(
                container,
                GatheringContentIds.QingshiQingxin01,
                InventoryContentIds.QingxinGrass,
                1,
                2,
                30f,
                new Vector3(-9f, 0.45f, 6f),
                new Color(0.24f, 0.65f, 0.25f, 1f));
            EnsureGatherable(
                container,
                GatheringContentIds.QingshiQingxin02,
                InventoryContentIds.QingxinGrass,
                1,
                2,
                30f,
                new Vector3(-6f, 0.45f, 8.5f),
                new Color(0.27f, 0.7f, 0.3f, 1f));
            EnsureGatherable(
                container,
                GatheringContentIds.QingshiQingxin03,
                InventoryContentIds.QingxinGrass,
                1,
                2,
                30f,
                new Vector3(-2.5f, 0.45f, 10f),
                new Color(0.22f, 0.62f, 0.25f, 1f));
            EnsureGatherable(
                container,
                GatheringContentIds.QingshiQingxin04,
                InventoryContentIds.QingxinGrass,
                1,
                2,
                30f,
                new Vector3(2f, 0.45f, 9.5f),
                new Color(0.3f, 0.72f, 0.32f, 1f));
            EnsureGatherable(
                container,
                GatheringContentIds.QingshiSpiritDust01,
                InventoryContentIds.SpiritDust,
                1,
                2,
                45f,
                new Vector3(6f, 0.38f, 7.5f),
                new Color(0.42f, 0.7f, 0.78f, 1f));
            EnsureGatherable(
                container,
                GatheringContentIds.QingshiSpiritDust02,
                InventoryContentIds.SpiritDust,
                1,
                2,
                45f,
                new Vector3(9f, 0.38f, 5.5f),
                new Color(0.48f, 0.76f, 0.84f, 1f));
        }

        private static void EnsureGatherable(
            Transform parent,
            string nodeId,
            string itemId,
            int minCount,
            int maxCount,
            float respawnSeconds,
            Vector3 position,
            Color color)
        {
            Transform existing = FindChildRecursive(parent, nodeId);
            GatherableObject gatherable;
            if (existing == null)
            {
                GameObject node = GameObject.CreatePrimitive(
                    itemId == InventoryContentIds.SpiritDust
                        ? PrimitiveType.Sphere
                        : PrimitiveType.Capsule);
                node.name = nodeId;
                node.transform.SetParent(parent, false);
                node.transform.localPosition = position;
                node.transform.localScale = itemId == InventoryContentIds.SpiritDust
                    ? new Vector3(0.55f, 0.38f, 0.55f)
                    : new Vector3(0.38f, 0.45f, 0.38f);
                ApplyGatherableMaterial(node.GetComponent<Renderer>(), color, nodeId);
                gatherable = node.AddComponent<GatherableObject>();
            }
            else
            {
                gatherable = existing.GetComponent<GatherableObject>();
                if (gatherable == null)
                {
                    gatherable = existing.gameObject.AddComponent<GatherableObject>();
                }
            }

            gatherable.Configure(
                nodeId,
                itemId,
                minCount,
                maxCount,
                1,
                respawnSeconds);
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

        private static void EnsureDefaultRespawnPoint(Transform root)
        {
            Transform spawn = FindChildRecursive(root, DefaultSpawnName);
            if (spawn == null)
            {
                var spawnObject = new GameObject(DefaultSpawnName);
                spawnObject.transform.SetParent(root, false);
                spawn = spawnObject.transform;
            }

            ConfigureRespawnPoint(spawn.gameObject);
        }

        private static void ConfigureRespawnPoint(GameObject spawnObject)
        {
            RespawnPoint point = spawnObject.GetComponent<RespawnPoint>();
            if (point == null)
            {
                point = spawnObject.AddComponent<RespawnPoint>();
            }

            point.Configure(DefaultRespawnPointId);
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

        private static void ApplyGroundMaterial(Renderer renderer)
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

            const string materialName = "Greybox_Ground_Runtime";
            Material current = renderer.sharedMaterial;
            Color color = new Color(0.28f, 0.36f, 0.29f, 1f);
            if (current != null && current.name == materialName)
            {
                current.color = color;
                return;
            }

            var material = new Material(shader)
            {
                name = materialName,
                color = color,
                hideFlags = HideFlags.DontSave
            };
            renderer.sharedMaterial = material;
        }

        private static void ApplyGatherableMaterial(
            Renderer renderer,
            Color color,
            string nodeId)
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

            string materialName = nodeId + "_RuntimeMaterial";
            Material current = renderer.sharedMaterial;
            if (current != null && current.name == materialName)
            {
                current.color = color;
                return;
            }

            var material = new Material(shader)
            {
                name = materialName,
                color = color,
                hideFlags = HideFlags.DontSave
            };
            renderer.sharedMaterial = material;
        }
    }
}
