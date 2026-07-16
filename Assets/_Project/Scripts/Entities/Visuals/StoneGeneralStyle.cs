using System;
using UnityEngine;

namespace Wendao.Entities.Visuals
{
    /// <summary>
    /// Restores the boss-specific stone hierarchy and jade emissive accents
    /// after the generic URP material upgrade.
    /// </summary>
    public sealed class StoneGeneralStyle : MonoBehaviour
    {
        private static readonly Color Stone =
            new Color(0.16f, 0.2f, 0.21f, 1f);
        private static readonly Color DarkStone =
            new Color(0.055f, 0.075f, 0.08f, 1f);
        private static readonly Color Jade =
            new Color(0.09f, 0.62f, 0.42f, 1f);
        private static readonly Color JadeEmission =
            new Color(0.04f, 0.85f, 0.52f, 1f);

        private bool _configured;

        public bool IsConfigured => _configured;

        public void Configure()
        {
            if (_configured)
            {
                return;
            }

            foreach (Renderer renderer in
                GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                {
                    continue;
                }

                string objectName = renderer.gameObject.name;
                bool jade = objectName.IndexOf(
                        "Core",
                        StringComparison.Ordinal) >= 0
                    || objectName.IndexOf(
                        "Eye",
                        StringComparison.Ordinal) >= 0;
                bool dark = objectName.IndexOf(
                        "Maul",
                        StringComparison.Ordinal) >= 0
                    || objectName.IndexOf(
                        "Crown",
                        StringComparison.Ordinal) >= 0
                    || objectName.IndexOf(
                        "Shoulder",
                        StringComparison.Ordinal) >= 0
                    || objectName.IndexOf(
                        "ChestPlate",
                        StringComparison.Ordinal) >= 0;

                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null)
                    {
                        continue;
                    }

                    Color color = jade
                        ? Jade
                        : dark
                            ? DarkStone
                            : Stone;
                    SetColor(material, color);
                    if (jade)
                    {
                        SetEmission(material);
                    }
                }
            }

            _configured = true;
        }

        private static void SetColor(
            Material material,
            Color color)
        {
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

        private static void SetEmission(Material material)
        {
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor(
                    "_EmissionColor",
                    JadeEmission * 2.1f);
            }
        }
    }
}
