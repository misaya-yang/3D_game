using System;
using UnityEngine;

namespace Wendao.Entities.Visuals
{
    /// <summary>
    /// Applies the shared G09-07 texture set and faction palette to modular
    /// NPC and human-enemy FBX assets. Geometry and animation remain authored
    /// in Blender; no runtime placeholder meshes are created.
    /// </summary>
    public sealed class ModularCharacterStyle : MonoBehaviour
    {
        private string _resourcePath = string.Empty;
        private bool _configured;

        public string ResourcePath => _resourcePath;
        public bool IsConfigured => _configured;

        public void Configure(string resourcePath)
        {
            if (_configured)
            {
                return;
            }

            if (!TryResolvePalette(
                    resourcePath,
                    out ModularCharacterPalette palette))
            {
                Debug.LogWarning(
                    $"No modular character palette registered for "
                    + $"{resourcePath}.");
                enabled = false;
                return;
            }

            _resourcePath = resourcePath;
            ModularCharacterMaterialUtility.Apply(gameObject, palette);
            _configured = true;
        }

        private static bool TryResolvePalette(
            string resourcePath,
            out ModularCharacterPalette palette)
        {
            if (string.Equals(
                    resourcePath,
                    BudgetArtCatalog.NpcGuard,
                    StringComparison.Ordinal))
            {
                palette = ModularCharacterMaterialUtility.Guard;
                return true;
            }

            if (string.Equals(
                    resourcePath,
                    BudgetArtCatalog.NpcHealer,
                    StringComparison.Ordinal))
            {
                palette = ModularCharacterMaterialUtility.Healer;
                return true;
            }

            if (string.Equals(
                    resourcePath,
                    BudgetArtCatalog.NpcHermit,
                    StringComparison.Ordinal))
            {
                palette = ModularCharacterMaterialUtility.Hermit;
                return true;
            }

            if (string.Equals(
                    resourcePath,
                    BudgetArtCatalog.HumanEnemy,
                    StringComparison.Ordinal))
            {
                palette = ModularCharacterMaterialUtility.Bandit;
                return true;
            }

            palette = default;
            return false;
        }
    }
}
