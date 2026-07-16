using System;
using UnityEngine;

namespace Wendao.Entities.Visuals
{
    /// <summary>
    /// Runtime finish for the Blender-built cultivator. The FBX owns all
    /// visible geometry; this component only applies the project palette and
    /// exposes the authored weapon bone to gameplay.
    /// </summary>
    public sealed class CultivatorPlayerStyle : MonoBehaviour
    {
        private Transform _weaponBone;
        private bool _configured;

        public Transform WeaponBone => _weaponBone;
        public bool IsConfigured => _configured;

        public void Configure(GameObject actorRoot)
        {
            if (_configured || actorRoot == null)
            {
                return;
            }

            _weaponBone = FindTransform("Weapon.R");
            ModularCharacterMaterialUtility.Apply(
                gameObject,
                ModularCharacterMaterialUtility.Player);
            _configured = true;
        }

        private Transform FindTransform(string transformName)
        {
            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            foreach (Transform candidate in transforms)
            {
                if (candidate != null
                    && string.Equals(
                        candidate.name,
                        transformName,
                        StringComparison.Ordinal))
                {
                    return candidate;
                }
            }
            return null;
        }
    }
}
