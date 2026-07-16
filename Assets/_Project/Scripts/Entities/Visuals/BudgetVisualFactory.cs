using System.Collections.Generic;
using UnityEngine;
using Wendao.Data;
using Wendao.Systems.Enemy;

namespace Wendao.Entities.Visuals
{
    public enum BudgetMaterialProfile
    {
        Character,
        Creature,
        Environment,
        Stone,
        Dungeon,
        Accent
    }

    /// <summary>
    /// Adds a replaceable visual child while preserving gameplay roots,
    /// colliders, component ownership and content ids.
    /// </summary>
    public static class BudgetVisualFactory
    {
        public const string VisualRootName = "BudgetVisual";

        private const string UrpLitShader = "Universal Render Pipeline/Lit";
        private static readonly Dictionary<int, Material> MaterialCache =
            new Dictionary<int, Material>();
        private static readonly Dictionary<string, GameObject> ResourceCache =
            new Dictionary<string, GameObject>();
        private static readonly Dictionary<string, Material> TexturedMaterialCache =
            new Dictionary<string, Material>();

        public static bool AttachPlayer(GameObject root)
        {
            return AttachResourceVisual(
                root,
                BudgetArtCatalog.Player,
                1.82f,
                false,
                0f,
                Vector3.zero,
                new Color(0.88f, 0.97f, 0.94f, 1f),
                true,
                BudgetMaterialProfile.Character,
                CharacterVisualRole.Player);
        }

