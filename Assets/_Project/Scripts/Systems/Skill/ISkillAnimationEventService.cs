using UnityEngine;

namespace Wendao.Systems.Skill
{
    public interface ISkillAnimationEventService
    {
        bool TryReleaseAtAnimationEvent(GameObject casterActor);
    }
}
