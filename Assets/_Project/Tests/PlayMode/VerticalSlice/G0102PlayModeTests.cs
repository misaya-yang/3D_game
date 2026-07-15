using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Wendao.CameraSystem;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.Enemy;
using Wendao.Entities.Player;
using Wendao.Systems.Combat;
using Wendao.Systems.Input;
using Wendao.Systems.NPC;
using Wendao.Systems.Player;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0102PlayModeTests
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
        public void LockOnInputSupportsMouseKeyboardAndGamepad()
        {
            InputActionAsset asset = Resources.Load<InputActionAsset>(
                PlayerInputReader.DefaultActionAssetResourcePath);
            Assert.That(asset, Is.Not.Null);
            InputAction lockOn = asset
                .FindActionMap(PlayerInputReader.PlayerActionMapName, true)
                .FindAction("LockOn", true);

            AssertBinding(lockOn, "<Mouse>/middleButton");
            AssertBinding(lockOn, "<Keyboard>/tab");
            AssertBinding(lockOn, "<Gamepad>/rightStickPress");
        }

        [Test]
        public void NearestTargetLocksAndRepeatedInputCyclesCandidates()
        {
            CreatePlayerRig(
                out PlayerController player,
                out _,
                out _,
                out PlayerTargetingController targeting,
                out FakePlayerInputSource input);
            ThirdPersonCamera camera = CreateCamera(player.transform, input);
            TestLockTarget nearest = CreateTarget(
                "Nearest",
                new Vector3(0f, 0f, 4f),
                14f);
            TestLockTarget farther = CreateTarget(
                "Farther",
                new Vector3(0f, 0f, 7f),
                14f);

            var observed = new List<LockOnInfo>();
            Action<LockOnInfo> handler = info => observed.Add(info);
            EventBus.Subscribe(PlayerEvents.LockOnChanged, handler);
            try
            {
                input.LockOnPressedThisFrame = true;
                targeting.TickTargeting(0.016f);
                input.LockOnPressedThisFrame = false;
                Assert.That(targeting.LastCandidateCount, Is.EqualTo(2));
                Assert.That(targeting.CurrentTarget, Is.SameAs(nearest));
                Assert.That(player.LockTarget, Is.EqualTo(nearest.transform));
                Assert.That(camera.LockOnTarget, Is.EqualTo(nearest.transform));

                input.LockOnPressedThisFrame = true;
                targeting.TickTargeting(0.016f);
                input.LockOnPressedThisFrame = false;
                Assert.That(targeting.CurrentTarget, Is.SameAs(farther));
                Assert.That(player.LockTarget, Is.EqualTo(farther.transform));
                Assert.That(camera.LockOnTarget, Is.EqualTo(farther.transform));

                input.LockOnPressedThisFrame = true;
                targeting.TickTargeting(0.016f);
                input.LockOnPressedThisFrame = false;
                Assert.That(targeting.CurrentTarget, Is.SameAs(nearest));
            }
            finally
            {
                EventBus.Unsubscribe(PlayerEvents.LockOnChanged, handler);
            }

            Assert.That(observed.Count, Is.EqualTo(3));
            Assert.That(observed[0].Locked, Is.True);
            Assert.That(observed[0].Target, Is.EqualTo(nearest.gameObject));
            Assert.That(observed[1].Target, Is.EqualTo(farther.gameObject));
        }

        [Test]
        public void DeadAndOutOfRangeTargetsClearLockOn()
        {
            CreatePlayerRig(
                out PlayerController player,
                out _,
                out _,
                out PlayerTargetingController targeting,
                out FakePlayerInputSource input);
            ThirdPersonCamera camera = CreateCamera(player.transform, input);
            TestLockTarget target = CreateTarget(
                "MortalTarget",
                new Vector3(0f, 0f, 3f),
                6f);

            Assert.That(targeting.TryCycleLockOn(), Is.True);
            target.Kill();
            targeting.TickTargeting(0.016f);
            Assert.That(targeting.CurrentTarget, Is.Null);
            Assert.That(player.LockTarget, Is.Null);
            Assert.That(camera.LockOnTarget, Is.Null);

            TestLockTarget distant = CreateTarget(
                "DistantTarget",
                new Vector3(0f, 0f, 5f),
                6f);
            Assert.That(targeting.TryCycleLockOn(), Is.True);
            Assert.That(targeting.CurrentTarget, Is.SameAs(distant));

            distant.transform.position = new Vector3(0f, 0f, 6.1f);
            targeting.TickTargeting(0.016f);
            Assert.That(targeting.CurrentTarget, Is.Null);
            Assert.That(player.LockTarget, Is.Null);
            Assert.That(camera.LockOnTarget, Is.Null);
        }

        [Test]
        public void LockedLightAttackTurnsPlayerTowardTarget()
        {
            CreatePlayerRig(
                out PlayerController player,
                out _,
                out PlayerCombatController combat,
                out PlayerTargetingController targeting,
                out _);
            CreateTarget("RightTarget", new Vector3(4f, 0f, 0f), 14f);

            Assert.That(targeting.TryCycleLockOn(), Is.True);
            Assert.That(combat.TryStartLightAttack(), Is.True);
            targeting.TickTargeting(0.25f);

            Assert.That(
                Vector3.Dot(player.transform.forward, Vector3.right),
                Is.GreaterThan(0.999f));
        }

        [Test]
        public void CameraAppliesCombatLockDialogueFovsAndDodgeShake()
        {
            CreatePlayerRig(
                out PlayerController player,
                out _,
                out _,
                out _,
                out FakePlayerInputSource input);
            ThirdPersonCamera camera = CreateCamera(player.transform, input);

            camera.TickCamera(1f);
            Assert.That(
                camera.CurrentFov,
                Is.EqualTo(ThirdPersonCamera.ExploreFov).Within(0.001f));

            camera.SetCombatMode(true);
            camera.TickCamera(1f);
            Assert.That(
                camera.CurrentFov,
                Is.EqualTo(ThirdPersonCamera.CombatFov).Within(0.001f));

            GameObject lockTarget = new GameObject("CameraLockTarget");
            lockTarget.transform.position = new Vector3(4f, 0f, 0f);
            camera.SetLockOnTarget(lockTarget.transform);
            camera.TickCamera(1f);
            Assert.That(
                camera.CurrentFov,
                Is.EqualTo(ThirdPersonCamera.LockOnFov).Within(0.001f));
            Assert.That(
                camera.CurrentDistance,
                Is.EqualTo(ThirdPersonCamera.CombatDistance).Within(0.001f));
            Assert.That(
                Vector3.Distance(
                    camera.CurrentPivot,
                    new Vector3(2f, 0.9f, 0f)),
                Is.LessThan(0.001f));

            TestDialogueFocusTarget focus = new GameObject("DialogueFocus")
                .AddComponent<TestDialogueFocusTarget>();
            focus.Configure("npc_g0102_focus");
            focus.transform.position = new Vector3(2f, 1.5f, 3f);
            EventBus.Publish(
                DialogueEvents.Started,
                new DialogueInfo
                {
                    NpcId = focus.NpcId,
                    DialogueId = "dlg_g0102_focus",
                    Cancelled = false
                });
            camera.TickCamera(1f);
            Assert.That(camera.DialogueFocus, Is.EqualTo(focus.transform));
            Assert.That(
                camera.CurrentFov,
                Is.EqualTo(ThirdPersonCamera.DialogueFov).Within(0.001f));

            EventBus.Publish(
                DialogueEvents.Ended,
                new DialogueInfo
                {
                    NpcId = focus.NpcId,
                    DialogueId = "dlg_g0102_focus",
                    Cancelled = false
                });
            Assert.That(camera.DialogueFocus, Is.Null);

            Assert.That(player.TryStartDodge(Vector3.forward), Is.True);
            Assert.That(
                camera.ShakeIntensity,
                Is.EqualTo(ThirdPersonCamera.DodgeShakeIntensity)
                    .Within(0.0001f));
            Assert.That(
                camera.ShakeRemaining,
                Is.EqualTo(ThirdPersonCamera.DodgeShakeDuration)
                    .Within(0.0001f));
            camera.TickCamera(0.05f);
            Assert.That(camera.ShakeRemaining, Is.EqualTo(0.05f).Within(0.001f));

            Object.Destroy(lockTarget);
        }

        [Test]
        public void CameraCollisionClampsDistanceAndRestoresOccluderFade()
        {
            CreatePlayerRig(
                out PlayerController player,
                out _,
                out _,
                out _,
                out FakePlayerInputSource input);
            ThirdPersonCamera camera = CreateCamera(player.transform, input);
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "G01-02 Camera Occluder";
            wall.transform.position = new Vector3(0f, 2.3f, -2.5f);
            wall.transform.localScale = new Vector3(4f, 4f, 0.5f);
            Renderer renderer = wall.GetComponent<Renderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Sprites/Default");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            renderer.sharedMaterial = material;

            Physics.SyncTransforms();
            camera.TickCamera(1f);
            Assert.That(
                camera.CurrentDistance,
                Is.InRange(
                    ThirdPersonCamera.MinimumCollisionDistance,
                    ThirdPersonCamera.ExploreDistance - 0.001f));
            Assert.That(camera.FadedOccluderCount, Is.GreaterThanOrEqualTo(1));

            wall.transform.position = new Vector3(100f, 100f, 100f);
            Physics.SyncTransforms();
            camera.TickCamera(1f);
            Assert.That(camera.FadedOccluderCount, Is.Zero);
            Assert.That(
                camera.CurrentDistance,
                Is.EqualTo(ThirdPersonCamera.ExploreDistance).Within(0.001f));

            Object.Destroy(wall);
            Object.Destroy(material);
        }

        private static void CreatePlayerRig(
            out PlayerController player,
            out PlayerStats stats,
            out PlayerCombatController combat,
            out PlayerTargetingController targeting,
            out FakePlayerInputSource input)
        {
            GameObject playerObject = new GameObject("[G01-02 Player]");
            player = playerObject.AddComponent<PlayerController>();
            stats = playerObject.AddComponent<PlayerStats>();
            combat = playerObject.AddComponent<PlayerCombatController>();
            targeting = playerObject.AddComponent<PlayerTargetingController>();

            input = new FakePlayerInputSource();
            var combatService = new CapturingCombatService();
            player.SetInputSource(input);
            combat.SetInputSource(input);
            combat.SetCombatService(combatService);
            targeting.SetInputSource(input);
        }

        private static ThirdPersonCamera CreateCamera(
            Transform target,
            IPlayerInputSource input)
        {
            var cameraObject = new GameObject("[G01-02 Camera]");
            cameraObject.AddComponent<UnityEngine.Camera>();
            ThirdPersonCamera camera = cameraObject.AddComponent<ThirdPersonCamera>();
            camera.SetTarget(target);
            camera.SetInputSource(input);
            return camera;
        }

        private static TestLockTarget CreateTarget(
            string name,
            Vector3 position,
            float disengageRange)
        {
            var targetObject = new GameObject(name);
            targetObject.transform.position = position;
            TestLockTarget target = targetObject.AddComponent<TestLockTarget>();
            target.Configure(disengageRange);
            return target;
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
            DestroyAll<ThirdPersonCamera>();
            DestroyAll<TestLockTarget>();
            DestroyAll<TestDialogueFocusTarget>();
            DestroyAll<EnemyBrain>();
            DestroyAll<TrainingDummy>();
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

    public sealed class TestLockTarget : MonoBehaviour, ILockOnTarget
    {
        private float _currentHp = 100f;

        public float CurrentHp => _currentHp;
        public float MaxHp => 100f;
        public bool IsDead => _currentHp <= 0f;
        public bool CanLockOn => isActiveAndEnabled && !IsDead;
        public Transform LockOnTransform => transform;
        public float LockOnDisengageRange { get; private set; }

        public void Configure(float disengageRange)
        {
            LockOnDisengageRange = Mathf.Max(0.1f, disengageRange);
            _currentHp = MaxHp;
        }

        public void Kill()
        {
            _currentHp = 0f;
        }

        public void ApplyDamage(DamageInfo info)
        {
            _currentHp = Mathf.Max(0f, _currentHp - info.Amount);
        }

        public void ApplyHeal(float amount, string sourceId)
        {
            _currentHp = Mathf.Min(MaxHp, _currentHp + amount);
        }
    }

    public sealed class TestDialogueFocusTarget : MonoBehaviour,
        IDialogueFocusTarget
    {
        public string NpcId { get; private set; } = string.Empty;
        public Transform DialogueFocusTransform => transform;

        public void Configure(string npcId)
        {
            NpcId = npcId ?? string.Empty;
        }
    }
}
