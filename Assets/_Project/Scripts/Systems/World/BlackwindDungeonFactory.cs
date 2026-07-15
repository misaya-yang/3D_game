using UnityEngine;
using UnityEngine.SceneManagement;
using Wendao.Core;

namespace Wendao.Systems.World
{
    public static class BlackwindDungeonFactory
    {
        public const string RootName = "Dungeon_Blackwind_Greybox";
        public const string AreaRootName = "AreaMarkers_Blackwind";
        public const string PressurePlateName = "Blackwind_B1_PressurePlate";
        public const string EntryChestName = MapContentIds.BlackwindEntryChest;
        public const string BranchChestName = MapContentIds.BlackwindBranchChest;
        public const string DeepChestName = MapContentIds.BlackwindDeepChest;
        public const string LegacyBranchChestName = "Blackwind_B3_BranchChest";
        public const string BranchHazardName = "Blackwind_B3_SpikeHazard";
        public const string HealingSpringName = "Blackwind_B4_HealingSpring";
        public const string BossArenaName = "Blackwind_B5_BossArena";
        public const string ReturnGateName = "Blackwind_ReturnGate";
        public const int FloorCount = 5;
        public const int RequiredChestCount = 3;
        public const float FloorSpacing = 22f;

        public static Vector3 GetFloorCenter(int floor)
        {
            int clamped = Mathf.Clamp(floor, 1, FloorCount);
            return new Vector3(0f, 0f, (clamped - 1) * FloorSpacing);
        }

        public static string GetSpawnName(int floor)
        {
            return $"Blackwind_B{Mathf.Clamp(floor, 1, FloorCount)}_Spawn";
        }

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
            }

            EnsureLighting(root.transform);
            EnsureFloors(root.transform);
            EnsureMechanics(root.transform);
            EnsureNavigation(root);

