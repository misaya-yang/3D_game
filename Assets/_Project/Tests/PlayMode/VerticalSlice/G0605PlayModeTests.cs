using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.Enemy;
using Wendao.Entities.Player;
using Wendao.Systems.Combat;
using Wendao.Systems.Feedback;
using Wendao.Systems.Skill;
using Wendao.Systems.World;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0605PlayModeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            EventBus.Clear();
            DestroyRuntimeObjects();
            yield return null;
            ServiceLocator.Clear();
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
        public void StableFeedbackIdsMatchContentTable()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    "BGM_Explore_Qingshi",
                    "BGM_Explore_Cangwu",
                    "BGM_Combat_Normal",
                    "BGM_Boss_StoneGeneral",
                    "BGM_City_Qingshi"
                },
                AudioContentIds.Bgm);
            Assert.That(AudioContentIds.Sfx, Has.Length.EqualTo(10));
            Assert.That(AudioContentIds.Ambience, Has.Length.EqualTo(3));
            Assert.That(VfxContentIds.All, Has.Length.EqualTo(12));
            Assert.That(
                VfxContentIds.IsKnown("VFX_Boss_Charge_Warning"),
                Is.True);
            Assert.That(AudioContentIds.IsSfx("SFX_UI_Error"), Is.True);
        }

        [Test]
        public void ManagersProvidePlaceholderVfxSfxAndCrossfade()
        {
            VFXManager vfx = new GameObject("[G0605VFX]")
                .AddComponent<VFXManager>();
            AudioManager audio = new GameObject("[G0605Audio]")
                .AddComponent<AudioManager>();

            GameObject instance = vfx.Play(
                VfxContentIds.SkillEmber,
                Vector3.one,
                Quaternion.identity,
                0.5f);
            Assert.That(instance, Is.Not.Null);
            Assert.That(instance.GetComponent<ParticleSystem>(), Is.Not.Null);
            Assert.That(vfx.LastPlayedVfxId, Is.EqualTo("VFX_Skill_Ember"));
            Assert.That(vfx.ActiveCount, Is.EqualTo(1));

            audio.PlayBGM(AudioContentIds.ExploreQingshi, 0f);
            audio.CrossfadeBGM(AudioContentIds.CombatNormal, 1f);
            Assert.That(audio.CurrentBgmId, Is.EqualTo("BGM_Combat_Normal"));
            Assert.That(audio.PreviousBgmId, Is.EqualTo("BGM_Explore_Qingshi"));
            Assert.That(audio.CrossfadeDuration, Is.EqualTo(1f));
            audio.TickAudio(1f);
            Assert.That(audio.CrossfadeRemaining, Is.Zero);

            audio.PlaySFX(AudioContentIds.SkillFire);
            Assert.That(audio.LastSfxId, Is.EqualTo("SFX_Skill_Fire"));
            Assert.That(audio.SfxPlayCount, Is.EqualTo(1));
            vfx.Stop(instance);
        }

        [Test]
        public void AttackAnimationEventAppliesExactlyOneDamageFrame()
        {
            var service = new CapturingCombatService();
            var playerObject = new GameObject("[G0605Player]");
            playerObject.AddComponent<PlayerController>();
            playerObject.AddComponent<PlayerStats>();
            PlayerCombatController combat =
                playerObject.AddComponent<PlayerCombatController>();
            combat.SetCombatService(service);

            Assert.That(combat.TryStartLightAttack(), Is.True);
            combat.OnAttackHit();
            combat.OnAttackHit();
            combat.TickAttack(combat.CurrentWindup);
            Assert.That(service.MeleeRequests, Has.Count.EqualTo(1));
            Assert.That(combat.IsInRecovery, Is.True);

            combat.OnAnimEnd();
            Assert.That(combat.IsAttacking, Is.False);
            Assert.That(service.MeleeRequests, Has.Count.EqualTo(1));
        }

        [Test]
        public void SkillReactionAndBossTelegraphUseFeedbackServices()
        {
            ConfigDatabase database = new GameObject("[G0605Config]")
                .AddComponent<ConfigDatabase>();
            database.LoadAll();
            VFXManager vfx = new GameObject("[G0605VFX]")
                .AddComponent<VFXManager>();
            AudioManager audio = new GameObject("[G0605Audio]")
                .AddComponent<AudioManager>();
            CombatFeedbackController feedback =
                new GameObject("[G0605Feedback]")
                    .AddComponent<CombatFeedbackController>();

            var request = new DamageRequest
            {
                Source = new GameObject("Caster"),
                BaseDamage = 10f,
                Type = DamageType.Physical,
                Element = ElementType.None,
                Multiplier = 1f
            };
            SkillProjectile projectile = SkillProjectile.Spawn(
                SkillContentIds.BasicQiBolt,
                Vector3.zero,
                Vector3.forward * 5f,
                5f,
                request);
            Assert.That(projectile, Is.Not.Null);
            Assert.That(
                vfx.LastPlayedVfxId,
                Is.EqualTo(VfxContentIds.SkillQiBoltProjectile));

            EventBus.Publish(
                SkillEvents.SkillCast,
                new SkillCastInfo
                {
                    SkillId = SkillContentIds.FireEmber,
                    Origin = Vector3.zero,
                    TargetPoint = Vector3.forward
                });
            Assert.That(audio.LastSfxId, Is.EqualTo(AudioContentIds.SkillFire));
            Assert.That(feedback.SkillFeedbackCount, Is.EqualTo(1));

            var target = new GameObject("ReactionTarget");
            EventBus.Publish(
                CombatEvents.ElementReactionTriggered,
                new ElementReactionInfo
                {
                    Target = target,
                    Reaction = ElementReactionType.Melt,
                    AttackElement = ElementType.Ice
                });
            Assert.That(feedback.ReactionFeedbackCount, Is.EqualTo(1));
            Assert.That(vfx.LastPlayedVfxId, Is.EqualTo(VfxContentIds.HitIce));
            Assert.That(audio.LastSfxId, Is.EqualTo(AudioContentIds.SkillIce));

            var telegraphObject = new GameObject("BossTelegraph");
            BossSkillTelegraphView telegraph =
                telegraphObject.AddComponent<BossSkillTelegraphView>();
            telegraph.Show(
                new BossSkillTelegraph
                {
                    SkillId = "boss_sg_charge",
                    Shape = TelegraphShape.Line,
                    Duration = 0.8f,
                    RadiusOrLength = 5f,
                    VfxId = VfxContentIds.BossChargeWarning
                },
                Vector3.forward * 5f);
            Assert.That(telegraph.IsVisible, Is.True);
            Assert.That(
                vfx.LastPlayedVfxId,
                Is.EqualTo(VfxContentIds.BossChargeWarning));
        }

        [Test]
        public void BgmStateTransitionsExploreCombatBossAndBackAfterEightSeconds()
        {
            GameManager game = new GameObject("[G0605Game]")
                .AddComponent<GameManager>();
            AudioManager audio = new GameObject("[G0605Audio]")
                .AddComponent<AudioManager>();
            AudioStateController states = new GameObject("[G0605AudioState]")
                .AddComponent<AudioStateController>();

            states.RefreshForScene(SceneLoader.DefaultMapSceneName);
            Assert.That(states.State, Is.EqualTo(BgmPlaybackState.Explore));
            Assert.That(audio.CurrentBgmId, Is.EqualTo(AudioContentIds.ExploreQingshi));

            game.SetCombatFlag(true);
            states.TickState(0f);
            Assert.That(states.State, Is.EqualTo(BgmPlaybackState.Combat));
            Assert.That(audio.CurrentBgmId, Is.EqualTo(AudioContentIds.CombatNormal));
            Assert.That(
                states.LastTransitionDuration,
                Is.LessThanOrEqualTo(1f));

            states.SetBossEncounter(true);
            Assert.That(states.State, Is.EqualTo(BgmPlaybackState.Boss));
            Assert.That(
                audio.CurrentBgmId,
                Is.EqualTo(AudioContentIds.BossStoneGeneral));

            states.SetBossEncounter(false);
            game.SetCombatFlag(false);
            states.TickState(0f);
            Assert.That(states.State, Is.EqualTo(BgmPlaybackState.Combat));
            states.TickState(AudioStateController.PostCombatFlagTailSeconds - 0.01f);
            Assert.That(states.State, Is.EqualTo(BgmPlaybackState.Combat));
            states.TickState(0.02f);
            Assert.That(states.State, Is.EqualTo(BgmPlaybackState.Explore));
            Assert.That(audio.CurrentBgmId, Is.EqualTo(AudioContentIds.ExploreQingshi));

            states.RefreshForScene(SceneLoader.CangwuMapSceneName);
            Assert.That(audio.CurrentBgmId, Is.EqualTo(AudioContentIds.ExploreCangwu));
        }

        private static void DestroyRuntimeObjects()
        {
            DestroyAll<AudioStateController>();
            DestroyAll<CombatFeedbackController>();
            DestroyAll<VFXManager>();
            DestroyAll<AudioManager>();
            DestroyAll<SkillProjectile>();
            DestroyAll<BossSkillTelegraphView>();
            DestroyAll<PlayerController>();
            DestroyAll<ConfigDatabase>();
            DestroyAll<GameManager>();

            AudioSource[] sources = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include);
            for (int index = 0; index < sources.Length; index++)
            {
                if (sources[index] != null)
                {
                    Object.Destroy(sources[index].gameObject);
                }
            }
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

        private sealed class CapturingCombatService : ICombatService
        {
            public List<DamageRequest> MeleeRequests { get; } =
                new List<DamageRequest>();

            public DamageInfo ComputeDamage(DamageRequest request)
            {
                return default;
            }

            public DamageInfo ComputeDamage(
                DamageRequest request,
                IDamageable target)
            {
                return default;
            }

            public void DealDamage(IDamageable target, DamageRequest request)
            {
            }

            public bool TryMeleeHit(
                Transform attacker,
                float range,
                float angle,
                DamageRequest request)
            {
                MeleeRequests.Add(request);
                return true;
            }

            public void RegisterActor(IDamageable actor)
            {
            }

            public void UnregisterActor(IDamageable actor)
            {
            }
        }
    }
}
