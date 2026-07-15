using System.Collections.Generic;
using UnityEngine;
using Wendao.Data;

namespace Wendao.Systems.Skill
{
    public interface ISkillService
    {
        IReadOnlyList<SkillRuntime> Learned { get; }
        string[] EquippedIds { get; }
        bool IsCasting { get; }

        bool Learn(string skillId);
        bool Equip(string skillId, int barIndex);
        bool Unequip(int barIndex);
        bool CanCast(int barIndex);
        bool TryCast(int barIndex, Vector3 targetPoint, GameObject targetActor);
        void TickCooldowns(float deltaTime);
        bool TryUpgrade(string skillId);
        float GetCooldownRemaining(int barIndex);
    }
}
