using System;
using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Combat;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Player;
using Wendao.Systems.Skill;

namespace Wendao.Systems.Feedback
{
    public sealed class CombatFeedbackController : MonoBehaviour
    {
        private IAudioService _audio;
        private IVfxService _vfx;

        public int DamageFeedbackCount { get; private set; }
        public int SkillFeedbackCount { get; private set; }
        public int ReactionFeedbackCount { get; private set; }

        private void Awake()
        {
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DamageInfo>(
                CombatEvents.DamageApplied,
                HandleDamageApplied);
            EventBus.Subscribe<ElementReactionInfo>(
                CombatEvents.ElementReactionTriggered,
                HandleElementReaction);
            EventBus.Subscribe<SkillCastInfo>(
                SkillEvents.SkillCast,
                HandleSkillCast);
            EventBus.Subscribe<PlayerDodgeInfo>(
                PlayerEvents.Dodged,
                HandlePlayerDodged);
            EventBus.Subscribe<HealInfo>(
                CombatEvents.PlayerHealed,
                HandlePlayerHealed);
            EventBus.Subscribe<RealmChangeInfo>(
                CultivationEvents.RealmBreakthrough,
                HandleRealmBreakthrough);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DamageInfo>(
                CombatEvents.DamageApplied,
                HandleDamageApplied);
            EventBus.Unsubscribe<ElementReactionInfo>(
                CombatEvents.ElementReactionTriggered,
                HandleElementReaction);
            EventBus.Unsubscribe<SkillCastInfo>(
                SkillEvents.SkillCast,
                HandleSkillCast);
            EventBus.Unsubscribe<PlayerDodgeInfo>(
                PlayerEvents.Dodged,
                HandlePlayerDodged);
            EventBus.Unsubscribe<HealInfo>(
                CombatEvents.PlayerHealed,
                HandlePlayerHealed);
            EventBus.Unsubscribe<RealmChangeInfo>(
                CultivationEvents.RealmBreakthrough,
                HandleRealmBreakthrough);
        }

        private void HandleDamageApplied(DamageInfo info)
        {
            if (info.Amount <= 0f)
            {
                return;
            }

            ResolveServices();
            Vector3 position = ResolvePosition(info.HitPoint, info.Target);
            _vfx?.Play(
                ResolveHitVfx(info.Element, info.Type),
                position,
                Quaternion.identity,
                0.65f);
            _audio?.PlaySFXAt(
                info.HitstopSeconds + 0.0001f
                    >= CombatFeelSettings.HitstopLight34HeavySeconds
                    ? AudioContentIds.CombatHitHeavy
                    : AudioContentIds.CombatHitLight,
                position);
            DamageFeedbackCount++;
        }

        private void HandleElementReaction(ElementReactionInfo info)
        {
            if (info.Reaction == ElementReactionType.None)
            {
                return;
            }

            ResolveServices();
            Vector3 position = info.Target != null
                ? info.Target.transform.position + Vector3.up * 0.9f
                : Vector3.zero;
            _vfx?.Play(
                ResolveHitVfx(info.AttackElement, DamageType.Physical),
                position,
                Quaternion.identity,
                1f);
            _audio?.PlaySFXAt(ResolveElementSfx(info.AttackElement), position);
            ReactionFeedbackCount++;
        }

        private void HandleSkillCast(SkillCastInfo info)
        {
            ResolveServices();
            SkillData skill = ConfigDatabase.Instance?.GetSkill(info.SkillId);
            SkillElement element = skill != null
                ? skill.Element
                : SkillElement.None;
            _audio?.PlaySFXAt(ResolveSkillSfx(element), info.Origin);

            if (skill != null
                && !skill.IsProjectile
                && VfxContentIds.IsKnown(skill.ProjectileVfxId))
            {
                _vfx?.Play(
                    skill.ProjectileVfxId,
                    info.Origin,
                    Quaternion.identity,
                    0.8f);
            }

            if (skill != null
                && !skill.IsProjectile
                && VfxContentIds.IsKnown(skill.ImpactVfxId))
            {
                _vfx?.Play(
                    skill.ImpactVfxId,
                    info.TargetPoint,
                    Quaternion.identity,
                    0.8f);
            }

            SkillFeedbackCount++;
        }

        private void HandlePlayerDodged(PlayerDodgeInfo info)
        {
            ResolveServices();
            if (info.Player != null)
            {
                _audio?.PlaySFXAt(
                    AudioContentIds.CombatDodge,
                    info.Player.transform.position);
            }
            else
            {
                _audio?.PlaySFX(AudioContentIds.CombatDodge);
            }
        }

        private void HandlePlayerHealed(HealInfo info)
        {
            if (info.Amount <= 0f)
            {
                return;
            }

            ResolveServices();
            Vector3 position = info.Target != null
                ? info.Target.transform.position + Vector3.up
                : Vector3.zero;
            _vfx?.Play(
                VfxContentIds.Heal,
                position,
                Quaternion.identity,
                1.2f);
        }

        private void HandleRealmBreakthrough(RealmChangeInfo info)
        {
            if (!info.Success)
            {
                return;
            }

            ResolveServices();
            Vector3 position = Vector3.zero;
            if (ServiceLocator.TryGet<Wendao.Systems.Inventory.IPlayerHealthService>(
                    out Wendao.Systems.Inventory.IPlayerHealthService health)
                && health is Component component)
            {
                position = component.transform.position + Vector3.up;
            }

            _vfx?.Play(
                VfxContentIds.RealmBreakthrough,
                position,
                Quaternion.identity,
                2f);
            _audio?.PlaySFX(AudioContentIds.UiLevelUp);
        }

        private void ResolveServices()
        {
            if (_audio == null)
            {
                ServiceLocator.TryGet(out _audio);
            }

            if (_vfx == null)
            {
                ServiceLocator.TryGet(out _vfx);
            }
        }

        private static Vector3 ResolvePosition(
            Vector3 requested,
            GameObject target)
        {
            if (requested.sqrMagnitude > 0.0001f)
            {
                return requested;
            }

            return target != null
                ? target.transform.position + Vector3.up * 0.9f
                : Vector3.zero;
        }

        private static string ResolveHitVfx(
            ElementType element,
            DamageType damageType)
        {
            if (element == ElementType.Fire || damageType == DamageType.Fire)
            {
                return VfxContentIds.HitFire;
            }

            if (element == ElementType.Ice || damageType == DamageType.Ice)
            {
                return VfxContentIds.HitIce;
            }

            return VfxContentIds.HitPhysical;
        }

        private static string ResolveElementSfx(ElementType element)
        {
            switch (element)
            {
                case ElementType.Ice:
                case ElementType.Water:
                    return AudioContentIds.SkillIce;
                case ElementType.Lightning:
                    return AudioContentIds.SkillLightning;
                default:
                    return AudioContentIds.SkillFire;
            }
        }

        private static string ResolveSkillSfx(SkillElement element)
        {
            switch (element)
            {
                case SkillElement.Ice:
                    return AudioContentIds.SkillIce;
                case SkillElement.Lightning:
                    return AudioContentIds.SkillLightning;
                case SkillElement.Fire:
                    return AudioContentIds.SkillFire;
                default:
                    return AudioContentIds.CombatHitLight;
            }
        }
    }
}
