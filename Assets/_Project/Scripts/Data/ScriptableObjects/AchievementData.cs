using System;
using UnityEngine;

namespace Wendao.Data
{
    [CreateAssetMenu(fileName = "Achievement_New", menuName = "问道/成就/AchievementData")]
    public class AchievementData : ScriptableObject
    {
        public string Id;
        public string DisplayNameKey;
        public string DisplayName;
        public string DescriptionKey;
        public string Description;
        public string TriggerType;
        public string TargetId;
        public float RequiredValue;
        public string RewardTitleId;
        public ItemStack[] RewardItems = Array.Empty<ItemStack>();
        public int RewardSpiritStones;
    }
}
