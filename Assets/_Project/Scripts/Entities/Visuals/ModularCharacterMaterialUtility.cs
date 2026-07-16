using System;
using UnityEngine;

namespace Wendao.Entities.Visuals
{
    internal readonly struct ModularCharacterPalette
    {
        public ModularCharacterPalette(
            Color robe,
            Color pants,
            Color rangerCloth,
            Color leather,
            Color skin,
            Color hair,
            Color eye,
            Color metal,
            bool bodyUsesRanger,
            bool legsUseRanger,
            bool feetUseRanger)
        {
            Robe = robe;
            Pants = pants;
            RangerCloth = rangerCloth;
            Leather = leather;
            Skin = skin;
            Hair = hair;
            Eye = eye;
            Metal = metal;
            BodyUsesRanger = bodyUsesRanger;
            LegsUseRanger = legsUseRanger;
            FeetUseRanger = feetUseRanger;
        }

        public Color Robe { get; }
        public Color Pants { get; }
        public Color RangerCloth { get; }
        public Color Leather { get; }
        public Color Skin { get; }
        public Color Hair { get; }
        public Color Eye { get; }
        public Color Metal { get; }
        public bool BodyUsesRanger { get; }
        public bool LegsUseRanger { get; }
        public bool FeetUseRanger { get; }
    }

    internal static class ModularCharacterMaterialUtility
    {
        private const string TextureRoot =
            "Art/Budget/Characters/CultivatorTextures/";

        public static readonly ModularCharacterPalette Player =
            new ModularCharacterPalette(
                new Color(0.34f, 0.45f, 0.48f, 1f),
                new Color(0.13f, 0.2f, 0.22f, 1f),
                new Color(0.26f, 0.35f, 0.37f, 1f),
                new Color(0.24f, 0.17f, 0.11f, 1f),
                new Color(0.78f, 0.62f, 0.5f, 1f),
                new Color(0.045f, 0.052f, 0.058f, 1f),
                new Color(0.78f, 0.73f, 0.64f, 1f),
                new Color(0.46f, 0.5f, 0.52f, 1f),
                false,
                false,
                true);

        public static readonly ModularCharacterPalette Guard =
            new ModularCharacterPalette(
                new Color(0.26f, 0.37f, 0.42f, 1f),
                new Color(0.1f, 0.16f, 0.19f, 1f),
                new Color(0.28f, 0.4f, 0.44f, 1f),
                new Color(0.19f, 0.14f, 0.09f, 1f),
                new Color(0.76f, 0.6f, 0.48f, 1f),
                new Color(0.04f, 0.047f, 0.052f, 1f),
                new Color(0.72f, 0.68f, 0.6f, 1f),
                new Color(0.4f, 0.44f, 0.46f, 1f),
                true,
                true,
                true);

        public static readonly ModularCharacterPalette Healer =
            new ModularCharacterPalette(
                new Color(0.42f, 0.54f, 0.46f, 1f),
                new Color(0.18f, 0.27f, 0.23f, 1f),
                new Color(0.36f, 0.48f, 0.41f, 1f),
                new Color(0.24f, 0.19f, 0.13f, 1f),
                new Color(0.8f, 0.65f, 0.52f, 1f),
                new Color(0.08f, 0.07f, 0.06f, 1f),
                new Color(0.72f, 0.68f, 0.6f, 1f),
                new Color(0.36f, 0.4f, 0.4f, 1f),
                false,
                false,
                false);

        public static readonly ModularCharacterPalette Hermit =
            new ModularCharacterPalette(
                new Color(0.38f, 0.42f, 0.48f, 1f),
                new Color(0.16f, 0.19f, 0.23f, 1f),
                new Color(0.31f, 0.36f, 0.42f, 1f),
                new Color(0.22f, 0.17f, 0.12f, 1f),
                new Color(0.74f, 0.58f, 0.47f, 1f),
                new Color(0.62f, 0.64f, 0.62f, 1f),
                new Color(0.7f, 0.67f, 0.6f, 1f),
                new Color(0.38f, 0.42f, 0.44f, 1f),
                false,
                false,
                false);

        public static readonly ModularCharacterPalette Bandit =
            new ModularCharacterPalette(
                new Color(0.29f, 0.17f, 0.15f, 1f),
                new Color(0.11f, 0.075f, 0.07f, 1f),
                new Color(0.31f, 0.18f, 0.16f, 1f),
                new Color(0.2f, 0.1f, 0.075f, 1f),
                new Color(0.68f, 0.5f, 0.4f, 1f),
                new Color(0.035f, 0.035f, 0.038f, 1f),
                new Color(0.68f, 0.61f, 0.52f, 1f),
                new Color(0.34f, 0.36f, 0.37f, 1f),
                true,
                true,
                true);

