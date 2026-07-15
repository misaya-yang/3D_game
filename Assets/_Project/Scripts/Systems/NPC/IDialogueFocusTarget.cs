using UnityEngine;

namespace Wendao.Systems.NPC
{
    public interface IDialogueFocusTarget
    {
        string NpcId { get; }
        Transform DialogueFocusTransform { get; }
    }
}
