using System;
using UnityEngine;

namespace Wendao.Data
{
    [Serializable]
    public struct DamageInfo
    {
        public GameObject Target;
        public GameObject Source;
        public float Amount;
        public DamageType Type;
        public bool IsCritical;
        public bool IsKillingBlow;
        public Vector3 HitPoint;
        public ElementType Element;
        public string SkillId;
        public ElementReactionType Reaction;
        public float ReactionMultiplier;
        public float HitstopSeconds;
        public float HitstunSeconds;
    }

    [Serializable]
    public struct HealInfo
    {
        public GameObject Target;
        public float Amount;
        public string SourceId;
    }

    [Serializable]
    public struct PlayerDodgeInfo
    {
        public GameObject Player;
        public Vector3 Direction;
    }

    [Serializable]
    public struct LockOnInfo
    {
        public GameObject Player;
        public GameObject Target;
        public bool Locked;
    }

    [Serializable]
    public struct StatusEffectInfo
    {
        public GameObject Target;
        public GameObject Source;
        public string StatusId;
        public int Stacks;
        public float RemainingDuration;
        public StatusEffectChangeType Change;
    }

    [Serializable]
    public struct ElementReactionInfo
    {
        public GameObject Target;
        public GameObject Source;
        public ElementReactionType Reaction;
        public ElementType AttackElement;
        public string ExistingStatusId;
        public float DamageMultiplier;
        public int SpreadTargetCount;
    }

    [Serializable]
    public struct DeathInfo
    {
        public GameObject Victim;
        public GameObject Killer;
        public Vector3 Position;
        public string LastHitSkillId;
    }

    [Serializable]
    public struct PlayerRespawnInfo
    {
        public GameObject Player;
        public string RespawnPointId;
        public Vector3 Position;
    }

    [Serializable]
    public struct EnemyDeathInfo
    {
        public string EnemyId;
        public EnemyRank Rank;
        public GameObject Victim;
        public GameObject Killer;
        public Vector3 Position;
    }

    [Serializable]
    public struct XpGainInfo
    {
        public float Amount;
        public XpSourceType Source;
        public float MultiplierApplied;
    }

    [Serializable]
    public struct CultivationXpPenaltyInfo
    {
        public float PreviousXp;
        public float AmountLost;
        public float CurrentXp;
        public float Percent;
    }

    [Serializable]
    public struct RealmChangeInfo
    {
        public RealmType PrevRealm;
        public RealmType NewRealm;
        public int PrevSubStage;
        public int NewSubStage;
        public bool Success;
        public float SuccessRate;
    }

    [Serializable]
    public struct ItemAcquireInfo
    {
        public string ItemId;
        public int Count;
        public AcquireSource Source;
    }

    [Serializable]
    public struct ItemUseInfo
    {
        public string ItemId;
        public int SlotIndex;
    }

    [Serializable]
    public struct CurrencyChangeInfo
    {
        public int Previous;
        public int Current;
        public int Delta;
    }

    [Serializable]
    public struct EquipmentChangeInfo
    {
        public EquipmentSlot Slot;
        public string OldItemId;
        public string NewItemId;
    }

    [Serializable]
    public struct EquipmentUpgradeInfo
    {
        public EquipmentSlot Slot;
        public string ItemId;
        public int NewRefineLevel;
        public bool Success;
    }

    [Serializable]
    public struct SkillInfo
    {
        public string SkillId;
        public int Level;
    }

    [Serializable]
    public struct SkillCastInfo
    {
        public string SkillId;
        public Vector3 Origin;
        public Vector3 TargetPoint;
        public GameObject TargetActor;
    }

    [Serializable]
    public struct SkillUpgradeInfo
    {
        public string SkillId;
        public int NewLevel;
    }

    [Serializable]
    public struct QuestInfo
    {
        public string QuestId;
        public QuestStatus Status;
    }

    [Serializable]
    public struct QuestProgressInfo
    {
        public string QuestId;
        public int ObjectiveIndex;
        public int Current;
        public int Required;
    }

    [Serializable]
    public struct DailyQuestProgressInfo
    {
        public string QuestId;
        public int Current;
        public int Required;
    }

    [Serializable]
    public struct DailyQuestResetInfo
    {
        public string CycleStartedUtc;
        public string NextResetUtc;
    }

    [Serializable]
    public struct DailyQuestClaimInfo
    {
        public string QuestId;
    }

    [Serializable]
    public struct DialogueInfo
    {
        public string NpcId;
        public string DialogueId;
        public bool Cancelled;
    }

    [Serializable]
    public struct CraftResultInfo
    {
        public string RecipeId;
        public string ResultItemId;
        public int ResultCount;
        public bool Success;
    }

    [Serializable]
    public struct ShopOpenInfo
    {
        public string VendorId;
    }

    [Serializable]
    public struct ShopTransactionInfo
    {
        public string VendorId;
        public ShopTransactionType Type;
        public string ItemId;
        public int Count;
        public int SpiritStonesDelta;
        public int InventorySlot;
    }

    [Serializable]
    public struct AffectionInfo
    {
        public string NpcId;
        public int OldValue;
        public int NewValue;
        public string MilestoneId;
    }

    [Serializable]
    public struct AchievementInfo
    {
        public string Id;
        public string DisplayName;
        public string Description;
    }

    [Serializable]
    public struct TitleInfo
    {
        public string TitleId;
        public bool Equipped;
    }

    [Serializable]
    public struct FactionReputationInfo
    {
        public string FactionId;
        public int Previous;
        public int Current;
        public int OldRank;
        public int NewRank;
    }

    [Serializable]
    public struct DayNightInfo
    {
        public bool IsNight;
        public float TimeOfDay;
    }

    [Serializable]
    public struct WeatherInfo
    {
        public WeatherId Weather;
        public float Intensity;
    }

    [Serializable]
    public struct MapInfo
    {
        public string MapId;
        public string SpawnId;
    }

    [Serializable]
    public struct BossPhaseInfo
    {
        public string BossId;
        public int OldPhase;
        public int NewPhase;
        public float HpPercent;
    }

    [Serializable]
    public struct MountInfo
    {
        public string MountId;
        public bool Mounted;
    }

    [Serializable]
    public struct FlightInfo
    {
        public bool IsFlying;
    }

    [Serializable]
    public struct SerendipityInfo
    {
        public string SerendipityId;
        public string MapId;
        public string WorldFlag;
    }

    [Serializable]
    public struct TutorialPromptInfo
    {
        public string TutorialId;
        public string StepId;
        public string LocalizationKey;
        public string DefaultValue;
        public float Duration;
        public bool IsForced;
        public bool CanDismiss;
        public Rect FocusRectNormalized;
    }

    [Serializable]
    public struct ToastInfo
    {
        public string LocalizationKey;
        public string DefaultValue;
        public float Duration;
    }

    // GameStateInfo is Core-owned because Core.GameManager publishes it.
}
