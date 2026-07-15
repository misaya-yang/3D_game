using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.Player;
using Wendao.Systems.Combat;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Mount;
using Wendao.Systems.Player;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0603PlayModeTests
    {
        private string _storageRoot;
        private SaveManager _save;
        private MountManager _mounts;
        private FakeCultivationService _cultivation;
        private FakePlayerMountLocomotion _locomotion;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            EventBus.Clear();
            DestroyAll<MountManager>();
            DestroyAll<SaveManager>();
            DestroyAll<GameManager>();
            yield return null;
            ServiceLocator.Clear();

            _storageRoot = Path.Combine(
                Path.GetTempPath(),
                "WendaoG0603Tests_" + Guid.NewGuid().ToString("N"));
            _save = new GameObject("[G0603Save]").AddComponent<SaveManager>();
            _save.ConfigureStorageRoot(_storageRoot);
            Assert.That(_save.SaveGame(0), Is.True, _save.LastError);

            _cultivation = new FakeCultivationService();
            _locomotion = new FakePlayerMountLocomotion();
            ServiceLocator.Register<ICultivationService>(_cultivation);
            ServiceLocator.Register<IPlayerMountLocomotion>(_locomotion);
            _mounts = new GameObject("[G0603Mounts]")
                .AddComponent<MountManager>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            EventBus.Clear();
            DestroyAll<MountManager>();
            DestroyAll<SaveManager>();
            DestroyAll<GameManager>();
            if (_locomotion?.Actor != null)
            {
                Object.Destroy(_locomotion.Actor);
            }

            yield return null;
            ServiceLocator.Clear();
            if (!string.IsNullOrEmpty(_storageRoot)
                && Directory.Exists(_storageRoot))
            {
                Directory.Delete(_storageRoot, true);
            }
        }

        [Test]
        public void SpiritHorseMountsAtOnePointFiveSpeedAndDismountResets()
        {
            Assert.That(_mounts.IsUnlocked(MountContentIds.SpiritHorse), Is.True);
            Assert.That(_mounts.TryMount(MountContentIds.SpiritHorse), Is.True);
            Assert.That(_mounts.IsMounted, Is.True);
            Assert.That(_mounts.IsFlying, Is.False);
            Assert.That(
                _locomotion.SpeedMultiplier,
                Is.EqualTo(MountManager.SpiritHorseSpeedMultiplier));
            Assert.That(
                _locomotion.Actor.transform.Find("MountVisual_SpiritHorse"),
                Is.Not.Null);

            var actualPlayerObject = new GameObject("G0603ActualPlayer");
            PlayerController actualPlayer =
                actualPlayerObject.AddComponent<PlayerController>();
            actualPlayerObject.AddComponent<PlayerStats>();
            PlayerCombatController actualCombat =
                actualPlayerObject.AddComponent<PlayerCombatController>();
            actualCombat.SetCombatService(new NoOpCombatService());
            actualPlayer.SetMountedState(
                true,
                MountManager.SpiritHorseSpeedMultiplier);
            Assert.That(
                actualPlayer.CurrentMoveSpeedMultiplier,
                Is.EqualTo(MountManager.SpiritHorseSpeedMultiplier));
            Assert.That(actualPlayer.TryStartBlock(), Is.False);
            Assert.That(actualPlayer.TryStartDodge(), Is.False);
            Assert.That(actualCombat.TryStartLightAttack(), Is.False);

            actualPlayer.SetMountedState(false, 1f);
            _mounts.Dismount();
            Assert.That(actualCombat.TryStartLightAttack(), Is.True);
            Assert.That(_mounts.IsMounted, Is.False);
            Assert.That(_locomotion.IsMounted, Is.False);
            Assert.That(_locomotion.SpeedMultiplier, Is.EqualTo(1f));
            Object.Destroy(actualPlayerObject);
        }

        [Test]
        public void FlyingSwordRequiresFoundationAndHonorsNoFlyZones()
        {
            Assert.That(_mounts.TryMount(MountContentIds.FlyingSword), Is.False);
            Assert.That(
                _mounts.IsUnlocked(MountContentIds.FlyingSword),
                Is.False);

            _cultivation.CurrentRealm = RealmType.Foundation;
            Assert.That(_mounts.RefreshRealmUnlocks(false), Is.True);
            Assert.That(
                _mounts.SelectedMountId,
                Is.EqualTo(MountContentIds.FlyingSword));
            Assert.That(_mounts.TryMount(MountContentIds.FlyingSword), Is.True);
            Assert.That(_mounts.TryTakeOff(), Is.True);
            Assert.That(_mounts.IsFlying, Is.True);
            Assert.That(_locomotion.IsFlying, Is.True);
            Assert.That(
                _locomotion.MaximumHeight,
                Is.EqualTo(MountManager.MaximumFlightHeight));

            _mounts.SetNoFlyZoneActive("boss_arena", true);
            Assert.That(_mounts.IsFlightAllowedInCurrentMap, Is.False);
            Assert.That(_mounts.IsFlying, Is.False);
            Assert.That(_mounts.TryTakeOff(), Is.False);

            _mounts.SetNoFlyZoneActive("boss_arena", false);
            Assert.That(_mounts.IsFlightAllowedInCurrentMap, Is.True);
            Assert.That(_mounts.TryTakeOff(), Is.True);
            _mounts.Land();
            Assert.That(_locomotion.IsFlying, Is.False);
        }

        [UnityTest]
        public IEnumerator MountsJsonRoundTripPreservesUnlocksAndSelection()
        {
            _cultivation.CurrentRealm = RealmType.Foundation;
            _mounts.RefreshRealmUnlocks(false);
            Assert.That(_mounts.TryMount(MountContentIds.FlyingSword), Is.True);
            _mounts.Dismount();
            Assert.That(_save.SaveGame(0), Is.True, _save.LastError);

            string mountsPath = Path.Combine(
                _storageRoot,
                "SaveSlot_0",
                MountManager.SaveModuleName + ".json");
            Assert.That(File.Exists(mountsPath), Is.True);
            string json = File.ReadAllText(mountsPath);
            StringAssert.Contains(MountContentIds.SpiritHorse, json);
            StringAssert.Contains(MountContentIds.FlyingSword, json);

            Object.Destroy(_mounts.gameObject);
            Object.Destroy(_save.gameObject);
            Object.Destroy(_locomotion.Actor);
            yield return null;
            ServiceLocator.Clear();

            _save = new GameObject("[G0603ReloadedSave]")
                .AddComponent<SaveManager>();
            _save.ConfigureStorageRoot(_storageRoot);
            _cultivation = new FakeCultivationService
            {
                CurrentRealm = RealmType.Foundation
            };
            _locomotion = new FakePlayerMountLocomotion();
            ServiceLocator.Register<ICultivationService>(_cultivation);
            ServiceLocator.Register<IPlayerMountLocomotion>(_locomotion);
            _mounts = new GameObject("[G0603ReloadedMounts]")
                .AddComponent<MountManager>();
            Assert.That(_save.LoadGame(0), Is.True, _save.LastError);
            yield return null;

            Assert.That(
                _mounts.IsUnlocked(MountContentIds.FlyingSword),
                Is.True);
            Assert.That(
                _mounts.SelectedMountId,
                Is.EqualTo(MountContentIds.FlyingSword));
            Assert.That(_mounts.IsMounted, Is.False);
            Assert.That(_mounts.IsFlying, Is.False);
        }

        [Test]
        public void InvalidMountSaveDataIsRejected()
        {
            var invalid = new MountSaveData
            {
                SchemaVersion = SaveSchema.CurrentVersion,
                UnlockedMountIds = new List<string> { "mount_unknown" },
                SelectedMountId = "mount_unknown"
            };

            Assert.Throws<InvalidDataException>(
                () => _mounts.RestoreSaveData(invalid));
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

        private sealed class FakePlayerMountLocomotion : IPlayerMountLocomotion
        {
            public FakePlayerMountLocomotion()
            {
                Actor = new GameObject("G0603Player");
            }

            public GameObject Actor { get; }
            public bool IsGrounded { get; set; } = true;
            public bool CanChangeMountState { get; set; } = true;
            public bool IsMounted { get; private set; }
            public bool IsFlying { get; private set; }
            public float SpeedMultiplier { get; private set; } = 1f;
            public float MaximumHeight { get; private set; }

            public void SetMountedState(bool mounted, float speedMultiplier)
            {
                IsMounted = mounted;
                SpeedMultiplier = mounted ? speedMultiplier : 1f;
                if (!mounted)
                {
                    IsFlying = false;
                }
            }

            public bool SetFlyingState(bool flying, float maximumHeight)
            {
                if (flying && (!IsMounted || !IsGrounded))
                {
                    return false;
                }

                IsFlying = flying;
                MaximumHeight = maximumHeight;
                return true;
            }
        }

        private sealed class NoOpCombatService : ICombatService
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
                return false;
            }

            public void RegisterActor(IDamageable actor)
            {
            }

            public void UnregisterActor(IDamageable actor)
            {
            }
        }

        private sealed class FakeCultivationService : ICultivationService
        {
            public RealmType CurrentRealm { get; set; } =
                RealmType.QiCondensation;
            public RealmType Realm => CurrentRealm;
            public int SubStage => 1;
            public float CurrentXp => 0f;
            public float XpToNext => 100f;
            public BreakthroughState CurrentBreakthroughState =>
                BreakthroughState.Idle;
            public bool IsBreakingThrough => false;
            public bool IsBreakthroughActive => false;
            public bool IsBreakthroughInvincible => false;
            public int CeremonyBeat => -1;

            public void AddXp(float amount, XpSourceType source)
            {
            }

            public float ApplyDeathXpPenalty(float percent)
            {
                return 0f;
            }

            public bool CanLevelSubStage()
            {
                return false;
            }

            public bool TryAdvanceSubStage()
            {
                return false;
            }

            public bool CanBreakthrough()
            {
                return false;
            }

            public IReadOnlyList<BreakthroughBlocker> GetBreakthroughBlockers()
            {
                return Array.Empty<BreakthroughBlocker>();
            }

            public float GetBreakthroughSuccessRate()
            {
                return 0f;
            }

            public bool TryBreakthrough()
            {
                return false;
            }
        }
    }
}
