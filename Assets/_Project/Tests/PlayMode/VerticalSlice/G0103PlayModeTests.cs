using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Combat;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0103PlayModeTests
    {
        private ConfigDatabase _database;
        private StatusEffectManager _statusEffects;
        private CombatSystem _combat;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            EventBus.Clear();
            DestroyRuntimeObjects();
            yield return null;
            ServiceLocator.Clear();

            _database = new GameObject("[G01-03 ConfigDatabase]")
                .AddComponent<ConfigDatabase>();
            _statusEffects = new GameObject("[G01-03 StatusEffectManager]")
                .AddComponent<StatusEffectManager>();
            _combat = new GameObject("[G01-03 CombatSystem]")
                .AddComponent<CombatSystem>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            EventBus.Clear();
            DestroyRuntimeObjects();
            yield return null;
            ServiceLocator.Clear();
        }

        [Test]
        public void BuiltInStatusLibraryCarriesAuthoritativeLifecycleData()
        {
            StatusEffectData burn = _database.GetStatusEffect(
                StatusEffectContentIds.Burn);
            StatusEffectData chill = _database.GetStatusEffect(
                StatusEffectContentIds.Chill);
            StatusEffectData freeze = _database.GetStatusEffect(
                StatusEffectContentIds.Freeze);
            StatusEffectData shock = _database.GetStatusEffect(
                StatusEffectContentIds.ShockStun);

            Assert.That(burn, Is.Not.Null);
            Assert.That(burn.Duration, Is.EqualTo(3f));
            Assert.That(burn.DotBaseDamageMultiplier, Is.EqualTo(0.05f));
            Assert.That(chill.MoveSpeedMod, Is.EqualTo(-0.3f));
            Assert.That(chill.MaxStacks, Is.EqualTo(2));
            Assert.That(
                chill.PromoteAtMaxStacksStatusId,
                Is.EqualTo(StatusEffectContentIds.Freeze));
            Assert.That(freeze.Stun, Is.True);
            Assert.That(freeze.Duration, Is.EqualTo(1.5f));
            Assert.That(freeze.ReapplyCooldown, Is.EqualTo(8f));
            Assert.That(shock.Stun, Is.True);
            Assert.That(shock.Duration, Is.EqualTo(0.5f));
        }

        [Test]
        public void StatusEffectsStackRefreshAndEmitExpiryRemoval()
        {
            G0103TestCombatActor target = CreateActor(
                "Status Target",
                CombatTeam.Enemy,
                Vector3.zero,
                1000f,
                0f);
            var observed = new List<StatusEffectInfo>();
            Action<StatusEffectInfo> handler = info => observed.Add(info);
            EventBus.Subscribe(CombatEvents.StatusEffectChanged, handler);
            try
            {
                _statusEffects.Apply(
                    StatusEffectContentIds.Poison,
                    target.gameObject,
                    null);
                _statusEffects.Tick(2f);
                _statusEffects.Apply(
                    StatusEffectContentIds.Poison,
                    target.gameObject,
                    null);

                Assert.That(
                    _statusEffects.GetStacks(
                        StatusEffectContentIds.Poison,
                        target.gameObject),
                    Is.EqualTo(2));
                Assert.That(
                    _statusEffects.GetRemainingDuration(
                        StatusEffectContentIds.Poison,
                        target.gameObject),
                    Is.EqualTo(4f).Within(0.0001f));

                _statusEffects.Tick(4.01f);
                Assert.That(
                    _statusEffects.Has(
                        StatusEffectContentIds.Poison,
                        target.gameObject),
                    Is.False);
            }
            finally
            {
                EventBus.Unsubscribe(CombatEvents.StatusEffectChanged, handler);
            }

            Assert.That(
                observed.Exists(info =>
                    info.StatusId == StatusEffectContentIds.Poison
                    && info.Change == StatusEffectChangeType.StackChanged
                    && info.Stacks == 2),
                Is.True);
            Assert.That(
                observed.Exists(info =>
                    info.StatusId == StatusEffectContentIds.Poison
                    && info.Change == StatusEffectChangeType.Expired
                    && info.Stacks == 0),
                Is.True);
        }

        [Test]
        public void MeltShockAndBurnBurstUseAuthoritativeMultipliers()
        {
            G0103TestCombatActor source = CreateActor(
                "Reaction Source",
                CombatTeam.Player,
                Vector3.left,
                1000f,
                0f);
            G0103TestCombatActor target = CreateActor(
                "Reaction Target",
                CombatTeam.Enemy,
                Vector3.zero,
                1000f,
                0f);
            var damages = new List<DamageInfo>();
            var reactions = new List<ElementReactionInfo>();
            Action<DamageInfo> damageHandler = info => damages.Add(info);
            Action<ElementReactionInfo> reactionHandler = info => reactions.Add(info);
            EventBus.Subscribe(CombatEvents.DamageApplied, damageHandler);
            EventBus.Subscribe(
                CombatEvents.ElementReactionTriggered,
                reactionHandler);
            try
            {
                _statusEffects.Apply(
                    StatusEffectContentIds.Chill,
                    target.gameObject,
                    source.gameObject);
                _combat.DealDamage(
                    target,
                    Request(source.gameObject, 100f, DamageType.Fire, ElementType.Fire));
                AssertDamage(
                    damages[damages.Count - 1],
                    150f,
                    ElementReactionType.Melt,
                    FormulaLibrary.MeltMultiplier);

                target.Configure(1000f, 0f, CombatTeam.Enemy);
                _statusEffects.ClearAll(target.gameObject);
                _statusEffects.Apply(
                    StatusEffectContentIds.Poison,
                    target.gameObject,
                    source.gameObject);
                _combat.DealDamage(
                    target,
                    Request(source.gameObject, 100f, DamageType.Fire, ElementType.Fire));
                AssertDamage(
                    damages[damages.Count - 1],
                    130f,
                    ElementReactionType.BurnBurst,
                    FormulaLibrary.BurnBurstMultiplier);
                Assert.That(
                    _statusEffects.Has(
                        StatusEffectContentIds.Poison,
                        target.gameObject),
                    Is.False);

                target.Configure(1000f, 0f, CombatTeam.Enemy);
                _statusEffects.ClearAll(target.gameObject);
                _statusEffects.Apply(
                    StatusEffectContentIds.Wet,
                    target.gameObject,
                    source.gameObject);
                _combat.DealDamage(
                    target,
                    Request(
                        source.gameObject,
                        100f,
                        DamageType.Lightning,
                        ElementType.Lightning));
                AssertDamage(
                    damages[damages.Count - 1],
                    140f,
                    ElementReactionType.Shock,
                    FormulaLibrary.ShockMultiplier);
                Assert.That(_statusEffects.IsStunned(target.gameObject), Is.True);
                _statusEffects.Tick(0.51f);
                Assert.That(_statusEffects.IsStunned(target.gameObject), Is.False);
                Assert.That(
                    _statusEffects.Has(
                        StatusEffectContentIds.Wet,
                        target.gameObject),
                    Is.True);
            }
            finally
            {
                EventBus.Unsubscribe(CombatEvents.DamageApplied, damageHandler);
                EventBus.Unsubscribe(
                    CombatEvents.ElementReactionTriggered,
                    reactionHandler);
            }

            Assert.That(reactions.Count, Is.EqualTo(3));
            Assert.That(reactions[0].ExistingStatusId, Is.EqualTo(
                StatusEffectContentIds.Chill));
            Assert.That(reactions[1].Reaction, Is.EqualTo(
                ElementReactionType.BurnBurst));
            Assert.That(reactions[2].Reaction, Is.EqualTo(
                ElementReactionType.Shock));
        }

        [Test]
        public void OnHitBurnUsesFivePercentSkillBasePerTick()
        {
            G0103TestCombatActor source = CreateActor(
                "Burn Source",
                CombatTeam.Player,
                Vector3.left,
                1000f,
                0f);
            G0103TestCombatActor target = CreateActor(
                "Burn Target",
                CombatTeam.Enemy,
                Vector3.zero,
                1000f,
                0f);
            DamageRequest request = Request(
                source.gameObject,
                100f,
                DamageType.Fire,
                ElementType.Fire);
            request.StatusOnHitId = StatusEffectContentIds.Burn;
            request.StatusChance = 1f;

            _combat.DealDamage(target, request);
            Assert.That(
                _statusEffects.Has(
                    StatusEffectContentIds.Burn,
                    target.gameObject),
                Is.True);
            Assert.That(target.CurrentHp, Is.EqualTo(900f).Within(0.0001f));

            _statusEffects.Tick(1f);
            Assert.That(target.CurrentHp, Is.EqualTo(895f).Within(0.0001f));
        }

        [Test]
        public void HeartDemonReducesFinalDamageByTenPercent()
        {
            G0103TestCombatActor source = CreateActor(
                "Heart Demon Source",
                CombatTeam.Player,
                Vector3.left,
                1000f,
                0f);
            G0103TestCombatActor target = CreateActor(
                "Heart Demon Target",
                CombatTeam.Enemy,
                Vector3.zero,
                1000f,
                0f);
            _statusEffects.Apply(
                StatusEffectContentIds.HeartDemon,
                source.gameObject,
                target.gameObject);

            _combat.DealDamage(
                target,
                Request(
                    source.gameObject,
                    100f,
                    DamageType.Physical,
                    ElementType.None));

            Assert.That(target.CurrentHp, Is.EqualTo(910f).Within(0.001f));
        }

        [Test]
        public void ChillPromotesToFreezeAndHonoursEightSecondReapplyCooldown()
        {
            G0103TestCombatActor target = CreateActor(
                "Freeze Target",
                CombatTeam.Enemy,
                Vector3.zero,
                1000f,
                0f);

            ApplyChillTwice(target.gameObject);
            Assert.That(
                _statusEffects.Has(
                    StatusEffectContentIds.Chill,
                    target.gameObject),
                Is.False);
            Assert.That(
                _statusEffects.Has(
                    StatusEffectContentIds.Freeze,
                    target.gameObject),
                Is.True);
            Assert.That(_statusEffects.IsStunned(target.gameObject), Is.True);

            _statusEffects.Tick(1.6f);
            Assert.That(_statusEffects.IsStunned(target.gameObject), Is.False);
            ApplyChillTwice(target.gameObject);
            Assert.That(
                _statusEffects.GetStacks(
                    StatusEffectContentIds.Chill,
                    target.gameObject),
                Is.EqualTo(2));
            Assert.That(
                _statusEffects.Has(
                    StatusEffectContentIds.Freeze,
                    target.gameObject),
                Is.False);

            _statusEffects.Tick(6.5f);
            ApplyChillTwice(target.gameObject);
            Assert.That(
                _statusEffects.Has(
                    StatusEffectContentIds.Freeze,
                    target.gameObject),
                Is.True);
        }

        [Test]
        public void WindSpreadsAurasAndMetalAppliesSeverDefenseBreak()
        {
            G0103TestCombatActor source = CreateActor(
                "Spread Source",
                CombatTeam.Player,
                new Vector3(-1f, 0f, 0f),
                1000f,
                0f);
            G0103TestCombatActor primary = CreateActor(
                "Spread Primary",
                CombatTeam.Enemy,
                Vector3.zero,
                1000f,
                0f);
            G0103TestCombatActor nearby = CreateActor(
                "Spread Nearby",
                CombatTeam.Enemy,
                new Vector3(3f, 0f, 0f),
                1000f,
                0f);
            G0103TestCombatActor distant = CreateActor(
                "Spread Distant",
                CombatTeam.Enemy,
                new Vector3(5f, 0f, 0f),
                1000f,
                0f);
            _combat.RegisterActor(primary);
            _combat.RegisterActor(nearby);
            _combat.RegisterActor(distant);

            ElementReactionInfo lastReaction = default;
            DamageInfo lastDamage = default;
            Action<ElementReactionInfo> reactionHandler = info => lastReaction = info;
            Action<DamageInfo> damageHandler = info => lastDamage = info;
            EventBus.Subscribe(
                CombatEvents.ElementReactionTriggered,
                reactionHandler);
            EventBus.Subscribe(CombatEvents.DamageApplied, damageHandler);
            try
            {
                _statusEffects.Apply(
                    StatusEffectContentIds.Wet,
                    primary.gameObject,
                    source.gameObject);
                _combat.DealDamage(
                    primary,
                    Request(source.gameObject, 10f, DamageType.Wind, ElementType.Wind));
                Assert.That(lastReaction.Reaction, Is.EqualTo(
                    ElementReactionType.Spread));
                Assert.That(lastReaction.SpreadTargetCount, Is.EqualTo(1));
                Assert.That(
                    _statusEffects.Has(
                        StatusEffectContentIds.Wet,
                        nearby.gameObject),
                    Is.True);
                Assert.That(
                    _statusEffects.Has(
                        StatusEffectContentIds.Wet,
                        distant.gameObject),
                    Is.False);

                primary.Configure(1000f, 100f, CombatTeam.Enemy);
                _statusEffects.ClearAll(primary.gameObject);
                _statusEffects.Apply(
                    StatusEffectContentIds.WoodMark,
                    primary.gameObject,
                    source.gameObject);
                _combat.DealDamage(
                    primary,
                    Request(
                        source.gameObject,
                        100f,
                        DamageType.Physical,
                        ElementType.Metal));
                AssertDamage(
                    lastDamage,
                    62.5f,
                    ElementReactionType.Sever,
                    FormulaLibrary.SeverMultiplier);
                Assert.That(
                    _statusEffects.Has(
                        StatusEffectContentIds.SeverDefense,
                        primary.gameObject),
                    Is.True);

                _combat.DealDamage(
                    primary,
                    Request(
                        source.gameObject,
                        100f,
                        DamageType.Physical,
                        ElementType.None));
                Assert.That(
                    lastDamage.Amount,
                    Is.EqualTo(100f * (100f / 190f)).Within(0.001f));
            }
            finally
            {
                EventBus.Unsubscribe(
                    CombatEvents.ElementReactionTriggered,
                    reactionHandler);
                EventBus.Unsubscribe(CombatEvents.DamageApplied, damageHandler);
            }
        }

        private void ApplyChillTwice(GameObject target)
        {
            _statusEffects.Apply(StatusEffectContentIds.Chill, target, null);
            _statusEffects.Apply(StatusEffectContentIds.Chill, target, null);
        }

        private G0103TestCombatActor CreateActor(
            string name,
            CombatTeam team,
            Vector3 position,
            float maxHp,
            float defense)
        {
            var actorObject = new GameObject("[G01-03 " + name + "]");
            actorObject.transform.position = position;
            G0103TestCombatActor actor =
                actorObject.AddComponent<G0103TestCombatActor>();
            actor.Configure(maxHp, defense, team);
            return actor;
        }

        private static DamageRequest Request(
            GameObject source,
            float baseDamage,
            DamageType type,
            ElementType element)
        {
            return new DamageRequest
            {
                Source = source,
                BaseDamage = baseDamage,
                Type = type,
                Element = element,
                Multiplier = 1f,
                CanCrit = false,
                SkillId = "skill_g0103_test",
                StatusOnHitId = string.Empty,
                StatusChance = 0f
            };
        }

        private static void AssertDamage(
            DamageInfo damage,
            float expectedAmount,
            ElementReactionType expectedReaction,
            float expectedMultiplier)
        {
            Assert.That(
                damage.Amount,
                Is.EqualTo(expectedAmount).Within(0.001f));
            Assert.That(damage.Reaction, Is.EqualTo(expectedReaction));
            Assert.That(
                damage.ReactionMultiplier,
                Is.EqualTo(expectedMultiplier).Within(0.0001f));
        }

        private static void DestroyRuntimeObjects()
        {
            DestroyAll<G0103TestCombatActor>();
            DestroyAll<StatusEffectManager>();
            DestroyAll<CombatSystem>();
            DestroyAll<ConfigDatabase>();
            DestroyAll<GameManager>();
        }

        private static void DestroyAll<T>() where T : Component
        {
            T[] instances = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            for (int index = 0; index < instances.Length; index++)
            {
                if (instances[index] != null)
                {
                    Object.Destroy(instances[index].gameObject);
                }
            }
        }
    }

    public sealed class G0103TestCombatActor : MonoBehaviour,
        IDamageable,
        ICombatStatsProvider,
        ICombatTeamProvider
    {
        public float CurrentHp { get; private set; }
        public float MaxHp { get; private set; }
        public bool IsDead => CurrentHp <= 0f;
        public float Attack { get; private set; }
        public float Defense { get; private set; }
        public float CritRate => 0f;
        public float CritDamage => FormulaLibrary.BaseCritDamage;
        public CombatTeam Team { get; private set; }

        public void Configure(
            float maxHp,
            float defense,
            CombatTeam team,
            float attack = 0f)
        {
            MaxHp = Mathf.Max(1f, maxHp);
            CurrentHp = MaxHp;
            Attack = Mathf.Max(0f, attack);
            Defense = Mathf.Max(0f, defense);
            Team = team;
        }

        public void ApplyDamage(DamageInfo info)
        {
            CurrentHp = Mathf.Max(0f, CurrentHp - Mathf.Max(0f, info.Amount));
        }

        public void ApplyHeal(float amount, string sourceId)
        {
            CurrentHp = Mathf.Min(MaxHp, CurrentHp + Mathf.Max(0f, amount));
        }
    }
}