        public static void Apply(
            GameObject root,
            ModularCharacterPalette palette)
        {
            if (root == null)
            {
                return;
            }

            Texture2D skinTexture =
                Resources.Load<Texture2D>(TextureRoot + "Skin");
            Texture2D eyeTexture =
                Resources.Load<Texture2D>(TextureRoot + "Eyes");
            Texture2D hairTexture =
                Resources.Load<Texture2D>(TextureRoot + "Hair");
            Texture2D robeTexture =
                Resources.Load<Texture2D>(TextureRoot + "Robe");
            Texture2D rangerTexture =
                Resources.Load<Texture2D>(TextureRoot + "Ranger");

            foreach (Renderer renderer in
                root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                {
                    continue;
                }

                string objectName = renderer.gameObject.name;
                if (objectName.StartsWith(
                        "Cultivator_Hair",
                        StringComparison.Ordinal)
                    || string.Equals(
                        objectName,
                        "Cultivator_Eyebrows",
                        StringComparison.Ordinal))
                {
                    ApplyAll(
                        renderer,
                        palette.Hair,
                        hairTexture);
                }
                else if (string.Equals(
                             objectName,
                             "Cultivator_Body",
                             StringComparison.Ordinal))
                {
                    ApplyAll(
                        renderer,
                        palette.Robe,
                        palette.BodyUsesRanger
                            ? rangerTexture
                            : robeTexture);
                }
                else if (string.Equals(
                             objectName,
                             "Cultivator_Pants",
                             StringComparison.Ordinal))
                {
                    ApplyAll(
                        renderer,
                        palette.Pants,
                        palette.LegsUseRanger
                            ? rangerTexture
                            : robeTexture);
                }
                else if (string.Equals(
                             objectName,
                             "Cultivator_Arms",
                             StringComparison.Ordinal))
                {
                    ApplyPalette(
                        renderer,
                        new[] { palette.RangerCloth, palette.Skin },
                        new Texture[] { rangerTexture, skinTexture });
                }
                else if (string.Equals(
                             objectName,
                             "Cultivator_Head",
                             StringComparison.Ordinal))
                {
                    ApplyAll(renderer, palette.Skin, skinTexture);
                }
                else if (string.Equals(
                             objectName,
                             "Cultivator_Eyes",
                             StringComparison.Ordinal))
                {
                    ApplyPalette(
                        renderer,
                        new[] { palette.Eye, palette.Hair },
                        new Texture[] { eyeTexture, hairTexture });
                }
                else if (objectName.IndexOf(
                             "Boot",
                             StringComparison.Ordinal) >= 0)
                {
                    ApplyAll(
                        renderer,
                        palette.FeetUseRanger
                            ? palette.Leather
                            : palette.Pants,
                        palette.FeetUseRanger
                            ? rangerTexture
                            : robeTexture);
                }
                else if (objectName.IndexOf(
                             "Bracer",
                             StringComparison.Ordinal) >= 0
                         || objectName.IndexOf(
                             "Pauldron",
                             StringComparison.Ordinal) >= 0
                         || objectName.IndexOf(
                             "Belt",
                             StringComparison.Ordinal) >= 0
                         || objectName.IndexOf(
                             "Hood",
                             StringComparison.Ordinal) >= 0)
                {
                    ApplyAll(
                        renderer,
                        palette.Leather,
                        rangerTexture);
                }
                else if (objectName.IndexOf(
                             "Staff",
                             StringComparison.Ordinal) >= 0
                         || objectName.IndexOf(
                             "Bow",
                             StringComparison.Ordinal) >= 0)
                {
                    ApplyAll(renderer, palette.Leather, null);
                }
                else if (objectName.IndexOf(
                             "Jian",
                             StringComparison.Ordinal) >= 0
                         || objectName.IndexOf(
                             "Dagger",
                             StringComparison.Ordinal) >= 0)
                {
                    ApplyAll(renderer, palette.Metal, null);
                }
            }
        }

        private static void ApplyPalette(
            Renderer renderer,
            Color[] colors,
            Texture[] textures)
        {
            Material[] materials = renderer.sharedMaterials;
            for (int index = 0; index < materials.Length; index++)
            {
                Material material = materials[index];
                if (material == null)
                {
                    continue;
                }

                SetColor(
                    material,
                    colors[Mathf.Min(index, colors.Length - 1)]);
                SetTexture(
                    material,
                    textures[Mathf.Min(index, textures.Length - 1)]);
            }
        }

        private static void ApplyAll(
            Renderer renderer,
            Color color,
            Texture texture)
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                SetColor(material, color);
                SetTexture(material, texture);
            }
        }

        private static void SetColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
            material.color = color;
        }

        private static void SetTexture(
            Material material,
            Texture texture)
        {
            if (material == null || texture == null)
            {
                return;
            }
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }
            material.mainTexture = texture;
        }
    }
}
