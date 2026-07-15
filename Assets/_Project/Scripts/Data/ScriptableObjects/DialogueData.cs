using System;
using UnityEngine;

namespace Wendao.Data
{
    [CreateAssetMenu(fileName = "Dialogue_New", menuName = "问道/对话/DialogueData")]
    public class DialogueData : ScriptableObject
    {
        public string Id;
        public DialogueNode[] Nodes = Array.Empty<DialogueNode>();
    }

    [Serializable]
    public class DialogueNode
    {
        public string NodeId;
        public string SpeakerNameKey;
        public string SpeakerName;
        public string TextKey;
        [TextArea] public string Text;
        public DialogueChoice[] Choices = Array.Empty<DialogueChoice>();
        public string NextNodeId;
        public string QuestOfferId;
        public string QuestTurnInId;
        public bool End;
    }

    [Serializable]
    public class DialogueChoice
    {
        public string TextKey;
        public string Text;
        public string NextNodeId;
        public int RequiredAffection;
        public string RequiredQuestId;
        public string SetFlag;
    }
}
