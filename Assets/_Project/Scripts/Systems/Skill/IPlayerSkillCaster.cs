using UnityEngine;

namespace Wendao.Systems.Skill
{
    public interface IPlayerSkillCaster
    {
        GameObject Actor { get; }
        Vector3 CastOrigin { get; }
        Vector3 Forward { get; }
        bool IsDead { get; }
        bool IsSilenced { get; }
        bool CanBeginSkillCast { get; }

        bool BeginSkillCast();
        void EndSkillCast();
    }
}
