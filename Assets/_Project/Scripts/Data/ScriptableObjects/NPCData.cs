using System;
using UnityEngine;

namespace Wendao.Data
{
    [CreateAssetMenu(fileName = "NPC_New", menuName = "问道/NPC/NPCData")]
    public class NPCData : ScriptableObject
    {
        public string Id;
        public string DisplayNameKey;
        public string DisplayName;
        public string DefaultDialogueId;
        public string FactionId;
        public bool IsVendor;
        public string[] VendorItemIds = Array.Empty<string>();
        public AffectionMilestone[] AffectionMilestones = Array.Empty<AffectionMilestone>();
        public GameObject Prefab;
    }

    [Serializable]
    public class AffectionMilestone
    {
        public int RequiredAffection;
        public string MilestoneId;
        public string UnlockDialogueId;
        public string UnlockQuestId;
    }
}
