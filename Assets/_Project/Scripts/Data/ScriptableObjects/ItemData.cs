using System;
using UnityEngine;

namespace Wendao.Data
{
    [CreateAssetMenu(fileName = "Item_New", menuName = "问道/物品/ItemData")]
    public class ItemData : ScriptableObject
    {
        public string Id;
        public string DisplayNameKey;
        public string DisplayName;
        public string DescriptionKey;
        [TextArea] public string Description;
        public ItemType Type;
        public ItemRarity Rarity;
        public Sprite Icon;
        public int MaxStack = 99;
        public bool IsBound;
        public int BuyPrice;
        public int SellPrice;
        public int RequiredRealm;
        public UseEffect[] UseEffects = Array.Empty<UseEffect>();
        public string EquipmentDataId;
        public string[] AcquisitionHintKeys = Array.Empty<string>();
    }

    [Serializable]
    public class UseEffect
    {
        public UseEffectType EffectType;
        public float Value;
        public float Duration;
        public string StatusEffectId;
    }
}
