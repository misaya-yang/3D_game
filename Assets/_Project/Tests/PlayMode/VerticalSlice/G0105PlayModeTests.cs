using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Wendao.CameraSystem;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.Enemy;
using Wendao.Entities.Player;
using Wendao.Systems.Combat;
using Wendao.Systems.Input;
using Wendao.Systems.Skill;
using Wendao.UI.Combat;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0105PlayModeTests
    {
        private GameManager _gameManager;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            EventBus.Clear();
            DestroyRuntimeObjects();
            yield return null;
            ServiceLocator.Clear();

            _gameManager = new GameObject("[G01-05 GameManager]")
                .AddComponent<GameManager>();
            EnterPlayingState(_gameManager);
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
        public void CombatFeelSettingsMatchAuthoritativeDefaults()
        {
            Assert.That(
                CombatFeelSettings.InputBufferSeconds,
                Is.EqualTo(0.12f).Within(0.0001f));
            Assert.That(
                CombatFeelSettings.HitstopLight12Seconds,
                Is.EqualTo(0.03f).Within(0.0001f));
            Assert.That(
                CombatFeelSettings.HitstopLight34HeavySeconds,
                Is.EqualTo(0.06f).Within(0.0001f));
            Assert.That(
                CombatFeelSettings.NormalEnemyHitstunSeconds,
                Is.EqualTo(0.18f).Within(0.0001f));
            Assert.That(
                CombatFeelSettings.EliteHitstunMultiplier,
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(
                CombatFeelSettings.CriticalShakeIntensity,
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(
                CombatFeelSettings.CriticalShakeDuration,
                Is.EqualTo(0.15f).Within(0.0001f));
            Assert.That(
                CombatFeelSettings.ElementReactionFovKick,
                Is.EqualTo(2f).Within(0.0001f));
            Assert.That(
                CombatFeelSettings.ElementReactionFovDuration,
                Is.EqualTo(0.2f).Within(0.0001f));
        }

        [Test]
        public void BufferedPreInputChainsAttackAndCastsSkillWhenActorBecomesFree()
        {
            CreatePlayerRig(
                out PlayerController player,
                out PlayerActionBuffer buffer,
                out PlayerCombatController combat,
                out PlayerSkillController skill,
                out CapturingCombatService combatService,
                out CapturingSkillService skillService,
                out _);

            Assert.That(combat.TryStartLightAttack(), Is.True);
            buffer.EnqueueBufferedAction(BufferedActionType.LightAttack);
            Assert.That(combat.TryProcessBufferedCombatInput(), Is.False);
            buffer.TickBuffer(0.08f);
            combat.TickAttack(combat.CurrentWindup);
            Assert.That(combat.TryProcessBufferedCombatInput(), Is.True);
            Assert.That(
                buffer.HasBufferedAction(BufferedActionType.LightAttack),
                Is.False);

            combat.TickAttack(combat.CurrentRecovery);
            Assert.That(combat.IsAttacking, Is.True);
            Assert.That(combat.CurrentComboStep, Is.EqualTo(2));
            Assert.That(combatService.MeleeRequests.Count, Is.EqualTo(1));

            combat.TickAttack(
                combat.CurrentWindup + combat.CurrentRecovery - 0.05f);
            buffer.EnqueueBufferedAction(BufferedActionType.Skill1);
            Assert.That(skill.TryProcessBufferedSkillInput(), Is.False);
            buffer.TickBuffer(0.05f);
            combat.TickAttack(0.05f);
            Assert.That(combat.IsAttacking, Is.False);
            Assert.That(player.State, Is.Not.EqualTo(PlayerState.SkillCast));
            player.ForceState(PlayerState.Idle);
            Assert.That(skill.TryProcessBufferedSkillInput(), Is.True);
            Assert.That(skillService.CastCount, Is.EqualTo(1));
            Assert.That(
                buffer.HasBufferedAction(BufferedActionType.Skill1),
                Is.False);
        }

        [Test]
        public void BufferExpiresAtOneHundredTwentyMillisecondsAndClearsOnDialogueDeath()
        {
            var buffer = new GameObject("[G01-05 Buffer]")
                .AddComponent<PlayerActionBuffer>();

            buffer.EnqueueBufferedAction(BufferedActionType.HeavyAttack);
            buffer.TickBuffer(0.119f);
            Assert.That(
                buffer.HasBufferedAction(BufferedActionType.HeavyAttack),
                Is.True);
            buffer.TickBuffer(0.0011f);
            Assert.That(
                buffer.HasBufferedAction(BufferedActionType.HeavyAttack),
                Is.False);

            buffer.EnqueueBufferedAction(BufferedActionType.Dodge);
            Assert.That(_gameManager.TrySetState(GameState.Dialogue), Is.True);
            Assert.That(buffer.BufferedCount, Is.Zero);
            Assert.That(_gameManager.TrySetState(GameState.Playing), Is.True);

            buffer.EnqueueBufferedAction(BufferedActionType.Skill1);
            Assert.That(_gameManager.TrySetState(GameState.Dead), Is.True);
            Assert.That(buffer.BufferedCount, Is.Zero);
        }

        [Test]
        public void BufferedDodgeSurvivesWindupAndCancelsAsRecoveryBegins()
        {
            CreatePlayerRig(
                out PlayerController player,
                out PlayerActionBuffer buffer,
                out PlayerCombatController combat,
                out _,
                out _,
                out _,
                out _);

            Assert.That(combat.TryStartHeavyAttack(), Is.True);
            combat.TickAttack(CombatFeelSettings.InputBufferSeconds + 0.18f);
            buffer.EnqueueBufferedAction(BufferedActionType.Dodge);
            buffer.TickBuffer(0.04f);
            Assert.That(combat.TryProcessBufferedCombatInput(), Is.False);

            combat.TickAttack(0.05f);
            Assert.That(combat.IsInRecovery, Is.True);
            Assert.That(combat.TryProcessBufferedCombatInput(), Is.True);
            Assert.That(combat.IsAttacking, Is.False);
            Assert.That(player.State, Is.EqualTo(PlayerState.Dodge));
            Assert.That(
                buffer.HasBufferedAction(BufferedActionType.Dodge),
                Is.False);
        }

        [Test]
        public void LightComboAndHeavyCarryTheirRequiredHitstopDurations()
        {
            CreatePlayerRig(
                out PlayerController player,
                out _,
                out PlayerCombatController combat,
                out _,
                out CapturingCombatService service,
                out _,
                out _);

            Assert.That(combat.TryStartLightAttack(), Is.True);
            for (int step = 1; step <= 4; step++)
            {
                Assert.That(combat.CurrentComboStep, Is.EqualTo(step));
                combat.TickAttack(combat.CurrentWindup);
                DamageRequest request = service.MeleeRequests[step - 1];
                float expected = step <= 2
                    ? CombatFeelSettings.HitstopLight12Seconds
                    : CombatFeelSettings.HitstopLight34HeavySeconds;
                Assert.That(
                    request.HitstopSeconds,
                    Is.EqualTo(expected).Within(0.0001f));
                Assert.That(
                    request.HitstunSeconds,
                    Is.EqualTo(CombatFeelSettings.NormalEnemyHitstunSeconds)
                        .Within(0.0001f));

                if (step < 4)
                {
                    Assert.That(combat.TryQueueNextLightAttack(), Is.True);
                }

                combat.TickAttack(combat.CurrentRecovery);
            }

            player.ForceState(PlayerState.Idle);
            Assert.That(combat.TryStartHeavyAttack(), Is.True);
            combat.TickAttack(combat.CurrentWindup);
            DamageRequest heavy = service.MeleeRequests[4];
            Assert.That(
                heavy.HitstopSeconds,
                Is.EqualTo(CombatFeelSettings.HitstopLight34HeavySeconds)
                    .Within(0.0001f));
        }

        [Test]
        public void ResolvedDamagePlaysHitstopAndPauseSuspendsCountdown()
        {
            CombatFeelController feel = new GameObject("[G01-05 Feel]")
                .AddComponent<CombatFeelController>();

            EventBus.Publish(
                CombatEvents.DamageApplied,
                new DamageInfo
                {
                    Amount = 10f,
                    HitstopSeconds =
                        CombatFeelSettings.HitstopLight34HeavySeconds
                });
            Assert.That(feel.IsHitstopActive, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(
                feel.HitstopRemaining,
                Is.EqualTo(0.06f).Within(0.0001f));

            feel.TickHitstop(0.03f);
            Assert.That(feel.IsHitstopActive, Is.True);
            Assert.That(_gameManager.TrySetState(GameState.Paused), Is.True);
            feel.TickHitstop(1f);
            Assert.That(
                feel.HitstopRemaining,
                Is.EqualTo(0.03f).Within(0.0001f));
            Assert.That(Time.timeScale, Is.Zero);

            Assert.That(_gameManager.TrySetState(GameState.Playing), Is.True);
            feel.TickHitstop(0.031f);
            Assert.That(feel.IsHitstopActive, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void CriticalAndElementReactionDriveExistingCameraAndColoredText()
        {
            CreatePlayerRig(
                out PlayerController player,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _);
            ThirdPersonCamera camera = CreateCamera(player.transform);
            DamageFloatingTextView floatingText =
                new GameObject("[G01-05 FloatingText]")
                    .AddComponent<DamageFloatingTextView>();
            GameObject target = new GameObject("[G01-05 Reaction Target]");
            target.transform.position = new Vector3(0f, 0f, 3f);

            EventBus.Publish(
                CombatEvents.DamageApplied,
                new DamageInfo
                {
                    Target = target,
                    Source = player.gameObject,
                    Amount = 20f,
                    IsCritical = true,
                    HitPoint = target.transform.position
                });
            Assert.That(camera.CriticalShakeCount, Is.EqualTo(1));
            Assert.That(
                camera.ShakeIntensity,
                Is.EqualTo(CombatFeelSettings.CriticalShakeIntensity)
                    .Within(0.0001f));
            Assert.That(
                camera.ShakeRemaining,
                Is.EqualTo(CombatFeelSettings.CriticalShakeDuration)
                    .Within(0.0001f));

            EventBus.Publish(
                CombatEvents.ElementReactionTriggered,
                new ElementReactionInfo
                {
                    Target = target,
                    Source = player.gameObject,
                    Reaction = ElementReactionType.Shock,
                    AttackElement = ElementType.Lightning,
                    ExistingStatusId = "status_wet",
                    DamageMultiplier = 1.4f
                });
            Assert.That(camera.ReactionFovPulseCount, Is.EqualTo(1));
            Assert.That(
                camera.ReactionFovOffset,
                Is.EqualTo(CombatFeelSettings.ElementReactionFovKick)
                    .Within(0.0001f));
            Assert.That(floatingText.ReactionNumberCount, Is.EqualTo(1));
            Assert.That(
                floatingText.LastReaction,
                Is.EqualTo(ElementReactionType.Shock));
            Assert.That(
                floatingText.LastReactionLocalizationKey,
                Is.EqualTo("reaction_name_shock"));
            Assert.That(
                floatingText.LastReactionRenderedText,
                Is.EqualTo("感电"));
            Assert.That(
                floatingText.LastReactionColor,
                Is.EqualTo(DamageFloatingTextView.GetReactionColor(
                    ElementReactionType.Shock)));

            camera.TickCamera(0.1f);
            Assert.That(camera.ReactionFovOffset, Is.GreaterThan(0f));
            camera.TickCamera(0.1f);
            Assert.That(camera.ReactionFovOffset, Is.Zero.Within(0.0001f));
            Assert.That(
                camera.CurrentFov,
                Is.EqualTo(ThirdPersonCamera.CombatFov).Within(0.001f));
        }

        [Test]
        public void MeleeHitstunUsesFullNormalAndHalfEliteDuration()
        {
            CombatSystem combat = new GameObject("[G01-05 CombatSystem]")
                .AddComponent<CombatSystem>();

            TrainingDummy dummy = GameObject.CreatePrimitive(PrimitiveType.Cube)
                .AddComponent<TrainingDummy>();
            dummy.ConfigureStats(100f, 0f);
            combat.DealDamage(
                dummy,
                new DamageRequest
                {
                    BaseDamage = 1f,
                    Multiplier = 1f,
                    Type = DamageType.Physical,
                    HitstunSeconds =
                        CombatFeelSettings.NormalEnemyHitstunSeconds
                });
            Assert.That(
                dummy.HitstunRemaining,
                Is.EqualTo(CombatFeelSettings.NormalEnemyHitstunSeconds)
                    .Within(0.0001f));

            GameObject eliteObject = new GameObject("[G01-05 Elite]");
            EnemyBrain elite = eliteObject.AddComponent<EnemyBrain>();
            EnemyData eliteData = ScriptableObject.CreateInstance<EnemyData>();
            eliteData.Id = "enemy_g0105_elite";
            eliteData.Rank = EnemyRank.Elite;
            eliteData.MaxHp = 100f;
            eliteData.AttackRange = 1f;
            eliteData.DisengageRange = 14f;
            elite.SpawnInit(eliteData, Vector3.zero);

            combat.DealDamage(
                elite,
                new DamageRequest
                {
                    BaseDamage = 1f,
                    Multiplier = 1f,
                    Type = DamageType.Physical,
                    HitstunSeconds =
                        CombatFeelSettings.NormalEnemyHitstunSeconds
                });
            Assert.That(
                elite.HitstunRemaining,
                Is.EqualTo(
                        CombatFeelSettings.NormalEnemyHitstunSeconds
                        * CombatFeelSettings.EliteHitstunMultiplier)
                    .Within(0.0001f));

            Object.DestroyImmediate(eliteData);
        }

        private static void CreatePlayerRig(
            out PlayerController player,
            out PlayerActionBuffer buffer,
            out PlayerCombatController combat,
            out PlayerSkillController skill,
            out CapturingCombatService combatService,
            out CapturingSkillService skillService,
            out FakePlayerInputSource input)
        {
            GameObject playerObject = new GameObject("[G01-05 Player]");
            player = playerObject.AddComponent<PlayerController>();
            playerObject.AddComponent<PlayerStats>();
            buffer = playerObject.AddComponent<PlayerActionBuffer>();
            combat = playerObject.AddComponent<PlayerCombatController>();
            skill = playerObject.AddComponent<PlayerSkillController>();

            input = new FakePlayerInputSource();
            combatService = new CapturingCombatService();
            skillService = new CapturingSkillService();
            buffer.SetInputSource(input);
            player.SetInputSource(input);
            player.SetActionBuffer(buffer);
            combat.SetInputSource(input);
            combat.SetActionBuffer(buffer);
            combat.SetCombatService(combatService);
            skill.SetInputSource(input);
            skill.SetActionBuffer(buffer);
            skill.SetSkillService(skillService);
        }

        private static ThirdPersonCamera CreateCamera(Transform target)
        {
            GameObject cameraObject = new GameObject("[G01-05 Camera]");
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<UnityEngine.Camera>();
            ThirdPersonCamera camera =
                cameraObject.AddComponent<ThirdPersonCamera>();
            camera.SetTarget(target);
            return camera;
        }

        private static void EnterPlayingState(GameManager gameManager)
        {
            Assert.That(gameManager.TrySetState(GameState.MainMenu), Is.True);
            Assert.That(gameManager.TrySetState(GameState.Loading), Is.True);
            Assert.That(gameManager.TrySetState(GameState.Playing), Is.True);
        }

        private static void DestroyRuntimeObjects()
        {
            DestroyAll<DamageFloatingTextView>();
            DestroyAll<ThirdPersonCamera>();
            DestroyAll<EnemyBrain>();
            DestroyAll<TrainingDummy>();
            DestroyAll<PlayerController>();
            DestroyAll<CombatFeelController>();
            DestroyAll<CombatSystem>();
            DestroyAll<EventSystem>();
            DestroyAll<GameManager>();
        }

        private static void DestroyAll<T>() where T : Component
        {
            T[] instances = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            foreach (T instance in instances)
            {
                if (instance != null)
                {
                    Object.Destroy(instance.gameObject);
                }
            }
        }

        private sealed class FakePlayerInputSource : IPlayerInputSource
        {
            public Vector2 Move { get; set; }
            public Vector2 Look { get; set; }
            public bool LookIsPointerDelta { get; set; }
            public bool JumpPressedThisFrame { get; set; }
            public bool JumpHeld { get; set; }
            public bool SprintHeld { get; set; }
            public bool LightAttackPressedThisFrame { get; set; }
            public bool HeavyAttackPressedThisFrame { get; set; }
            public bool DodgePressedThisFrame { get; set; }
            public bool BlockHeld { get; set; }
            public bool LockOnPressedThisFrame { get; set; }
            public bool Skill1PressedThisFrame { get; set; }
            public bool Skill2PressedThisFrame { get; set; }
            public bool Skill3PressedThisFrame { get; set; }
            public bool Skill4PressedThisFrame { get; set; }
            public bool InteractPressedThisFrame { get; set; }
            public bool OpenInventoryPressedThisFrame { get; set; }
            public bool OpenCharacterPressedThisFrame { get; set; }
            public bool OpenSkillPressedThisFrame { get; set; }
            public bool OpenQuestPressedThisFrame { get; set; }
            public bool OpenMapPressedThisFrame { get; set; }
            public bool PausePressedThisFrame { get; set; }
            public bool MountPressedThisFrame { get; set; }
            public bool IsEnabled { get; private set; } = true;

            public void SetEnabled(bool enabled)
            {
                IsEnabled = enabled;
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

        private sealed class CapturingSkillService : ISkillService
        {
            private readonly string[] _equippedIds = new string[4];

            public IReadOnlyList<SkillRuntime> Learned =>
                Array.Empty<SkillRuntime>();
            public string[] EquippedIds => _equippedIds;
            public bool IsCasting => false;
            public int CastCount { get; private set; }

            public bool Learn(string skillId)
            {
                return false;
            }

            public bool Equip(string skillId, int barIndex)
            {
                return false;
            }

            public bool Unequip(int barIndex)
            {
                return false;
            }

            public bool CanCast(int barIndex)
            {
                return true;
            }

            public bool TryCast(
                int barIndex,
                Vector3 targetPoint,
                GameObject targetActor)
            {
                CastCount++;
                return true;
            }

            public void TickCooldowns(float deltaTime)
            {
            }

            public bool TryUpgrade(string skillId)
            {
                return false;
            }

            public float GetCooldownRemaining(int barIndex)
            {
                return 0f;
            }
        }
    }
}
