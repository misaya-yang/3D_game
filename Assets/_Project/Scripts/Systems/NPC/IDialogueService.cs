using System.Collections.Generic;
using Wendao.Data;

namespace Wendao.Systems.NPC
{
    public interface IDialogueService
    {
        bool IsOpen { get; }
        string CurrentDialogueId { get; }
        string CurrentNpcId { get; }
        DialogueNode CurrentNode { get; }
        IReadOnlyList<DialogueChoice> VisibleChoices { get; }

        void StartDialogue(string dialogueId, string npcId);
        bool TryStartDialogue(string dialogueId, string npcId);
        void Advance();
        void Choose(int choiceIndex);
        void EndDialogue(bool cancelled);
    }
}
