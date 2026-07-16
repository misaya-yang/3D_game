using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Wendao.Systems.Crafting;
using Wendao.Systems.Inventory;
using Wendao.Systems.World;

namespace Wendao.Entities.Visuals
{
    /// <summary>
    /// Deterministic, collider-free dressing for the three MVP gameplay maps.
    /// The existing greybox remains the authoritative gameplay/navigation layer.
    /// </summary>
    public static class BudgetWorldArtBootstrap
    {
        public const string RootPrefix = "BudgetArt_";
        public const int QingshiMinimumDecorationCount = 45;
        public const int CangwuMinimumDecorationCount = 45;
        public const int BlackwindMinimumDecorationCount = 25;

        private static Mesh _hipRoofMesh;
        private static Material _waterMaterial;
        private static readonly Dictionary<string, Material> SkyboxMaterials =
            new Dictionary<string, Material>();

        public static void Install()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureForScene(SceneManager.GetActiveScene());
        }

        public static GameObject EnsureForScene(Scene scene)
        {
            if (!IsGameplayScene(scene))
            {
                return null;
            }

            string rootName = RootPrefix + scene.name;
            GameObject existing = FindSceneRoot(scene, rootName);
            if (existing != null)
            {
                ApplyEnvironment(scene);
                UpgradeGameplaySurfaces(scene);
                UpgradeExistingInteractables(scene);
                RepairBrokenMaterials(scene);
                return existing;
            }

            var root = new GameObject(rootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            if (scene.name == SceneLoader.DefaultMapSceneName)
            {
                BuildQingshi(root.transform);
            }
            else if (scene.name == SceneLoader.CangwuMapSceneName)
            {
                BuildCangwu(root.transform);
            }
            else
            {
                BuildBlackwind(root.transform);
            }

            UpgradeExistingInteractables(scene);
            ApplyEnvironment(scene);
            UpgradeGameplaySurfaces(scene);
            RepairBrokenMaterials(scene);
            return root;
        }

        public static int CountDecorations(Scene scene)
        {
            GameObject root = FindSceneRoot(scene, RootPrefix + scene.name);
            return root == null ? 0 : CountDescendants(root.transform) - 1;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene);
        }

        private static bool IsGameplayScene(Scene scene)
        {
            return scene.IsValid()
                && scene.isLoaded
                && (scene.name == SceneLoader.DefaultMapSceneName
                    || scene.name == SceneLoader.CangwuMapSceneName
                    || scene.name == SceneLoader.BlackwindDungeonSceneName);
        }

