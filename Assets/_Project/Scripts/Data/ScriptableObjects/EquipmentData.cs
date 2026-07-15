using System;
using UnityEngine;

namespace Wendao.Data
{
    [CreateAssetMenu(fileName = "Equipment_New", menuName = "问道/物品/EquipmentData")]
    public class EquipmentData : ScriptableObject
    {
        public string Id;
        public string DisplayNameKey;
        public string DisplayName;
        public EquipmentSlot Slot;
        public ItemRarity Rarity;
        public int RequiredRealm;
        public StatBlock BaseStats = new StatBlock();
        public string SetId;
        public int MaxRefineLevel = 10;
        public GameObject VisualPrefab;
        public int MaxGemSockets;
        public int MaxDurability = 100;
    }

    [Serializable]
    public class StatBlock
    {
        public float MaxHp;
        public float MaxMana;
        public float Attack;
        public float Defense;
        public float CritRate;
        public float CritDamage = 1.5f;
        public float MoveSpeed;
        public float AttackSpeed;
        public float FireBonus;
        public float IceBonus;
        public float LightningBonus;
        public float PoisonBonus;
        public float WindBonus;
        public float CultivationSpeed;
        public float DivineSense;

        public static StatBlock operator +(StatBlock a, StatBlock b)
        {
            a = a ?? Zero();
            b = b ?? Zero();

            return new StatBlock
            {
                MaxHp = a.MaxHp + b.MaxHp,
                MaxMana = a.MaxMana + b.MaxMana,
                Attack = a.Attack + b.Attack,
                Defense = a.Defense + b.Defense,
                CritRate = a.CritRate + b.CritRate,
                CritDamage = a.CritDamage + b.CritDamage,
                MoveSpeed = a.MoveSpeed + b.MoveSpeed,
                AttackSpeed = a.AttackSpeed + b.AttackSpeed,
                FireBonus = a.FireBonus + b.FireBonus,
                IceBonus = a.IceBonus + b.IceBonus,
                LightningBonus = a.LightningBonus + b.LightningBonus,
                PoisonBonus = a.PoisonBonus + b.PoisonBonus,
                WindBonus = a.WindBonus + b.WindBonus,
                CultivationSpeed = a.CultivationSpeed + b.CultivationSpeed,
                DivineSense = a.DivineSense + b.DivineSense
            };
        }

        public StatBlock Multiply(float multiplier)
        {
            return new StatBlock
            {
                MaxHp = MaxHp * multiplier,
                MaxMana = MaxMana * multiplier,
                Attack = Attack * multiplier,
                Defense = Defense * multiplier,
                CritRate = CritRate * multiplier,
                CritDamage = CritDamage * multiplier,
                MoveSpeed = MoveSpeed * multiplier,
                AttackSpeed = AttackSpeed * multiplier,
                FireBonus = FireBonus * multiplier,
                IceBonus = IceBonus * multiplier,
                LightningBonus = LightningBonus * multiplier,
                PoisonBonus = PoisonBonus * multiplier,
                WindBonus = WindBonus * multiplier,
                CultivationSpeed = CultivationSpeed * multiplier,
                DivineSense = DivineSense * multiplier
            };
        }

        private static StatBlock Zero()
        {
            return new StatBlock { CritDamage = 0f };
        }
    }
}
