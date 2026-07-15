using System;
using UnityEngine;

namespace Wendao.Data
{
    [CreateAssetMenu(fileName = "Quest_New", menuName = "问道/任务/QuestData")]
    public class QuestData : ScriptableObject
    {
        public string Id;
        public string DisplayNameKey;
        public string DisplayName;
        public string DescriptionKey;
        [TextArea] public string Description;
        public QuestType Type;
        public int RequiredRealm;
        public string[] PrerequisiteQuestIds = Array.Empty<string>();
        public QuestObjective[] Objectives = Array.Empty<QuestObjective>();
        public bool ObjectivesAreOrdered;
        public QuestReward AcceptRewards;
        public ItemStack[] TurnInCosts = Array.Empty<ItemStack>();
        public QuestReward Rewards = new QuestReward();
        public string StartDialogueId;
        public string CompleteDialogueId;
        public string StartNpcId;
        public string TurnInNpcId;
    }

    [Serializable]
    public class QuestObjective
    {
        public ObjectiveType Type;
        public string TargetId;
        public int RequiredCount;
        public string DescriptionKey;
        public string Description;
        public bool LatchOnFirstAcquire;
    }

    [Serializable]
    public class QuestReward
    {
        public float CultivationXp;
        public int SpiritStones;
        public ItemStack[] Items = Array.Empty<ItemStack>();
        public int FactionRep;
        public string FactionId;
        public string[] SkillIds = Array.Empty<string>();
    }

    [Serializable]
    public class ItemStack
    {
        public string ItemId;
        public int Count;
    }
}