        private static void BuildQingshi(Transform root)
        {
            CreateHouse(root, "House_Herbalist", new Vector3(-10.5f, 0f, -10.5f),
                12f, new Color(0.34f, 0.3f, 0.23f, 1f));
            CreateHouse(root, "House_Inn", new Vector3(8.8f, 0f, -10.8f),
                -8f, new Color(0.4f, 0.27f, 0.19f, 1f));
            CreateHouse(root, "House_Smithy", new Vector3(12.1f, 0f, -4.6f),
                -78f, new Color(0.29f, 0.28f, 0.25f, 1f));
            CreateHouse(root, "House_Farm", new Vector3(-12f, 0f, -2.5f),
                82f, new Color(0.31f, 0.34f, 0.25f, 1f));
            CreateVillageGate(root, "Gate_Qingshi", new Vector3(0f, 0f, -12.8f), 0f);
            CreateSurfacePatch(
                root,
                "Qingshi_MainRoad",
                new Vector3(0f, 0.025f, -4.7f),
                new Vector3(3.1f, 0.035f, 16.5f),
                0f,
                BudgetArtCatalog.QingshiSurface,
                new Color(0.72f, 0.72f, 0.62f, 1f),
                new Vector2(1.8f, 8f));
            CreateSurfacePatch(
                root,
                "Qingshi_CreekRoad",
                new Vector3(-2.6f, 0.028f, 5.2f),
                new Vector3(2.6f, 0.035f, 8.6f),
                -34f,
                BudgetArtCatalog.QingshiSurface,
                new Color(0.68f, 0.7f, 0.6f, 1f),
                new Vector2(1.5f, 4.5f));

            Vector3[] treePositions =
            {
                new Vector3(-14f, 0f, -13f), new Vector3(-6f, 0f, -14f),
                new Vector3(5f, 0f, -14f), new Vector3(14f, 0f, -12f),
                new Vector3(14f, 0f, -1f), new Vector3(14f, 0f, 8f),
                new Vector3(11f, 0f, 14f), new Vector3(3f, 0f, 14f),
                new Vector3(-5f, 0f, 14f), new Vector3(-13f, 0f, 13f),
                new Vector3(-14f, 0f, 6f), new Vector3(-14f, 0f, 1f)
            };
            AddResourceScatter(
                root,
                "QingshiTree",
                treePositions,
                BudgetArtCatalog.QingshiTreeResources,
                4.2f,
                0.9f);

            Vector3[] rockPositions =
            {
                new Vector3(10f, 0f, 2f), new Vector3(13f, 0f, 4f),
                new Vector3(9f, 0f, 11f), new Vector3(-11f, 0f, 9f),
                new Vector3(-9f, 0f, -6f), new Vector3(6f, 0f, 12f)
            };
            AddResourceScatter(
                root,
                "QingshiRock",
                rockPositions,
                BudgetArtCatalog.RockResources,
                1.25f,
                0.65f,
                new Color(0.38f, 0.48f, 0.42f, 1f));

            CreateCreek(root);
            AddResource(root, "Bridge_HerbCreek", BudgetArtCatalog.Nature("bridge_wood"),
                new Vector3(-4f, 0.02f, 8f), 3.2f, true, 90f);
            for (int index = 0; index < 8; index++)
            {
                AddResource(
                    root,
                    $"PathStone_{index + 1:00}",
                    BudgetArtCatalog.Nature(index % 2 == 0
                        ? "path_stone"
                        : "path_stoneCircle"),
                    new Vector3(0f, 0.02f, -10f + index * 1.7f),
                    1.1f,
                    true,
                    index * 31f,
                    new Color(0.42f, 0.5f, 0.44f, 1f));
            }

            CreateHerbGarden(root);
            AddResource(root, "Campfire_Training", BudgetArtCatalog.Nature("campfire_stones"),
                new Vector3(4.2f, 0f, -3.8f), 1.1f, true, 0f,
                new Color(0.44f, 0.49f, 0.43f, 1f));
            AddResource(root, "Tent_Training", BudgetArtCatalog.Nature("tent_detailedOpen"),
                new Vector3(6.5f, 0f, -6.2f), 2.4f, false, -30f);
            AddResource(root, "Logs_Training", BudgetArtCatalog.Nature("log_stack"),
                new Vector3(4.7f, 0f, -6.2f), 1.3f, true, 22f);
            AddResource(root, "VillageSign", BudgetArtCatalog.Nature("sign"),
                new Vector3(2.4f, 0f, -11.2f), 1.55f, false, 0f);
            AddResource(root, "HerbalistPotLarge", BudgetArtCatalog.Nature("pot_large"),
                new Vector3(-8.7f, 0f, -8.8f), 0.75f, false, 0f);
            AddResource(root, "HerbalistPotSmall", BudgetArtCatalog.Nature("pot_small"),
                new Vector3(-9.5f, 0f, -8.7f), 0.48f, false, 15f);
        }

        private static void BuildCangwu(Transform root)
        {
            CreateVillageGate(
                root,
                "Gate_CangwuMountain",
                new Vector3(0f, 0f, -10.4f),
                0f,
                0.82f);
            CreateShrine(root, "Shrine_ThunderTerrace", new Vector3(8f, 0f, 14f));
            CreateSurfacePatch(
                root,
                "Cangwu_Road_Lower",
                new Vector3(0f, 0.025f, -7.5f),
                new Vector3(3.2f, 0.035f, 14f),
                0f,
                BudgetArtCatalog.CangwuSurface,
                new Color(0.54f, 0.61f, 0.56f, 1f),
                new Vector2(1.8f, 6f));
            CreateSurfacePatch(
                root,
                "Cangwu_Road_Middle",
                new Vector3(2.8f, 0.028f, 3.8f),
                new Vector3(3f, 0.035f, 11.5f),
                28f,
                BudgetArtCatalog.CangwuSurface,
                new Color(0.5f, 0.58f, 0.53f, 1f),
                new Vector2(1.7f, 5f));
            CreateSurfacePatch(
                root,
                "Cangwu_Road_Upper",
                new Vector3(5.7f, 0.03f, 12f),
                new Vector3(3f, 0.035f, 10f),
                -12f,
                BudgetArtCatalog.CangwuSurface,
                new Color(0.48f, 0.56f, 0.52f, 1f),
                new Vector2(1.6f, 4.5f));

            Vector3[] treePositions =
            {
                new Vector3(-17f, 0f, -18f), new Vector3(-11f, 0f, -17f),
                new Vector3(9f, 0f, -18f), new Vector3(17f, 0f, -15f),
                new Vector3(17f, 0f, -6f), new Vector3(16f, 0f, 4f),
                new Vector3(17f, 0f, 13f), new Vector3(14f, 0f, 21f),
                new Vector3(5f, 0f, 22f), new Vector3(-3f, 0f, 22f),
                new Vector3(-12f, 0f, 21f), new Vector3(-17f, 0f, 16f),
                new Vector3(-17f, 0f, 7f), new Vector3(-17f, 0f, -2f),
                new Vector3(-16f, 0f, -11f), new Vector3(12f, 0f, 7f)
            };
            AddResourceScatter(
                root,
                "CangwuPine",
                treePositions,
                BudgetArtCatalog.CangwuTreeResources,
                5.4f,
                1.1f);

            Vector3[] rockPositions =
            {
                new Vector3(-13f, 0f, -8f), new Vector3(-10f, 0f, -2f),
                new Vector3(-14f, 0f, 7f), new Vector3(-11f, 0f, 14f),
                new Vector3(-4f, 0f, 19f), new Vector3(4f, 0f, 19f),
                new Vector3(13f, 0f, 17f), new Vector3(14f, 0f, 10f),
                new Vector3(13f, 0f, 1f), new Vector3(9f, 0f, -8f),
                new Vector3(4f, 0f, -4f), new Vector3(-5f, 0f, 8f)
            };
            AddResourceScatter(
                root,
                "CangwuRock",
                rockPositions,
                BudgetArtCatalog.RockResources,
                1.8f,
                0.8f,
                new Color(0.37f, 0.49f, 0.5f, 1f));

            AddResource(root, "Cave_Cangwu", BudgetArtCatalog.Nature("cliff_cave_rock"),
                new Vector3(-8f, 0f, 16.8f), 6f, true, 180f,
                new Color(0.34f, 0.46f, 0.48f, 1f));
            AddResource(root, "Cliff_Cangwu", BudgetArtCatalog.Nature("cliff_rock"),
                new Vector3(-13f, 0f, 12f), 6.5f, true, 90f,
                new Color(0.32f, 0.44f, 0.47f, 1f));
            AddResource(root, "DamagedColumn", BudgetArtCatalog.Nature("statue_columnDamaged"),
                new Vector3(7f, 0f, 12f), 3.2f, false, 0f);
            AddResource(root, "Obelisk", BudgetArtCatalog.Nature("statue_obelisk"),
                new Vector3(10.5f, 0f, 13f), 3.8f, false, 18f);

            for (int index = 0; index < 7; index++)
            {
                AddResource(
                    root,
                    $"Bamboo_{index + 1:00}",
                    BudgetArtCatalog.Nature("crops_bambooStageB"),
                    new Vector3(-12f + index * 1.25f, 0f, 3.5f + index % 2),
                    2.6f + (index % 3) * 0.25f,
                    false,
                    index * 37f);
            }

            AddResource(root, "CangwuCampfire", BudgetArtCatalog.Nature("campfire_stones"),
                new Vector3(3f, 0f, -12f), 1.1f, true, 0f,
                new Color(0.4f, 0.47f, 0.46f, 1f));
            AddResource(root, "CangwuTent", BudgetArtCatalog.Nature("tent_detailedOpen"),
                new Vector3(6f, 0f, -12f), 2.5f, false, 35f);
        }

