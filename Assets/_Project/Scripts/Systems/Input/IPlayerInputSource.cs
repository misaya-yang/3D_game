using UnityEngine;

namespace Wendao.Systems.Input
{
    /// <summary>
    /// Device-agnostic gameplay input snapshot shared through ServiceLocator.
    /// </summary>
    public interface IPlayerInputSource
    {
        Vector2 Move { get; }
        Vector2 Look { get; }
        bool LookIsPointerDelta { get; }
        bool JumpPressedThisFrame { get; }
        bool JumpHeld { get; }
        bool SprintHeld { get; }
        bool LightAttackPressedThisFrame { get; }
        bool HeavyAttackPressedThisFrame { get; }
        bool DodgePressedThisFrame { get; }
        bool BlockHeld { get; }
        bool LockOnPressedThisFrame { get; }
        bool Skill1PressedThisFrame { get; }
        bool Skill2PressedThisFrame { get; }
        bool Skill3PressedThisFrame { get; }
        bool Skill4PressedThisFrame { get; }
        bool InteractPressedThisFrame { get; }
        bool OpenInventoryPressedThisFrame { get; }
        bool OpenCharacterPressedThisFrame { get; }
        bool OpenSkillPressedThisFrame { get; }
        bool OpenQuestPressedThisFrame { get; }
        bool OpenMapPressedThisFrame { get; }
        bool PausePressedThisFrame { get; }
        bool MountPressedThisFrame { get; }
        bool IsEnabled { get; }

        void SetEnabled(bool enabled);
    }
}