            if (ServiceLocator.TryGet<IBlackwindDungeonService>(
                    out IBlackwindDungeonService dungeon)
                && !dungeon.IsRunActive)
            {
                dungeon.BeginRun();
            }
        }

        private static void EnsureFloors(Transform root)
        {
            Transform areas = FindChildRecursive(root, AreaRootName);
            if (areas == null)
            {
                var areaRoot = new GameObject(AreaRootName);
                areaRoot.transform.SetParent(root, false);
                areas = areaRoot.transform;
            }

            for (int floor = 1; floor <= FloorCount; floor++)
            {
                Vector3 center = GetFloorCenter(floor);
                EnsurePrimitive(
                    root,
                    $"Blackwind_B{floor}_Platform",
                    PrimitiveType.Cube,
                    center + Vector3.down * 0.5f,
                    new Vector3(18f, 1f, 18f),
                    new Color(0.12f, 0.14f, 0.15f, 1f),
                    true);
                EnsureArea(
                    areas,
                    floor,
                    center,
                    new Color(
                        0.16f + floor * 0.015f,
                        0.18f,
                        0.2f + floor * 0.01f,
                        0.55f));
                EnsureSpawn(root, floor, center + new Vector3(0f, 0f, -6f));
                EnsureFloorZone(root, floor, center);

                if (floor < FloorCount)
                {
                    EnsurePrimitive(
                        root,
                        $"Blackwind_B{floor}_Connector",
                        PrimitiveType.Cube,
                        center + new Vector3(0f, -0.5f, 11f),
                        new Vector3(5f, 1f, 4f),
                        new Color(0.1f, 0.11f, 0.12f, 1f),
                        true);
                    EnsureDoor(root, floor, center + new Vector3(0f, 1.5f, 11f));
                }
            }
        }

        private static void EnsureMechanics(Transform root)
        {
            Vector3 b1 = GetFloorCenter(1);
            EnsurePrimitive(
                root,
                EntryChestName,
                PrimitiveType.Cube,
                b1 + new Vector3(-5f, 0.5f, -4f),
                new Vector3(1.1f, 0.8f, 0.8f),
                new Color(0.46f, 0.3f, 0.12f, 1f),
                false);
            GameObject plate = EnsurePrimitive(
                root,
                PressurePlateName,
                PrimitiveType.Cylinder,
                b1 + new Vector3(0f, 0.1f, -1f),
                new Vector3(1.5f, 0.1f, 1.5f),
                new Color(0.35f, 0.3f, 0.16f, 1f),
                false);
            if (plate.GetComponent<BlackwindPressurePlate>() == null)
            {
                plate.AddComponent<BlackwindPressurePlate>();
            }

            Vector3 b3 = GetFloorCenter(3);
            EnsurePrimitive(
                root,
                BranchChestName,
                PrimitiveType.Cube,
                b3 + new Vector3(-5f, 0.5f, 1f),
                new Vector3(1.1f, 0.8f, 0.8f),
                new Color(0.46f, 0.3f, 0.12f, 1f),
                false);
            GameObject hazard = EnsurePrimitive(
                root,
                BranchHazardName,
                PrimitiveType.Cube,
                b3 + new Vector3(5f, 0.15f, 1f),
                new Vector3(4f, 0.3f, 5f),
                new Color(0.36f, 0.1f, 0.1f, 0.75f),
                true);
            if (hazard.GetComponent<BlackwindHazard>() == null)
            {
                hazard.AddComponent<BlackwindHazard>();
            }

            GameObject branchExit = EnsurePrimitive(
                root,
                "Blackwind_B3_ExitTrigger",
                PrimitiveType.Cube,
                b3 + new Vector3(0f, 1.2f, 7f),
                new Vector3(12f, 2.4f, 1.5f),
                new Color(0.2f, 0.35f, 0.3f, 0.18f),
                true);
            BlackwindFloorCompletionTrigger completion =
                branchExit.GetComponent<BlackwindFloorCompletionTrigger>()
                ?? branchExit.AddComponent<BlackwindFloorCompletionTrigger>();
            completion.Configure(3);
            Renderer exitRenderer = branchExit.GetComponent<Renderer>();
            if (exitRenderer != null)
            {
                exitRenderer.enabled = false;
            }

            Vector3 b4 = GetFloorCenter(4);
            EnsurePrimitive(
                root,
                DeepChestName,
                PrimitiveType.Cube,
                b4 + new Vector3(5f, 0.5f, 4f),
                new Vector3(1.1f, 0.8f, 0.8f),
                new Color(0.46f, 0.3f, 0.12f, 1f),
                false);
            GameObject spring = EnsurePrimitive(
                root,
                HealingSpringName,
                PrimitiveType.Cylinder,
                b4 + new Vector3(-4f, 0.25f, 1f),
                new Vector3(1.8f, 0.25f, 1.8f),
                new Color(0.18f, 0.5f, 0.55f, 0.9f),
                false);
            if (spring.GetComponent<BlackwindHealingSpring>() == null)
            {
                spring.AddComponent<BlackwindHealingSpring>();
            }

            Vector3 b5 = GetFloorCenter(5);
            EnsurePrimitive(
                root,
                BossArenaName,
                PrimitiveType.Cylinder,
                b5 + new Vector3(0f, 0.08f, 1f),
                new Vector3(6.5f, 0.08f, 6.5f),
                new Color(0.18f, 0.08f, 0.08f, 0.7f),
                false);
            GameObject returnGate = EnsurePrimitive(
                root,
                ReturnGateName,
                PrimitiveType.Cube,
                b5 + new Vector3(0f, 1.5f, 8f),
                new Vector3(3f, 3f, 0.6f),
                new Color(0.2f, 0.45f, 0.48f, 0.8f),
                true);
            if (returnGate.GetComponent<BlackwindReturnGate>() == null)
            {
                returnGate.AddComponent<BlackwindReturnGate>();
            }
        }

        private static void EnsureArea(
            Transform parent,
            int floor,
            Vector3 center,
            Color color)
        {
            string objectName = $"Area_Blackwind_B{floor}";
            Transform area = FindChildRecursive(parent, objectName);
            if (area == null)
            {
                var areaObject = new GameObject(objectName);
                areaObject.transform.SetParent(parent, false);
                area = areaObject.transform;
            }

            area.localPosition = center;
            WorldAreaMarker marker = area.GetComponent<WorldAreaMarker>()
                ?? area.gameObject.AddComponent<WorldAreaMarker>();
            marker.Configure(
                $"area_blackwind_b{floor}",
                $"area_name_blackwind_b{floor}",
                $"黑风地宫·{GetChineseFloor(floor)}层",
                new Vector2(17f, 17f),
                color);
        }

        private static void EnsureSpawn(
            Transform root,
            int floor,
            Vector3 position)
        {
            string objectName = GetSpawnName(floor);
            Transform spawn = FindChildRecursive(root, objectName);
            if (spawn == null)
            {
                var spawnObject = new GameObject(objectName);
                spawnObject.transform.SetParent(root, false);
                spawn = spawnObject.transform;
            }

            spawn.localPosition = position;
            RespawnPoint point = spawn.GetComponent<RespawnPoint>()
                ?? spawn.gameObject.AddComponent<RespawnPoint>();
            point.Configure(MapContentIds.GetBlackwindSpawnId(floor));
        }

        private static void EnsureFloorZone(
            Transform root,
            int floor,
            Vector3 center)
        {
            string objectName = $"Blackwind_B{floor}_FloorZone";
            Transform zone = FindChildRecursive(root, objectName);
            if (zone == null)
            {
                var zoneObject = new GameObject(
                    objectName,
                    typeof(BoxCollider));
                zoneObject.transform.SetParent(root, false);
                zone = zoneObject.transform;
            }

            zone.localPosition = center + Vector3.up;
            BoxCollider trigger = zone.GetComponent<BoxCollider>();
            if (trigger == null)
            {
                trigger = zone.gameObject.AddComponent<BoxCollider>();
            }

            trigger.isTrigger = true;
            trigger.size = new Vector3(16f, 3f, 16f);
            BlackwindFloorZone floorZone = zone.GetComponent<BlackwindFloorZone>();
            if (floorZone == null)
            {
                floorZone = zone.gameObject.AddComponent<BlackwindFloorZone>();
            }

            floorZone.Configure(floor);
        }

        private static void EnsureDoor(
            Transform root,
            int floor,
            Vector3 position)
        {
            GameObject door = EnsurePrimitive(
                root,
                $"Blackwind_B{floor}_SealDoor",
                PrimitiveType.Cube,
                position,
                new Vector3(5f, 3f, 0.7f),
                new Color(0.23f, 0.17f, 0.12f, 1f),
                true);
            BlackwindDungeonDoor controller =
                door.GetComponent<BlackwindDungeonDoor>()
                ?? door.AddComponent<BlackwindDungeonDoor>();
            controller.Configure(floor);
        }

        private static GameObject EnsurePrimitive(
            Transform root,
            string objectName,
            PrimitiveType primitive,
            Vector3 localPosition,
            Vector3 localScale,
            Color color,
            bool keepCollider)
        {
            Transform existing = FindChildRecursive(root, objectName);
            GameObject result;
            if (existing == null)
            {
                result = GameObject.CreatePrimitive(primitive);
                result.name = objectName;
                result.transform.SetParent(root, false);
            }
            else
            {
                result = existing.gameObject;
            }

            result.transform.localPosition = localPosition;
            result.transform.localScale = localScale;
            Collider collider = result.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = keepCollider;
            }

            ApplyMaterial(result.GetComponent<Renderer>(), color, objectName);
            return result;
        }

        private static void EnsureLighting(Transform root)
        {
            if (Camera.main == null)
            {
                var cameraObject = new GameObject("Greybox_BlackwindCamera");
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetParent(root, false);
                cameraObject.transform.localPosition = new Vector3(0f, 12f, -14f);
                cameraObject.transform.LookAt(GetFloorCenter(1));
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.055f, 0.065f, 0.07f, 1f);
            }

            Transform existing = FindChildRecursive(root, "Greybox_BlackwindLight");
            if (existing == null)
            {
                var lightObject = new GameObject("Greybox_BlackwindLight");
                lightObject.transform.SetParent(root, false);
                lightObject.transform.localRotation = Quaternion.Euler(58f, -25f, 0f);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 0.72f;
                light.color = new Color(0.72f, 0.82f, 0.82f, 1f);
                light.shadows = LightShadows.Soft;
            }
        }

        private static void EnsureNavigation(GameObject root)
        {
            BlackwindNavigationSurface surface =
                root.GetComponent<BlackwindNavigationSurface>()
                ?? root.AddComponent<BlackwindNavigationSurface>();
            if (!surface.IsBuilt)
            {
                surface.Rebuild();
            }
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

        private static Transform FindChildRecursive(Transform current, string name)
        {
            if (current.name == name)
            {
                return current;
            }

            for (int index = 0; index < current.childCount; index++)
            {
                Transform result = FindChildRecursive(current.GetChild(index), name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static string GetChineseFloor(int floor)
        {
            switch (floor)
            {
                case 1: return "一";
                case 2: return "二";
                case 3: return "三";
                case 4: return "四";
                default: return "五";
            }
        }

        private static void ApplyMaterial(Renderer renderer, Color color, string id)
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

            string materialName = id + "_GreyboxMaterial";
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