        private static void BuildBlackwind(Transform root)
        {
            for (int floor = 1; floor <= BlackwindDungeonFactory.FloorCount; floor++)
            {
                Vector3 center = BlackwindDungeonFactory.GetFloorCenter(floor);
                string room = BudgetArtCatalog.DungeonRoomResources[
                    (floor - 1) % BudgetArtCatalog.DungeonRoomResources.Length];
                AddResource(
                    root,
                    $"DungeonRoom_B{floor}",
                    room,
                    center,
                    17f,
                    true,
                    floor % 2 == 0 ? 180f : 0f,
                    new Color(0.5f, 0.58f, 0.58f, 1f));

                if (floor < BlackwindDungeonFactory.FloorCount)
                {
                    AddResource(
                        root,
                        $"DungeonGate_B{floor}",
                        BudgetArtCatalog.Dungeon("gate-door"),
                        center + new Vector3(0f, 0f, 10.7f),
                        4.8f,
                        true,
                        0f,
                        new Color(0.42f, 0.36f, 0.31f, 1f));
                }

                CreateBrazier(root, $"Brazier_B{floor}_L",
                    center + new Vector3(-6.5f, 0f, -5f));
                CreateBrazier(root, $"Brazier_B{floor}_R",
                    center + new Vector3(6.5f, 0f, -5f));
                AddResource(
                    root,
                    $"DungeonStone_B{floor}",
                    BudgetArtCatalog.RockResources[floor % BudgetArtCatalog.RockResources.Length],
                    center + new Vector3(floor % 2 == 0 ? -6f : 6f, 0f, 5.5f),
                    1.65f,
                    false,
                    floor * 43f,
                    new Color(0.45f, 0.5f, 0.5f, 1f));
            }

            Vector3 bossCenter = BlackwindDungeonFactory.GetFloorCenter(5);
            AddResource(root, "BossObelisk_Left", BudgetArtCatalog.Nature("statue_obelisk"),
                bossCenter + new Vector3(-6f, 0f, 4f), 4.2f, false, 0f);
            AddResource(root, "BossObelisk_Right", BudgetArtCatalog.Nature("statue_obelisk"),
                bossCenter + new Vector3(6f, 0f, 4f), 4.2f, false, 0f);
        }

