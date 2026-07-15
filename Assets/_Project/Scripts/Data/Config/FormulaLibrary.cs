using UnityEngine;

namespace Wendao.Data
{
    public static class FormulaLibrary
    {
        public const float DefenseConstant = 100f;
        public const float BaseCritDamage = 1.5f;
        public const float GlobalDamageMin = 1f;

        public const float MeltMultiplier = 1.5f;
        public const float BurnBurstMultiplier = 1.3f;
        public const float ShockMultiplier = 1.4f;
        public const float SeverMultiplier = 1.25f;
        public const float SpreadRadius = 4f;

        public const float RefineStatPerLevel = 0.05f;
        public const float RefineBaseSuccess = 0.95f;
        public const float RefineMinimumSuccess = 0.4f;
        public const float RefineSuccessLossPerLevel = 0.03f;
        public const int RefineMaterialBaseCost = 1;
        public const int RefineLevelsPerMaterialIncrease = 2;

        public const float DeathXpPenaltyPercent = 0.05f;
        public const float OutOfCombatRecoveryDelay = 5f;
        public const float OutOfCombatHpRecoveryPerSecond = 0.02f;
        public const float OutOfCombatManaRecoveryPerSecond = 0.03f;

        public static float CalculateRawDamage(
            float skillBase,
            float attack,
            float skillCoefficient)
        {
            return Mathf.Max(0f, skillBase)
                * (1f + Mathf.Max(0f, attack) / 100f)
                * Mathf.Max(0f, skillCoefficient);
        }

        public static float ApplyDefense(float rawDamage, float defense, bool isTrueDamage = false)
        {
            float nonNegativeRaw = Mathf.Max(0f, rawDamage);
            if (isTrueDamage)
            {
                return nonNegativeRaw;
            }

            return nonNegativeRaw
                * (DefenseConstant / (DefenseConstant + Mathf.Max(0f, defense)));
        }

        public static float ApplyElementModifier(
            float damage,
            float elementBonus,
            float elementResistance)
        {
            return Mathf.Max(0f, damage * (1f + elementBonus - elementResistance));
        }

        public static float ApplyCritical(float damage, bool isCritical, float critDamage)
        {
            return isCritical ? damage * Mathf.Max(0f, critDamage) : damage;
        }

        public static float FinalizeDamage(float damage)
        {
            return Mathf.Max(GlobalDamageMin, damage);
        }

        public static float GetRefineStatMultiplier(int refineLevel)
        {
            return 1f + RefineStatPerLevel * Mathf.Max(0, refineLevel);
        }

        public static float GetRefineSuccessRate(int currentRefineLevel)
        {
            return Mathf.Max(
                RefineMinimumSuccess,
                RefineBaseSuccess - RefineSuccessLossPerLevel * Mathf.Max(0, currentRefineLevel));
        }

        public static int GetRefineMaterialCost(int currentRefineLevel)
        {
            return RefineMaterialBaseCost
                + Mathf.Max(0, currentRefineLevel)
                    / RefineLevelsPerMaterialIncrease;
        }
    }
}
