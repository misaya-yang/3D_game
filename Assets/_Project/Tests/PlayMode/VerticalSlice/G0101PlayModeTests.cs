using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.Player;
using Wendao.Systems.Combat;
using Wendao.Systems.Input;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0101PlayModeTests
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
        public void CombatInputsExposeKeyboardMouseAndGamepadBindings()
        {
            InputActionAsset asset = Resources.Load<InputActionAsset>(
                PlayerInputReader.DefaultActionAssetResourcePath);
            Assert.That(asset, Is.Not.Null);
            InputActionMap map = asset.FindActionMap(
                PlayerInputReader.PlayerActionMapName,
                true);

            InputAction heavy = map.FindAction("HeavyAttack", true);
            AssertBinding(heavy, "<Mouse>/rightButton");
            AssertBinding(heavy, "<Gamepad>/buttonNorth");

            InputAction dodge = map.FindAction("Dodge", true);
            AssertBinding(dodge, "<Keyboard>/leftCtrl");
            AssertBinding(dodge, "<Keyboard>/q");
            AssertBinding(dodge, "<Gamepad>/buttonEast");

            InputAction block = map.FindAction("Block", true);
            AssertBinding(block, "<Mouse>/backButton");
            AssertBinding(block, "<Keyboard>/f");
            AssertBinding(block, "<Gamepad>/leftTrigger");
        }

        [Test]
        public void FourHitComboUsesSpecifiedMultipliersAndTimings()
        {
            var service = new CapturingCombatService();
            CreatePlayerRig(
                service,
                out PlayerController player,
                out _,
                out PlayerCombatController combat);

            float[] expectedMultipliers = { 1f, 1.1f, 1.25f, 1.5f };
            float[] expectedWindups = { 0.1f, 0.1f, 0.12f, 0.15f };
            float[] expectedRecoveries = { 0.25f, 0.25f, 0.3f, 0.45f };

            Assert.That(combat.TryStartLightAttack(), Is.True);
            for (int index = 0; index < expectedMultipliers.Length; index++)
            {
                int step = index + 1;
                Assert.That(player.State, Is.EqualTo(PlayerState.LightAttack));
                Assert.That(combat.CurrentComboStep, Is.EqualTo(step));
                Assert.That(
                    combat.CurrentMultiplier,
                    Is.EqualTo(expectedMultipliers[index]).Within(0.0001f));
                Assert.That(
                    combat.CurrentWindup,
                    Is.EqualTo(expectedWindups[index]).Within(0.0001f));
                Assert.That(
                    combat.CurrentRecovery,
                    Is.EqualTo(expectedRecoveries[index]).Within(0.0001f));

                combat.TickAttack(combat.CurrentWindup);
                Assert.That(service.MeleeRequests.Count, Is.EqualTo(step));
                Assert.That(
                    service.MeleeRequests[index].Multiplier,
                    Is.EqualTo(expectedMultipliers[index]).Within(0.0001f));
                Assert.That(combat.IsInRecovery, Is.True);

                if (step < PlayerCombatController.MaximumLightComboStep)
                {
                    Assert.That(combat.TryQueueNextLightAttack(), Is.True);
                }

                combat.TickAttack(combat.CurrentRecovery);
            }

            Assert.That(combat.IsAttacking, Is.False);
            Assert.That(combat.CurrentComboStep, Is.Zero);
            Assert.That(player.State, Is.Not.EqualTo(PlayerState.LightAttack));
        }

        [Test]
        public void ComboWindowExpiresAndRestartsAtLightOne()
        {
            var service = new CapturingCombatService();
            CreatePlayerRig(
                service,
                out _,
                out _,
                out PlayerCombatController combat);

            Assert.That(combat.TryStartLightAttack(), Is.True);
            combat.TickAttack(combat.CurrentWindup + combat.CurrentRecovery);

            Assert.That(combat.IsAttacking, Is.False);
            Assert.That(combat.CurrentComboStep, Is.EqualTo(1));
            Assert.That(
                combat.ComboWindowRemaining,
                Is.EqualTo(0.1f).Within(0.0001f));

            combat.TickComboContinuation(0.101f);
            Assert.That(combat.CurrentComboStep, Is.Zero);
            Assert.That(combat.TryStartLightAttack(), Is.True);
            Assert.That(combat.CurrentComboStep, Is.EqualTo(1));
        }

        [Test]
        public void HeavyAttackUsesSpecifiedMultiplierWindupAndRecovery()
        {
            var service = new CapturingCombatService();
            CreatePlayerRig(
                service,
                out PlayerController player,
                out _,
                out PlayerCombatController combat);

            Assert.That(combat.TryStartHeavyAttack(), Is.True);
            Assert.That(player.State, Is.EqualTo(PlayerState.HeavyAttack));
            Assert.That(
                combat.CurrentMultiplier,
                Is.EqualTo(2f).Within(0.0001f));
            Assert.That(
                combat.CurrentWindup,
                Is.EqualTo(0.35f).Within(0.0001f));
            Assert.That(
                combat.CurrentRecovery,
                Is.EqualTo(0.5f).Within(0.0001f));

            combat.TickAttack(combat.CurrentWindup);
            Assert.That(service.MeleeRequests.Count, Is.EqualTo(1));
            Assert.That(
                service.MeleeRequests[0].Multiplier,
                Is.EqualTo(2f).Within(0.0001f));
            Assert.That(combat.IsInRecovery, Is.True);

            combat.TickAttack(combat.CurrentRecovery);
            Assert.That(combat.IsAttacking, Is.False);
        }

        [Test]
        public void DodgeHasPointTwoSecondInvincibilityFiveMeterTravelAndCooldown()
        {
            CombatSystem damageSystem = new GameObject("[CombatSystem]")
                .AddComponent<CombatSystem>();
            CreatePlayerRig(
                damageSystem,
                out PlayerController player,
                out PlayerStats stats,
                out _);
            player.TeleportTo(
                new Vector3(10000f, 10000f, 10000f),
                Quaternion.identity);
            stats.ConfigureBaseStats(100f, 0f, 0f, 0f, 1.5f);

            var request = new DamageRequest
            {
                BaseDamage = 100f,
                Type = DamageType.Physical,
                Element = ElementType.None,
                Multiplier = 1f,
                CanCrit = false,
                SkillId = string.Empty
            };

            Vector3 dodgeStart = player.transform.position;
            Assert.That(player.TryStartDodge(Vector3.forward), Is.True);
            Assert.That(player.IsInvincible, Is.True);
            Assert.That(
                damageSystem.ComputeDamage(request, stats).Amount,
                Is.Zero);

            player.TickDodgeState(0.199f);
            Assert.That(player.IsInvincible, Is.True);
            player.TickDodgeState(0.0011f);
            Assert.That(player.IsInvincible, Is.False);
            Assert.That(
                damageSystem.ComputeDamage(request, stats).Amount,
                Is.EqualTo(100f).Within(0.0001f));

            player.TickDodgeState(0.2f);
            Assert.That(player.State, Is.Not.EqualTo(PlayerState.Dodge));
            Assert.That(
                player.DodgeDistanceTravelled,
                Is.EqualTo(PlayerController.DodgeDistance).Within(0.0001f));
            Assert.That(
                Vector3.Distance(dodgeStart, player.transform.position),
                Is.EqualTo(PlayerController.DodgeDistance).Within(0.001f));

            player.ForceState(PlayerState.Idle);
            Assert.That(player.TryStartDodge(Vector3.forward), Is.False);
            player.TickDodgeState(PlayerController.DodgeCooldown);
            Assert.That(player.TryStartDodge(Vector3.forward), Is.True);
        }

        [Test]
        public void BlockReducesPhysicalAndElementalButNotTrueDamage()
        {
            CombatSystem damageSystem = new GameObject("[CombatSystem]")
                .AddComponent<CombatSystem>();
            CreatePlayerRig(
                damageSystem,
                out PlayerController player,
                out PlayerStats stats,
                out _);
            stats.ConfigureBaseStats(100f, 0f, 0f, 0f, 1.5f);

            var request = new DamageRequest
            {
                BaseDamage = 100f,
                Type = DamageType.Physical,
                Element = ElementType.None,
                Multiplier = 1f,
                CanCrit = false,
                SkillId = string.Empty
            };

            Assert.That(player.TryStartBlock(), Is.True);
            Assert.That(player.IsBlocking, Is.True);
            Assert.That(
                player.CurrentMoveSpeedMultiplier,
                Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(
                damageSystem.ComputeDamage(request, stats).Amount,
                Is.EqualTo(40f).Within(0.0001f));

            request.Type = DamageType.Fire;
            request.Element = ElementType.Fire;
            Assert.That(
                damageSystem.ComputeDamage(request, stats).Amount,
                Is.EqualTo(70f).Within(0.0001f));

            request.Type = DamageType.True;
            request.Element = ElementType.None;
            Assert.That(
                damageSystem.ComputeDamage(request, stats).Amount,
                Is.EqualTo(100f).Within(0.0001f));

            request.Type = DamageType.Physical;
            damageSystem.DealDamage(stats, request);
            Assert.That(stats.CurrentHp, Is.EqualTo(60f).Within(0.0001f));
            Assert.That(player.State, Is.EqualTo(PlayerState.BlockHit));

            player.StopBlock();
            Assert.That(player.IsBlocking, Is.False);
            Assert.That(
                damageSystem.ComputeDamage(request, stats).Amount,
                Is.EqualTo(100f).Within(0.0001f));
        }

        [Test]
        public void AttackWindupCannotCancelButRecoveryCanCancelIntoDodge()
        {
            var service = new CapturingCombatService();
            CreatePlayerRig(
                service,
                out PlayerController player,
                out _,
                out PlayerCombatController combat);

            Assert.That(combat.TryStartLightAttack(), Is.True);
            Assert.That(
                combat.TryCancelRecoveryIntoDodge(Vector3.right),
                Is.False);
            Assert.That(player.State, Is.EqualTo(PlayerState.LightAttack));

            combat.TickAttack(combat.CurrentWindup);
            Assert.That(combat.IsInRecovery, Is.True);
            Assert.That(
                combat.TryCancelRecoveryIntoDodge(Vector3.right),
                Is.True);

            Assert.That(combat.IsAttacking, Is.False);
            Assert.That(combat.CurrentComboStep, Is.Zero);
            Assert.That(player.State, Is.EqualTo(PlayerState.Dodge));
            Assert.That(player.IsInvincible, Is.True);
        }

        private static void CreatePlayerRig(
            ICombatService combatService,
            out PlayerController player,
            out PlayerStats stats,
            out PlayerCombatController combat)
        {
            GameObject playerObject = new GameObject("[G01-01 Player]");
            player = playerObject.AddComponent<PlayerController>();
            stats = playerObject.AddComponent<PlayerStats>();
            combat = playerObject.AddComponent<PlayerCombatController>();

            var input = new FakePlayerInputSource();
            player.SetInputSource(input);
            combat.SetInputSource(input);
            combat.SetCombatService(combatService);
        }

        private static void AssertBinding(
            InputAction action,
            string effectivePath)
        {
            bool found = false;
            foreach (InputBinding binding in action.bindings)
            {
                if (binding.effectivePath == effectivePath)
                {
                    found = true;
                    break;
                }
            }

            Assert.That(
                found,
                Is.True,
                $"{action.name} must contain binding {effectivePath}.");
        }

        private static void DestroyRuntimeObjects()
        {
            DestroyAll<PlayerController>();
            DestroyAll<StatusEffectManager>();
            DestroyAll<CombatSystem>();
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
    }
}