        private static void ApplyEnvironment(Scene scene)
        {
            WorldVisualProfile profile =
                WorldEnvironmentProfiles.GetVisualProfile(scene.name);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = profile.AmbientSkyColor;
            RenderSettings.ambientEquatorColor = profile.AmbientEquatorColor;
            RenderSettings.ambientGroundColor = profile.AmbientGroundColor;
            RenderSettings.ambientIntensity = profile.AmbientIntensity;
            RenderSettings.reflectionIntensity = 0.28f;
            RenderSettings.fog = profile.BaseFogDensity > 0.0001f;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = profile.BaseFogDensity;
            RenderSettings.fogColor = profile.FogColor;

            Camera camera = Camera.main;
            if (camera != null && camera.gameObject.scene == scene)
            {
                camera.allowHDR = true;
                camera.backgroundColor = profile.HorizonColor;
                camera.farClipPlane = Mathf.Max(camera.farClipPlane, 140f);
                if (profile.UseSkybox)
                {
                    Material skybox = GetSkyboxMaterial(scene.name, profile);
                    if (skybox != null)
                    {
                        RenderSettings.skybox = skybox;
                        camera.clearFlags = CameraClearFlags.Skybox;
                    }
                    else
                    {
                        camera.clearFlags = CameraClearFlags.SolidColor;
                    }
                }
                else
                {
                    RenderSettings.skybox = null;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                }
            }
        }

        private static Material GetSkyboxMaterial(
            string sceneName,
            WorldVisualProfile profile)
        {
            if (SkyboxMaterials.TryGetValue(sceneName, out Material material)
                && material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Skybox/Procedural");
            if (shader == null)
            {
                return null;
            }

            material = new Material(shader)
            {
                name = $"BudgetSky_{sceneName}",
                hideFlags = HideFlags.DontSave
            };
            if (material.HasProperty("_SkyTint"))
            {
                material.SetColor("_SkyTint", profile.SkyTint);
            }
            if (material.HasProperty("_GroundColor"))
            {
                material.SetColor("_GroundColor", profile.HorizonColor * 0.56f);
            }
            if (material.HasProperty("_Exposure"))
            {
                material.SetFloat("_Exposure", profile.SkyExposure);
            }
            if (material.HasProperty("_AtmosphereThickness"))
            {
                material.SetFloat("_AtmosphereThickness", 0.62f);
            }
            if (material.HasProperty("_SunSize"))
            {
                material.SetFloat("_SunSize", 0.025f);
            }
            SkyboxMaterials[sceneName] = material;
            return material;
        }

        private static void UpgradeGameplaySurfaces(Scene scene)
        {
            if (scene.name == SceneLoader.DefaultMapSceneName)
            {
                ApplySurfaceMaterial(
                    FindSceneTransform(scene, QingshiGreyboxFactory.GroundName),
                    BudgetArtCatalog.QingshiSurface,
                    new Color(0.62f, 0.7f, 0.56f, 1f),
                    new Vector2(9f, 9f),
                    BudgetMaterialProfile.Environment);
                return;
            }

            if (scene.name == SceneLoader.CangwuMapSceneName)
            {
                ApplySurfaceMaterial(
                    FindSceneTransform(scene, CangwuGreyboxFactory.GroundName),
                    BudgetArtCatalog.CangwuSurface,
                    new Color(0.55f, 0.63f, 0.58f, 1f),
                    new Vector2(11f, 13f),
                    BudgetMaterialProfile.Environment);
                return;
            }

            for (int floor = 1; floor <= BlackwindDungeonFactory.FloorCount; floor++)
            {
                ApplySurfaceMaterial(
                    FindSceneTransform(scene, $"Blackwind_B{floor}_Platform"),
                    BudgetArtCatalog.BlackwindSurface,
                    new Color(0.42f, 0.48f, 0.52f, 1f),
                    new Vector2(4f, 4f),
                    BudgetMaterialProfile.Dungeon);
                ApplySurfaceMaterial(
                    FindSceneTransform(scene, $"Blackwind_B{floor}_Connector"),
                    BudgetArtCatalog.BlackwindSurface,
                    new Color(0.34f, 0.4f, 0.44f, 1f),
                    new Vector2(1.4f, 1.8f),
                    BudgetMaterialProfile.Dungeon);
            }
        }

        private static void ApplySurfaceMaterial(
            Transform target,
            string texturePath,
            Color tint,
            Vector2 tiling,
            BudgetMaterialProfile profile)
        {
            Renderer renderer = target != null
                ? target.GetComponent<Renderer>()
                : null;
            if (renderer == null)
            {
                return;
            }

            renderer.sharedMaterial = BudgetVisualFactory.GetTexturedMaterial(
                texturePath,
                tint,
                tiling,
                profile);
            renderer.receiveShadows = true;
        }

        private static void CreateSurfacePatch(
            Transform root,
            string name,
            Vector3 position,
            Vector3 scale,
            float yaw,
            string texturePath,
            Color tint,
            Vector2 tiling)
        {
            GameObject patch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            patch.name = name;
            patch.transform.SetParent(root, false);
            patch.transform.localPosition = position;
            patch.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            patch.transform.localScale = scale;
            Collider collider = patch.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            Renderer renderer = patch.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = BudgetVisualFactory.GetTexturedMaterial(
                    texturePath,
                    tint,
                    tiling,
                    BudgetMaterialProfile.Environment);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
            }
        }

