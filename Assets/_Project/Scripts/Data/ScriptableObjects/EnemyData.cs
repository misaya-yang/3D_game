using System;
using UnityEngine;

namespace Wendao.Data
{
    [CreateAssetMenu(fileName = "Enemy_New", menuName = "问道/敌人/EnemyData")]
    public class EnemyData : ScriptableObject
    {
        public string Id;
        public string DisplayNameKey;
        public string DisplayName;
        public EnemyRank Rank;
        public RealmType Realm;
        public int SubStage;
        public float MaxHp;
        public float Attack;
        public float Defense;
        public float MoveSpeed;
        public float AggroRange;
        public float AttackRange;
        public float DisengageRange;
        public float AttackInterval;
        public float CultivationXpReward;
        public int MinSpiritStones;
        public int MaxSpiritStones;
        public LootEntry[] LootTable = Array.Empty<LootEntry>();
        public string[] SkillIds = Array.Empty<string>();
        public BossPhase[] BossPhases = Array.Empty<BossPhase>();
        public GameObject Prefab;
    }

    [Serializable]
    public class LootEntry
    {
        public string ItemId;
        public int MinCount;
        public int MaxCount;
        public float DropChance;
    }

    [Serializable]
    public class BossPhase
    {
        public int PhaseIndex;
        public float HpThreshold;
        public string[] SkillIds = Array.Empty<string>();
        public string PhaseVfxId;
        public bool InvulnerableDuringTransition;
        public BossSkillTelegraph[] Telegraphs = Array.Empty<BossSkillTelegraph>();
    }

    [Serializable]
    public class BossSkillTelegraph
    {
        public string SkillId;
        public TelegraphShape Shape;
        public float Duration;
        public float RadiusOrLength;
        public float Angle;
        public string VfxId;
        public float RecoverStun;
        public bool Interruptible;
    }
}
