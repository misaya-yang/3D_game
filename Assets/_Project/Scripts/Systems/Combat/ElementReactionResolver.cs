using UnityEngine;
using Wendao.Data;

namespace Wendao.Systems.Combat
{
    internal readonly struct ElementReactionResolution
    {
        public ElementReactionResolution(
            ElementReactionType reaction,
            ElementType attackElement,
            string existingStatusId,
            float damageMultiplier)
        {
            Reaction = reaction;
            AttackElement = attackElement;
            ExistingStatusId = existingStatusId ?? string.Empty;
            DamageMultiplier = Mathf.Max(0f, damageMultiplier);
        }

        public ElementReactionType Reaction { get; }
        public ElementType AttackElement { get; }
        public string ExistingStatusId { get; }
        public float DamageMultiplier { get; }

        public static ElementReactionResolution None(ElementType attackElement)
        {
            return new ElementReactionResolution(
                ElementReactionType.None,
                attackElement,
                string.Empty,
                1f);
        }
    }

    internal static class ElementReactionResolver
    {
        public static ElementReactionResolution Resolve(
            ElementType attackElement,
            GameObject target,
            IStatusEffectService statusEffects)
        {
            if (attackElement == ElementType.None
                || target == null
                || statusEffects == null)
            {
                return ElementReactionResolution.None(attackElement);
            }

            switch (attackElement)
            {
                case ElementType.Fire:
                    if (statusEffects.TryGetStatusForAura(
                            target,
                            ElementType.Ice,
                            out string iceStatus))
                    {
                        return new ElementReactionResolution(
                            ElementReactionType.Melt,
                            attackElement,
                            iceStatus,
                            FormulaLibrary.MeltMultiplier);
                    }

                    if (statusEffects.TryGetStatusForAura(
                            target,
                            ElementType.Poison,
                            out string poisonStatus))
                    {
                        return new ElementReactionResolution(
                            ElementReactionType.BurnBurst,
                            attackElement,
                            poisonStatus,
                            FormulaLibrary.BurnBurstMultiplier);
                    }

                    break;
                case ElementType.Lightning:
                    if (statusEffects.TryGetStatusForAura(
                            target,
                            ElementType.Water,
                            out string wetStatus))
                    {
                        return new ElementReactionResolution(
                            ElementReactionType.Shock,
                            attackElement,
                            wetStatus,
                            FormulaLibrary.ShockMultiplier);
                    }

                    if (statusEffects.TryGetStatusForAura(
                            target,
                            ElementType.Ice,
                            out string lightningIceStatus))
                    {
                        return new ElementReactionResolution(
                            ElementReactionType.Shock,
                            attackElement,
                            lightningIceStatus,
                            FormulaLibrary.ShockMultiplier);
                    }

                    break;
                case ElementType.Wind:
                    if (statusEffects.TryGetFirstAura(
                            target,
                            out _,
                            out string spreadStatus))
                    {
                        return new ElementReactionResolution(
                            ElementReactionType.Spread,
                            attackElement,
                            spreadStatus,
                            1f);
                    }

                    break;
                case ElementType.Metal:
                    if (statusEffects.TryGetStatusForAura(
                            target,
                            ElementType.Wood,
                            out string woodStatus))
                    {
                        return new ElementReactionResolution(
                            ElementReactionType.Sever,
                            attackElement,
                            woodStatus,
                            FormulaLibrary.SeverMultiplier);
                    }

                    break;
            }

            return ElementReactionResolution.None(attackElement);
        }
    }
}