        private static Transform FindSceneTransform(Scene scene, string objectName)
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

        private static Transform FindTransformRecursive(
            Transform current,
            string objectName)
        {
            if (current.name == objectName)
            {
                return current;
            }

            for (int index = 0; index < current.childCount; index++)
            {
                Transform result = FindTransformRecursive(
                    current.GetChild(index),
                    objectName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static void UpgradeExistingInteractables(Scene scene)
        {
            GatherableObject[] gatherables =
                UnityEngine.Object.FindObjectsByType<GatherableObject>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            foreach (GatherableObject gatherable in gatherables)
            {
                if (gatherable == null || gatherable.gameObject.scene != scene)
                {
                    continue;
                }

                bool mineral = gatherable.ItemId == InventoryContentIds.SpiritDust;
                BudgetVisualFactory.AttachResourceVisual(
                    gatherable.gameObject,
                    mineral
                        ? BudgetArtCatalog.Nature("rock_smallC")
                        : BudgetArtCatalog.Nature("plant_bushDetailed"),
                    mineral ? 0.62f : 0.72f,
                    false,
                    -1f,
                    new Vector3(0f, StableYaw(gatherable.NodeId), 0f),
                    mineral
                        ? new Color(0.58f, 0.8f, 0.92f, 1f)
                        : Color.white,
                    true);
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                UpgradeNamedObjects(root.transform);
            }
        }

        private static void UpgradeNamedObjects(Transform current)
        {
            string name = current.name ?? string.Empty;
            if (name.IndexOf("chest", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                CreateChestVisual(current);
            }
            else if (name.IndexOf("QuestBoard", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                BudgetVisualFactory.AttachResourceVisual(
                    current.gameObject,
                    BudgetArtCatalog.Nature("sign"),
                    2f,
                    false,
                    -0.5f,
                    Vector3.zero,
                    Color.white,
                    true);
            }
            else if (name.IndexOf("AlchemyFurnace", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                CreateCauldronVisual(current);
            }
            else if (name.IndexOf("TeleportRing", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Renderer renderer = current.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = BudgetVisualFactory.GetMaterial(
                        new Color(0.055f, 0.17f, 0.15f, 1f));
                }
            }

            for (int index = 0; index < current.childCount; index++)
            {
                Transform child = current.GetChild(index);
                if (child.name != BudgetVisualFactory.VisualRootName)
                {
                    UpgradeNamedObjects(child);
                }
            }
        }

        private static void RepairBrokenMaterials(Scene scene)
        {
            int repaired = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null)
                    {
                        continue;
                    }

                    Material[] materials = renderer.sharedMaterials;
                    bool changed = false;
                    for (int index = 0; index < materials.Length; index++)
                    {
                        Material material = materials[index];
                        if (material != null
                            && material.shader != null
                            && material.shader.isSupported
                            && material.shader.name != "Hidden/InternalErrorShader")
                        {
                            continue;
                        }

                        materials[index] = BudgetVisualFactory.GetMaterial(
                            new Color(0.22f, 0.24f, 0.22f, 1f));
                        repaired++;
                        changed = true;
                    }

                    if (changed)
                    {
                        renderer.sharedMaterials = materials;
                    }
                }
            }

            if (repaired > 0)
            {
                Debug.Log($"G09-02 repaired {repaired} unsupported materials in {scene.name}.");
            }
        }

        private static void CreateChestVisual(Transform source)
        {
            if (source == null
                || source.Find(BudgetVisualFactory.VisualRootName) != null
                || source.GetComponent<Renderer>() == null)
            {
                return;
            }

            BudgetVisualFactory.HideRenderer(source.gameObject);
            var visualObject = new GameObject(BudgetVisualFactory.VisualRootName);
            visualObject.transform.SetParent(source, false);
            Transform visual = visualObject.transform;
            Vector3 inverse = InverseLossyScale(source);
            Color wood = new Color(0.38f, 0.21f, 0.08f, 1f);
            Color iron = new Color(0.16f, 0.15f, 0.14f, 1f);
            BudgetVisualFactory.CreatePart(
                visual, PrimitiveType.Cube, "ChestBase", Vector3.zero,
                Vector3.Scale(new Vector3(0.95f, 0.52f, 0.68f), inverse),
                Quaternion.identity, wood);
            BudgetVisualFactory.CreatePart(
                visual, PrimitiveType.Cube, "ChestLid",
                Vector3.Scale(new Vector3(0f, 0.36f, 0f), inverse),
                Vector3.Scale(new Vector3(1.02f, 0.24f, 0.75f), inverse),
                Quaternion.identity, wood);
            BudgetVisualFactory.CreatePart(
                visual, PrimitiveType.Cube, "ChestBand",
                Vector3.Scale(new Vector3(0f, 0.06f, -0.37f), inverse),
                Vector3.Scale(new Vector3(0.18f, 0.68f, 0.05f), inverse),
                Quaternion.identity, iron);
        }

        private static void CreateCauldronVisual(Transform source)
        {
            if (source == null || source.Find(BudgetVisualFactory.VisualRootName) != null)
            {
                return;
            }

            if (BudgetVisualFactory.AttachResourceVisual(
                source.gameObject,
                BudgetArtCatalog.Nature("pot_large"),
                1.2f,
                true,
                -1f,
                Vector3.zero,
                new Color(0.56f, 0.43f, 0.3f, 1f),
                true,
                BudgetMaterialProfile.Environment))
            {
                return;
            }

            BudgetVisualFactory.HideRenderer(source.gameObject);
            var visualObject = new GameObject(BudgetVisualFactory.VisualRootName);
            visualObject.transform.SetParent(source, false);
            Transform visual = visualObject.transform;
            Vector3 inverse = InverseLossyScale(source);
            Color bronze = new Color(0.25f, 0.15f, 0.08f, 1f);
            BudgetVisualFactory.CreatePart(
                visual, PrimitiveType.Sphere, "Bowl", Vector3.zero,
                Vector3.Scale(new Vector3(1.05f, 0.72f, 1.05f), inverse),
                Quaternion.identity, bronze);
            for (int side = -1; side <= 1; side += 2)
            {
                BudgetVisualFactory.CreatePart(
                    visual, PrimitiveType.Cylinder, "Handle",
                    Vector3.Scale(new Vector3(side * 0.72f, 0.35f, 0f), inverse),
                    Vector3.Scale(new Vector3(0.11f, 0.32f, 0.11f), inverse),
                    Quaternion.Euler(0f, 0f, 90f), bronze);
            }
        }

        private static void CreateHouse(
            Transform parent,
            string name,
            Vector3 position,
            float yaw,
            Color wallColor)
        {
            var house = new GameObject(name);
            house.transform.SetParent(parent, false);
            house.transform.localPosition = position;
            house.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Transform root = house.transform;
            Color timber = new Color(0.18f, 0.09f, 0.045f, 1f);
            Color roof = new Color(0.075f, 0.12f, 0.11f, 1f);
            Color stone = new Color(0.24f, 0.25f, 0.22f, 1f);
            BudgetVisualFactory.CreatePart(root, PrimitiveType.Cube, "StonePlinth",
                new Vector3(0f, 0.16f, 0f), new Vector3(4.45f, 0.32f, 3.55f),
                Quaternion.identity, stone);
            BudgetVisualFactory.CreatePart(root, PrimitiveType.Cube, "Walls",
                new Vector3(0f, 1.15f, 0f), new Vector3(4.2f, 2.3f, 3.3f),
                Quaternion.identity, wallColor);
            BudgetVisualFactory.CreatePart(root, PrimitiveType.Cube, "Door",
                new Vector3(0f, 0.9f, -1.67f), new Vector3(0.85f, 1.65f, 0.12f),
                Quaternion.identity, timber);
            BudgetVisualFactory.CreatePart(root, PrimitiveType.Cube, "Eaves",
                new Vector3(0f, 2.38f, 0f), new Vector3(5.1f, 0.2f, 4.15f),
                Quaternion.identity, roof);
            CreateRoof(root, new Vector3(0f, 2.45f, 0f), new Vector3(5f, 1.3f, 4f), roof);
            for (int side = -1; side <= 1; side += 2)
            {
                BudgetVisualFactory.CreatePart(root, PrimitiveType.Cylinder, "FrontColumn",
                    new Vector3(side * 1.65f, 1f, -1.9f), new Vector3(0.13f, 1f, 0.13f),
                    Quaternion.identity, timber);
                BudgetVisualFactory.CreatePart(root, PrimitiveType.Cube, "WindowFrame",
                    new Vector3(side * 1.05f, 1.25f, -1.675f),
                    new Vector3(0.62f, 0.72f, 0.08f),
                    Quaternion.identity, timber);
            }
        }

        private static void CreateVillageGate(
            Transform parent,
            string name,
            Vector3 position,
            float yaw,
            float scale = 1f)
        {
            var gate = new GameObject(name);
            gate.transform.SetParent(parent, false);
            gate.transform.localPosition = position;
            gate.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            gate.transform.localScale = Vector3.one * Mathf.Max(0.2f, scale);
            Color timber = new Color(0.19f, 0.075f, 0.035f, 1f);
            Color roof = new Color(0.07f, 0.12f, 0.115f, 1f);
            for (int side = -1; side <= 1; side += 2)
            {
                BudgetVisualFactory.CreatePart(gate.transform, PrimitiveType.Cylinder, "GatePost",
                    new Vector3(side * 2.1f, 1.8f, 0f), new Vector3(0.24f, 1.8f, 0.24f),
                    Quaternion.identity, timber);
            }
            BudgetVisualFactory.CreatePart(gate.transform, PrimitiveType.Cube, "GateBeam",
                new Vector3(0f, 3.25f, 0f), new Vector3(5.1f, 0.3f, 0.38f),
                Quaternion.identity, timber);
            CreateRoof(gate.transform, new Vector3(0f, 3.48f, 0f),
                new Vector3(5.8f, 0.9f, 1.8f), roof);
        }

        private static void CreateShrine(Transform parent, string name, Vector3 position)
        {
            var shrine = new GameObject(name);
            shrine.transform.SetParent(parent, false);
            shrine.transform.localPosition = position;
            Color stone = new Color(0.29f, 0.33f, 0.34f, 1f);
            Color roof = new Color(0.15f, 0.2f, 0.22f, 1f);
            BudgetVisualFactory.CreatePart(shrine.transform, PrimitiveType.Cylinder, "Platform",
                new Vector3(0f, 0.2f, 0f), new Vector3(3.4f, 0.2f, 3.4f),
                Quaternion.identity, stone);
            for (int side = -1; side <= 1; side += 2)
            {
                BudgetVisualFactory.CreatePart(shrine.transform, PrimitiveType.Cylinder, "Pillar",
                    new Vector3(side * 1.7f, 1.7f, 0f), new Vector3(0.22f, 1.5f, 0.22f),
                    Quaternion.identity, stone);
            }
            CreateRoof(shrine.transform, new Vector3(0f, 3.05f, 0f),
                new Vector3(4.8f, 1.1f, 2.4f), roof);
        }

        private static void CreateHerbGarden(Transform root)
        {
            var garden = new GameObject("HerbGarden_Qingshi");
            garden.transform.SetParent(root, false);
            garden.transform.localPosition = new Vector3(-7f, 0f, 6f);
            for (int index = 0; index < 6; index++)
            {
                string plant = index % 3 == 0
                    ? "flower_purpleA"
                    : index % 3 == 1
                        ? "flower_yellowB"
                        : "plant_bush";
                AddResource(garden.transform, $"GardenPlant_{index + 1:00}",
                    BudgetArtCatalog.Nature(plant),
                    new Vector3((index % 3) * 1.1f, 0f, (index / 3) * 1.1f),
                    0.65f, false, index * 52f);
            }
            for (int index = 0; index < 3; index++)
            {
                AddResource(garden.transform, $"GardenFence_{index + 1:00}",
                    BudgetArtCatalog.Nature(index == 1 ? "fence_gate" : "fence_simple"),
                    new Vector3(index * 1.8f - 0.2f, 0f, -0.8f),
                    1.6f, true, 0f);
            }
        }

        private static void CreateCreek(Transform root)
        {
            GameObject water = BudgetVisualFactory.CreatePart(
                root,
                PrimitiveType.Cube,
                "CreekWater_Qingshi",
                new Vector3(-4f, 0.03f, 8f),
                new Vector3(11f, 0.05f, 2.4f),
                Quaternion.Euler(0f, -8f, 0f),
                new Color(0.12f, 0.32f, 0.34f, 1f));
            Renderer renderer = water.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = GetWaterMaterial();
                renderer.receiveShadows = false;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static Material GetWaterMaterial()
        {
            if (_waterMaterial != null)
            {
                return _waterMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");
            if (shader == null)
            {
                return null;
            }

            Color color = new Color(0.12f, 0.31f, 0.34f, 1f);
            _waterMaterial = new Material(shader)
            {
                name = "BudgetWater_Qingshi",
                color = color,
                hideFlags = HideFlags.DontSave
            };
            if (_waterMaterial.HasProperty("_BaseColor"))
            {
                _waterMaterial.SetColor("_BaseColor", color);
            }
            if (_waterMaterial.HasProperty("_Metallic"))
            {
                _waterMaterial.SetFloat("_Metallic", 0.05f);
            }
            if (_waterMaterial.HasProperty("_Smoothness"))
            {
                _waterMaterial.SetFloat("_Smoothness", 0.78f);
            }
            return _waterMaterial;
        }

        private static void CreateBrazier(Transform parent, string name, Vector3 position)
        {
            var brazier = new GameObject(name);
            brazier.transform.SetParent(parent, false);
            brazier.transform.localPosition = position;
            Color metal = new Color(0.16f, 0.14f, 0.12f, 1f);
            Color ember = new Color(0.9f, 0.23f, 0.04f, 1f);
            BudgetVisualFactory.CreatePart(brazier.transform, PrimitiveType.Cylinder, "Bowl",
                new Vector3(0f, 0.85f, 0f), new Vector3(0.48f, 0.22f, 0.48f),
                Quaternion.identity, metal);
            BudgetVisualFactory.CreatePart(brazier.transform, PrimitiveType.Cylinder, "Stand",
                new Vector3(0f, 0.38f, 0f), new Vector3(0.12f, 0.38f, 0.12f),
                Quaternion.identity, metal);
            BudgetVisualFactory.CreatePart(brazier.transform, PrimitiveType.Sphere, "Ember",
                new Vector3(0f, 1.05f, 0f), new Vector3(0.28f, 0.2f, 0.28f),
                Quaternion.identity, ember);
            Light light = brazier.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 6f;
            light.intensity = 1.35f;
            light.color = new Color(1f, 0.35f, 0.12f, 1f);
            light.shadows = LightShadows.None;
        }

        private static void CreateRoof(
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Color color)
        {
            var roof = new GameObject("HipRoof");
            roof.transform.SetParent(parent, false);
            roof.transform.localPosition = localPosition;
            roof.transform.localScale = localScale;
            MeshFilter filter = roof.AddComponent<MeshFilter>();
            filter.sharedMesh = GetHipRoofMesh();
            MeshRenderer renderer = roof.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = BudgetVisualFactory.GetMaterial(color);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        private static Mesh GetHipRoofMesh()
        {
            if (_hipRoofMesh != null)
            {
                return _hipRoofMesh;
            }

            _hipRoofMesh = new Mesh { name = "BudgetArt_HipRoof" };
            _hipRoofMesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(-0.18f, 1f, -0.08f), new Vector3(0.18f, 1f, -0.08f),
                new Vector3(0.18f, 1f, 0.08f), new Vector3(-0.18f, 1f, 0.08f)
            };
            _hipRoofMesh.triangles = new[]
            {
                0, 1, 5, 0, 5, 4,
                1, 2, 6, 1, 6, 5,
                2, 3, 7, 2, 7, 6,
                3, 0, 4, 3, 4, 7,
                4, 5, 6, 4, 6, 7,
                0, 3, 2, 0, 2, 1
            };
            _hipRoofMesh.RecalculateNormals();
            _hipRoofMesh.RecalculateBounds();
            return _hipRoofMesh;
        }

        private static void AddResourceScatter(
            Transform parent,
            string prefix,
            IReadOnlyList<Vector3> positions,
            IReadOnlyList<string> resources,
            float baseHeight,
            float variation)
        {
            AddResourceScatter(
                parent,
                prefix,
                positions,
                resources,
                baseHeight,
                variation,
                Color.white);
        }

        private static void AddResourceScatter(
            Transform parent,
            string prefix,
            IReadOnlyList<Vector3> positions,
            IReadOnlyList<string> resources,
            float baseHeight,
            float variation,
            Color tint)
        {
            for (int index = 0; index < positions.Count; index++)
            {
                float height = baseHeight
                    + ((index * 37) % 100) / 100f * variation;
                AddResource(
                    parent,
                    $"{prefix}_{index + 1:00}",
                    resources[index % resources.Count],
                    positions[index],
                    height,
                    false,
                    (index * 67) % 360,
                    tint);
            }
        }

        private static GameObject AddResource(
            Transform parent,
            string name,
            string resource,
            Vector3 position,
            float targetMeasure,
            bool footprint,
            float yaw)
        {
            return AddResource(
                parent,
                name,
                resource,
                position,
                targetMeasure,
                footprint,
                yaw,
                Color.white);
        }

        private static GameObject AddResource(
            Transform parent,
            string name,
            string resource,
            Vector3 position,
            float targetMeasure,
            bool footprint,
            float yaw,
            Color tint)
        {
            bool stone = name.IndexOf("rock", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("stone", StringComparison.OrdinalIgnoreCase) >= 0
                || resource != null
                    && resource.IndexOf("cliff_", StringComparison.OrdinalIgnoreCase) >= 0;
            BudgetMaterialProfile profile = stone
                ? BudgetMaterialProfile.Stone
                : resource != null && resource.StartsWith(
                    BudgetArtCatalog.DungeonRoot,
                    StringComparison.Ordinal)
                ? BudgetMaterialProfile.Dungeon
                : BudgetMaterialProfile.Environment;
            return BudgetVisualFactory.CreateResourceProp(
                parent,
                resource,
                name,
                position,
                targetMeasure,
                footprint,
                yaw,
                tint,
                profile);
        }

        private static int StableYaw(string value)
        {
            unchecked
            {
                int hash = 17;
                string text = value ?? string.Empty;
                for (int index = 0; index < text.Length; index++)
                {
                    hash = hash * 31 + text[index];
                }
                return Mathf.Abs(hash % 360);
            }
        }

        private static Vector3 InverseLossyScale(Transform transform)
        {
            Vector3 scale = transform.lossyScale;
            return new Vector3(
                Mathf.Abs(scale.x) > 0.0001f ? 1f / scale.x : 1f,
                Mathf.Abs(scale.y) > 0.0001f ? 1f / scale.y : 1f,
                Mathf.Abs(scale.z) > 0.0001f ? 1f / scale.z : 1f);
        }

        private static GameObject FindSceneRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }
            return null;
        }

        private static int CountDescendants(Transform root)
        {
            int count = 1;
            for (int index = 0; index < root.childCount; index++)
            {
                count += CountDescendants(root.GetChild(index));
            }
            return count;
        }
    }
}