        public static bool AttachNpc(GameObject root, string objectName)
        {
            string name = objectName ?? string.Empty;
            Color tint;
            if (name.IndexOf(
                    "YaoLao",
                    System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf(
                    "Danding",
                    System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                tint = new Color(0.78f, 0.9f, 0.77f, 1f);
            }
            else if (name.IndexOf(
                         "Hermit",
                         System.StringComparison.OrdinalIgnoreCase) >= 0
                     || name.IndexOf(
                         "Echo",
                         System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                tint = new Color(0.62f, 0.82f, 0.94f, 1f);
            }
            else
            {
                tint = new Color(0.7f, 0.8f, 0.68f, 1f);
            }
            return AttachResourceVisual(
                root,
                BudgetArtCatalog.GetNpcResource(objectName),
                1.72f,
                false,
                -1f,
                Vector3.zero,
                tint,
                true,
                BudgetMaterialProfile.Character,
                CharacterVisualRole.Npc);
        }

        public static bool AttachEnemy(GameObject root, EnemyData data)
        {
            if (root == null || data == null || HasVisual(root))
            {
                return root != null && HasVisual(root);
            }

            if (data.Id == EnemyContentIds.GreyWolf
                || data.Id == EnemyContentIds.EliteWolf)
            {
                bool elite = data.Id == EnemyContentIds.EliteWolf;
                bool attached = AttachResourceVisual(
                    root,
                    BudgetArtCatalog.Wolf,
                    elite ? 2.05f : 1.65f,
                    true,
                    0f,
                    Vector3.zero,
                    elite
                        ? new Color(0.68f, 0.36f, 0.28f, 1f)
                        : new Color(0.72f, 0.78f, 0.8f, 1f),
                    true,
                    BudgetMaterialProfile.Creature,
                    CharacterVisualRole.Creature);
                if (attached)
                {
                    return true;
                }

                HideExistingRenderers(root);
                CreateWolf(root.transform, elite);
                return true;
            }

            if (data.Id == EnemyContentIds.StoneGeneral)
            {
                bool attached = AttachResourceVisual(
                    root,
                    BudgetArtCatalog.StoneGeneral,
                    3.8f,
                    false,
                    0f,
                    Vector3.zero,
                    new Color(0.58f, 0.64f, 0.65f, 1f),
                    true,
                    BudgetMaterialProfile.Stone,
                    CharacterVisualRole.Boss);
                if (attached)
                {
                    return true;
                }

                HideExistingRenderers(root);
                CreateStoneGeneral(root.transform);
                return true;
            }

            string resource = BudgetArtCatalog.GetEnemyResource(data.Id);
            if (!string.IsNullOrEmpty(resource))
            {
                return AttachResourceVisual(
                    root,
                    resource,
                    data.Id == EnemyContentIds.BlackwindSpawn ? 1.6f : 1.72f,
                    false,
                    0f,
                    Vector3.zero,
                    data.Id == EnemyContentIds.BlackwindSpawn
                        ? new Color(0.52f, 0.68f, 0.72f, 1f)
                        : new Color(0.76f, 0.68f, 0.62f, 1f),
                    true,
                    BudgetMaterialProfile.Creature,
                    CharacterVisualRole.HumanEnemy);
            }

            return false;
        }

        public static bool AttachTrainingDummy(GameObject root)
        {
            if (root == null || HasVisual(root))
            {
                return root != null && HasVisual(root);
            }

            HideExistingRenderers(root);
            Transform visual = CreateVisualRoot(root.transform);
            Vector3 rootScale = root.transform.lossyScale;
            visual.localScale = new Vector3(
                Mathf.Abs(rootScale.x) > 0.0001f ? 1f / rootScale.x : 1f,
                Mathf.Abs(rootScale.y) > 0.0001f ? 1f / rootScale.y : 1f,
                Mathf.Abs(rootScale.z) > 0.0001f ? 1f / rootScale.z : 1f);
            visual.localPosition = Vector3.up * root.transform.InverseTransformPoint(
                new Vector3(root.transform.position.x, 0f, root.transform.position.z)).y;
            Color timber = new Color(0.39f, 0.22f, 0.1f, 1f);
            Color rope = new Color(0.62f, 0.48f, 0.26f, 1f);
            CreatePart(visual, PrimitiveType.Cylinder, "Post",
                new Vector3(0f, 0.95f, 0f), new Vector3(0.18f, 0.95f, 0.18f),
                Quaternion.identity, timber);
            CreatePart(visual, PrimitiveType.Sphere, "Head",
                new Vector3(0f, 1.82f, 0f), new Vector3(0.32f, 0.32f, 0.32f),
                Quaternion.identity, timber);
            CreatePart(visual, PrimitiveType.Cube, "Crossbar",
                new Vector3(0f, 1.25f, 0f), new Vector3(1.35f, 0.14f, 0.14f),
                Quaternion.identity, timber);
            CreatePart(visual, PrimitiveType.Cylinder, "Rope",
                new Vector3(0f, 1.25f, 0f), new Vector3(0.23f, 0.2f, 0.23f),
                Quaternion.identity, rope);
            CreatePart(visual, PrimitiveType.Cylinder, "Base",
                new Vector3(0f, 0.12f, 0f), new Vector3(0.62f, 0.12f, 0.62f),
                Quaternion.identity, new Color(0.2f, 0.16f, 0.12f, 1f));
            return true;
        }

        public static bool AttachResourceVisual(
            GameObject root,
            string resourcePath,
            float targetMeasure,
            bool measureFootprint,
            float groundOffset,
            Vector3 localEuler,
            Color tint,
            bool hideExisting,
            BudgetMaterialProfile materialProfile =
                BudgetMaterialProfile.Environment,
            CharacterVisualRole? characterRole = null)
        {
            if (root == null || string.IsNullOrEmpty(resourcePath))
            {
                return false;
            }

            if (HasVisual(root))
            {
                return true;
            }

            GameObject prefab = LoadResource(resourcePath);
            if (prefab == null)
            {
                Debug.LogWarning(
                    $"Budget art resource missing, keeping fallback: {resourcePath}");
                return false;
            }

            Transform visual = CreateVisualRoot(root.transform);
            GameObject instance = Object.Instantiate(prefab, visual, false);
            instance.name = "Model_" + prefab.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(localEuler);
            instance.transform.localScale = Vector3.one;
            DisableAuxiliaryMeshes(instance, resourcePath);
            RemoveImportedColliders(instance);
            UpgradeMaterials(
                instance,
                tint,
                LoadTextureOverride(resourcePath),
                materialProfile);

            if (!TryGetRendererBounds(instance, out Bounds bounds))
            {
                DestroyObject(visual.gameObject);
                return false;
            }

            float currentMeasure = measureFootprint
                ? Mathf.Max(bounds.size.x, bounds.size.z)
                : bounds.size.y;
            if (currentMeasure <= 0.0001f)
            {
                DestroyObject(visual.gameObject);
                return false;
            }

            float scale = Mathf.Max(0.0001f, targetMeasure) / currentMeasure;
            instance.transform.localScale = Vector3.one * scale;
            if (TryGetRendererBounds(instance, out bounds))
            {
                float localBottom = root.transform.InverseTransformPoint(bounds.min).y;
                instance.transform.localPosition +=
                    Vector3.up * (groundOffset - localBottom);
            }

            if (hideExisting)
            {
                HideExistingRenderers(root, visual);
            }

            if (characterRole.HasValue)
            {
                ConfigureCharacterRuntime(
                    root,
                    instance,
                    resourcePath,
                    characterRole.Value);
            }

            return true;
        }

        public static GameObject CreateResourceProp(
            Transform parent,
            string resourcePath,
            string objectName,
            Vector3 localPosition,
            float targetMeasure,
            bool measureFootprint,
            float yaw,
            Color tint,
            BudgetMaterialProfile materialProfile =
                BudgetMaterialProfile.Environment)
        {
            var root = new GameObject(objectName);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;
            bool attached = AttachResourceVisual(
                root,
                resourcePath,
                targetMeasure,
                measureFootprint,
                0f,
                new Vector3(0f, yaw, 0f),
                tint,
                false,
                materialProfile);
            if (!attached)
            {
                DestroyObject(root);
                return null;
            }

            return root;
        }

        public static GameObject CreatePart(
            Transform parent,
            PrimitiveType primitive,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation,
            Color color)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = objectName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation;
            part.transform.localScale = localScale;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = GetMaterial(color);
            }

            return part;
        }

        public static Material GetMaterial(Color color)
        {
            int key = Color32Key(color);
            if (MaterialCache.TryGetValue(key, out Material material)
                && material != null)
            {
                return material;
            }

            Shader shader = Shader.Find(UrpLitShader) ?? Shader.Find("Standard");
            if (shader == null)
            {
                return null;
            }

            material = new Material(shader)
            {
                name = $"BudgetArt_{key:X8}",
                color = color,
                hideFlags = HideFlags.DontSave
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            ConfigureSurface(material, BudgetMaterialProfile.Environment);

            MaterialCache[key] = material;
            return material;
        }

        public static Material GetTexturedMaterial(
            string textureResourcePath,
            Color tint,
            Vector2 tiling,
            BudgetMaterialProfile profile)
        {
            string key = string.Concat(
                textureResourcePath,
                "|",
                Color32Key(tint),
                "|",
                tiling.x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                "|",
                tiling.y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                "|",
                (int)profile);
            if (TexturedMaterialCache.TryGetValue(key, out Material cached)
                && cached != null)
            {
                return cached;
            }

            Texture2D texture = Resources.Load<Texture2D>(textureResourcePath);
            Shader shader = Shader.Find(UrpLitShader) ?? Shader.Find("Standard");
            if (texture == null || shader == null)
            {
                return GetMaterial(tint);
            }

            var material = new Material(shader)
            {
                name = $"BudgetSurface_{texture.name}_{Color32Key(tint):X8}",
                color = tint,
                hideFlags = HideFlags.DontSave,
                enableInstancing = true
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", tint);
            }
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", tiling);
            }
            material.mainTexture = texture;
            material.mainTextureScale = tiling;
            ConfigureSurface(material, profile);
            TexturedMaterialCache[key] = material;
            return material;
        }

        public static void HideRenderer(GameObject gameObject)
        {
            Renderer renderer = gameObject != null
                ? gameObject.GetComponent<Renderer>()
                : null;
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }

        private static GameObject LoadResource(string path)
        {
            if (ResourceCache.TryGetValue(path, out GameObject cached))
            {
                return cached;
            }

            GameObject loaded = Resources.Load<GameObject>(path);
            ResourceCache[path] = loaded;
            return loaded;
        }

        private static bool HasVisual(GameObject root)
        {
            return root != null && root.transform.Find(VisualRootName) != null;
        }

        private static Transform CreateVisualRoot(Transform parent)
        {
            var visual = new GameObject(VisualRootName);
            visual.transform.SetParent(parent, false);
            return visual.transform;
        }

        private static void HideExistingRenderers(
            GameObject root,
            Transform except = null)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null
                    && (except == null || !renderer.transform.IsChildOf(except)))
                {
                    renderer.enabled = false;
                }
            }
        }

        private static void RemoveImportedColliders(GameObject instance)
        {
            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in colliders)
            {
                DestroyObject(collider);
            }
        }

        private static void ConfigureCharacterRuntime(
            GameObject actorRoot,
            GameObject modelInstance,
            string resourcePath,
            CharacterVisualRole role)
        {
            RuntimeCharacterAnimator runtimeAnimator =
                modelInstance.GetComponent<RuntimeCharacterAnimator>();
            if (runtimeAnimator == null)
            {
                runtimeAnimator =
                    modelInstance.AddComponent<RuntimeCharacterAnimator>();
            }
            runtimeAnimator.Configure(actorRoot, resourcePath, role);

            if (role == CharacterVisualRole.Player
                && string.Equals(
                    resourcePath,
                    BudgetArtCatalog.Player,
                    System.StringComparison.Ordinal))
            {
                CultivatorPlayerStyle style =
                    modelInstance.GetComponent<CultivatorPlayerStyle>();
                if (style == null)
                {
                    style =
                        modelInstance.AddComponent<CultivatorPlayerStyle>();
                }
                style.Configure(actorRoot);
            }

            if (BudgetArtCatalog.IsModularCharacter(resourcePath))
            {
                ModularCharacterStyle style =
                    modelInstance.GetComponent<ModularCharacterStyle>();
                if (style == null)
                {
                    style =
                        modelInstance.AddComponent<ModularCharacterStyle>();
                }
                style.Configure(resourcePath);
            }

            if (string.Equals(
                    resourcePath,
                    BudgetArtCatalog.StoneGeneral,
                    System.StringComparison.Ordinal))
            {
                StoneGeneralStyle style =
                    modelInstance.GetComponent<StoneGeneralStyle>();
                if (style == null)
                {
                    style =
                        modelInstance.AddComponent<StoneGeneralStyle>();
                }
                style.Configure();
            }
        }

        private static void DisableAuxiliaryMeshes(
            GameObject instance,
            string resourcePath)
        {
            if (instance == null
                || !string.Equals(
                    resourcePath,
                    BudgetArtCatalog.Player,
                    System.StringComparison.Ordinal))
            {
                return;
            }

            // The curated Warrior FBX contains the artist's default Blender
            // cube in addition to the actual character meshes. It is not part
            // of the character and otherwise fills the camera at close range.
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null
                    && string.Equals(
                        renderer.gameObject.name,
                        "Cube",
                        System.StringComparison.Ordinal))
                {
                    renderer.enabled = false;
                }
            }
        }

        private static Texture LoadTextureOverride(string resourcePath)
        {
            if (string.Equals(
                    resourcePath,
                    BudgetArtCatalog.Player,
                    System.StringComparison.Ordinal))
            {
                // Cultivator embeds distinct cloth, sword and plain hair/eye
                // materials. The same-path PNG exists only for Resources and
                // provenance compatibility and must not replace every slot.
                return null;
            }
            if (string.Equals(
                    resourcePath,
                    BudgetArtCatalog.StoneGeneral,
                    System.StringComparison.Ordinal))
            {
                return null;
            }

            Texture texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null
                && resourcePath.StartsWith(
                    BudgetArtCatalog.DungeonRoot,
                    System.StringComparison.Ordinal))
            {
                texture = Resources.Load<Texture2D>(
                    BudgetArtCatalog.DungeonRoot + "/colormap");
            }
            return texture;
        }

        private static void UpgradeMaterials(
            GameObject instance,
            Color tint,
            Texture textureOverride,
            BudgetMaterialProfile profile)
        {
            Shader shader = Shader.Find(UrpLitShader);
            if (shader == null)
            {
                return;
            }

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                Material[] source = renderer.sharedMaterials;
                var upgraded = new Material[source.Length];
                for (int index = 0; index < source.Length; index++)
                {
                    Material original = source[index];
                    Color originalColor = ReadColor(original);
                    Color finalColor = GradeColor(originalColor, tint, profile);
                    var material = new Material(shader)
                    {
                        name = $"{instance.name}_{index}_BudgetURP",
                        hideFlags = HideFlags.DontSave
                    };
                    material.SetColor("_BaseColor", finalColor);
                    material.color = finalColor;
                    Texture texture = textureOverride ?? ReadTexture(original);
                    if (texture != null)
                    {
                        material.SetTexture("_BaseMap", texture);
                    }
                    ConfigureSurface(material, profile);

                    upgraded[index] = material;
                }

                renderer.sharedMaterials = upgraded;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        private static Color GradeColor(
            Color original,
            Color tint,
            BudgetMaterialProfile profile)
        {
            float tintStrength;
            float saturationScale;
            float brightnessScale;
            float maximumValue;
            switch (profile)
            {
                case BudgetMaterialProfile.Character:
                    tintStrength = 0.24f;
                    saturationScale = 0.88f;
                    brightnessScale = 0.96f;
                    maximumValue = 0.93f;
                    break;
                case BudgetMaterialProfile.Creature:
                    tintStrength = 0.42f;
                    saturationScale = 0.78f;
                    brightnessScale = 0.9f;
                    maximumValue = 0.82f;
                    break;
                case BudgetMaterialProfile.Stone:
                    tintStrength = 0.72f;
                    saturationScale = 0.42f;
                    brightnessScale = 0.84f;
                    maximumValue = 0.7f;
                    break;
                case BudgetMaterialProfile.Dungeon:
                    tintStrength = 0.62f;
                    saturationScale = 0.5f;
                    brightnessScale = 0.88f;
                    maximumValue = 0.7f;
                    break;
                case BudgetMaterialProfile.Accent:
                    tintStrength = 0.4f;
                    saturationScale = 0.82f;
                    brightnessScale = 0.94f;
                    maximumValue = 0.88f;
                    break;
                default:
                    tintStrength = 0.36f;
                    saturationScale = 0.66f;
                    brightnessScale = 0.88f;
                    maximumValue = 0.76f;
                    break;
            }

            Color multiplied = new Color(
                original.r * tint.r,
                original.g * tint.g,
                original.b * tint.b,
                original.a * tint.a);
            Color graded = Color.Lerp(original, multiplied, tintStrength);
            Color.RGBToHSV(graded, out float hue, out float saturation, out float value);
            saturation = Mathf.Clamp01(saturation * saturationScale);
            value = Mathf.Min(maximumValue, Mathf.Clamp01(value * brightnessScale));
            Color result = Color.HSVToRGB(hue, saturation, value);
            result.a = graded.a;
            return result;
        }

        private static void ConfigureSurface(
            Material material,
            BudgetMaterialProfile profile)
        {
            if (material == null)
            {
                return;
            }

            float smoothness;
            switch (profile)
            {
                case BudgetMaterialProfile.Character:
                    smoothness = 0.18f;
                    break;
                case BudgetMaterialProfile.Accent:
                    smoothness = 0.24f;
                    break;
                case BudgetMaterialProfile.Dungeon:
                    smoothness = 0.08f;
                    break;
                default:
                    smoothness = 0.06f;
                    break;
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }
            if (material.HasProperty("_SpecularHighlights"))
            {
                material.SetFloat("_SpecularHighlights", 0f);
            }
            material.enableInstancing = true;
        }

        private static Color ReadColor(Material material)
        {
            if (material == null)
            {
                return Color.white;
            }

            if (material.HasProperty("_BaseColor"))
            {
                return material.GetColor("_BaseColor");
            }

            if (material.HasProperty("_Color"))
            {
                return material.GetColor("_Color");
            }

            return Color.white;
        }

        private static Texture ReadTexture(Material material)
        {
            if (material == null)
            {
                return null;
            }

            if (material.HasProperty("_BaseMap"))
            {
                return material.GetTexture("_BaseMap");
            }

            return material.HasProperty("_MainTex")
                ? material.GetTexture("_MainTex")
                : null;
        }

        private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bounds = default;
            bool found = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found;
        }

        private static void CreateWolf(Transform root, bool elite)
        {
            Transform visual = CreateVisualRoot(root);
            Color fur = elite
                ? new Color(0.36f, 0.12f, 0.08f, 1f)
                : new Color(0.29f, 0.32f, 0.34f, 1f);
            Color dark = elite
                ? new Color(0.12f, 0.04f, 0.03f, 1f)
                : new Color(0.12f, 0.14f, 0.15f, 1f);
            float size = elite ? 1.18f : 1f;
            visual.localScale = Vector3.one * size;

            CreatePart(visual, PrimitiveType.Sphere, "Body",
                new Vector3(0f, 0.62f, 0f), new Vector3(0.42f, 0.34f, 0.72f),
                Quaternion.identity, fur);
            CreatePart(visual, PrimitiveType.Sphere, "Chest",
                new Vector3(0f, 0.72f, 0.42f), new Vector3(0.38f, 0.44f, 0.38f),
                Quaternion.identity, fur);
            CreatePart(visual, PrimitiveType.Sphere, "Head",
                new Vector3(0f, 0.9f, 0.72f), new Vector3(0.32f, 0.3f, 0.34f),
                Quaternion.identity, fur);
            CreatePart(visual, PrimitiveType.Cube, "Muzzle",
                new Vector3(0f, 0.82f, 1.02f), new Vector3(0.2f, 0.14f, 0.3f),
                Quaternion.identity, dark);
            CreatePart(visual, PrimitiveType.Cube, "EarL",
                new Vector3(-0.18f, 1.18f, 0.72f), new Vector3(0.09f, 0.22f, 0.08f),
                Quaternion.Euler(0f, 0f, -18f), dark);
            CreatePart(visual, PrimitiveType.Cube, "EarR",
                new Vector3(0.18f, 1.18f, 0.72f), new Vector3(0.09f, 0.22f, 0.08f),
                Quaternion.Euler(0f, 0f, 18f), dark);

            float[] xs = { -0.24f, 0.24f };
            float[] zs = { -0.38f, 0.42f };
            foreach (float x in xs)
            {
                foreach (float z in zs)
                {
                    CreatePart(visual, PrimitiveType.Cylinder, "Leg",
                        new Vector3(x, 0.28f, z), new Vector3(0.09f, 0.3f, 0.09f),
                        Quaternion.identity, dark);
                }
            }

            CreatePart(visual, PrimitiveType.Cylinder, "Tail",
                new Vector3(0f, 0.78f, -0.72f), new Vector3(0.11f, 0.48f, 0.11f),
                Quaternion.Euler(62f, 0f, 0f), fur);
        }

        private static void CreateStoneGeneral(Transform root)
        {
            Transform visual = CreateVisualRoot(root);
            Color stone = new Color(0.2f, 0.23f, 0.24f, 1f);
            Color edge = new Color(0.12f, 0.14f, 0.15f, 1f);
            Color core = new Color(0.85f, 0.28f, 0.08f, 1f);
            CreatePart(visual, PrimitiveType.Cube, "Torso",
                new Vector3(0f, 2f, 0f), new Vector3(1.4f, 1.25f, 0.8f),
                Quaternion.Euler(0f, 4f, 0f), stone);
            CreatePart(visual, PrimitiveType.Cube, "Head",
                new Vector3(0f, 3.35f, 0.04f), new Vector3(0.72f, 0.55f, 0.62f),
                Quaternion.Euler(0f, -6f, 0f), edge);
            CreatePart(visual, PrimitiveType.Sphere, "Core",
                new Vector3(0f, 2.1f, 0.84f), new Vector3(0.25f, 0.25f, 0.12f),
                Quaternion.identity, core);
            for (int side = -1; side <= 1; side += 2)
            {
                CreatePart(visual, PrimitiveType.Sphere, "Shoulder",
                    new Vector3(side * 1.55f, 2.65f, 0f),
                    new Vector3(0.65f, 0.65f, 0.65f), Quaternion.identity, edge);
                CreatePart(visual, PrimitiveType.Cube, "Arm",
                    new Vector3(side * 1.72f, 1.65f, 0f),
                    new Vector3(0.48f, 0.9f, 0.48f),
                    Quaternion.Euler(0f, 0f, side * -7f), stone);
                CreatePart(visual, PrimitiveType.Cube, "Leg",
                    new Vector3(side * 0.62f, 0.62f, 0f),
                    new Vector3(0.55f, 0.68f, 0.62f), Quaternion.identity, edge);
            }
        }

        private static void DestroyObject(Object value)
        {
            if (value == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(value);
            }
            else
            {
                Object.DestroyImmediate(value);
            }
        }

        private static int Color32Key(Color color)
        {
            Color32 value = color;
            return value.r | value.g << 8 | value.b << 16 | value.a << 24;
        }
    }
}
