using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.Player;
using Wendao.Systems.Combat;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Player;
using Wendao.Systems.World;
using Wendao.UI.Combat;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0104PlayModeTests
    {
        private readonly List<GameObject> _ownedObjects = new List<GameObject>();

        private string _storageRoot;
        private GameManager _gameManager;
        private SaveManager _saveManager;
        private CultivationManager _cultivation;
        private CombatSystem _combat;
        private PlayerController _player;
        private PlayerStats _stats;
        private DeathView _deathView;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            EventBus.Clear();
            DestroyRuntimeObjects();
            yield return null;
            ServiceLocator.Clear();

            _storageRoot = Path.Combine(
                Path.GetTempPath(),
                "WendaoG0104Tests_" + Guid.NewGuid().ToString("N"));
            _gameManager = Own("[G01-04 GameManager]")
                .AddComponent<GameManager>();
            EnterPlayingState(_gameManager);

            _saveManager = Own("[G01-04 SaveManager]")
                .AddComponent<SaveManager>();
            _saveManager.ConfigureStorageRoot(_storageRoot);
            Assert.That(_saveManager.SaveGame(0), Is.True);

            Own("[G01-04 ConfigDatabase]").AddComponent<ConfigDatabase>();
            _cultivation = Own("[G01-04 CultivationManager]")
                .AddComponent<CultivationManager>();
            _combat = Own("[G01-04 CombatSystem]")
                .AddComponent<CombatSystem>();

            CreateRespawnPoint(
                "respawn-far",
                "teleport_test_far",
                new Vector3(30f, 0f, 0f));
            CreateRespawnPoint(
                "respawn-near",
                "teleport_test_near",
                new Vector3(12f, 0f, 0f));

            GameObject playerObject = Own("G01-04 Player");
            playerObject.transform.position = new Vector3(10f, 0f, 0f);
            playerObject.AddComponent<CharacterController>();
            _player = playerObject.AddComponent<PlayerController>();
            _stats = playerObject.AddComponent<PlayerStats>();
            _stats.ConfigureBaseStats(100f, 0f, 0f, 0f, 1.5f);
            _stats.ConfigureMana(50f);

            _deathView = Own("G01-04 DeathView").AddComponent<DeathView>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            EventBus.Clear();
            for (int index = 0; index < _ownedObjects.Count; index++)
            {
                if (_ownedObjects[index] != null)
                {
                    Object.Destroy(_ownedObjects[index]);
                }
            }

            _ownedObjects.Clear();
            DestroyAll<EventSystem>();
            yield return null;
            ServiceLocator.Clear();

            if (!string.IsNullOrEmpty(_storageRoot)
                && Directory.Exists(_storageRoot))
            {
                Directory.Delete(_storageRoot, true);
            }
        }

        [Test]
        public void DeathPenalizesOnceShowsUiPersistsAndRespawnsAtNearestPoint()
        {
            _saveManager.Profile.CultivationXp = 200f;
            Assert.That(_saveManager.TrySaveModule("profile"), Is.True);

            var penalties = new List<CultivationXpPenaltyInfo>();
            var respawns = new List<PlayerRespawnInfo>();
            Action<CultivationXpPenaltyInfo> penaltyHandler =
                info => penalties.Add(info);
            Action<PlayerRespawnInfo> respawnHandler = info => respawns.Add(info);
            EventBus.Subscribe(
                CultivationEvents.DeathXpPenaltyApplied,
                penaltyHandler);
            EventBus.Subscribe(PlayerEvents.Respawned, respawnHandler);
            try
            {
                _combat.DealDamage(
                    _stats,
                    new DamageRequest
                    {
                        Source = Own("Lethal Enemy"),
                        BaseDamage = 1000f,
                        Multiplier = 1f,
                        Type = DamageType.True,
                        Element = ElementType.None,
                        CanCrit = false,
                        SkillId = "test_lethal"
                    });

                Assert.That(_stats.IsDead, Is.True);
                Assert.That(_player.State, Is.EqualTo(PlayerState.Dead));
                Assert.That(_gameManager.State, Is.EqualTo(GameState.Dead));
                Assert.That(_gameManager.IsInCombat, Is.False);
                Assert.That(_deathView.IsOpen, Is.True);
                Assert.That(_deathView.RespawnButtonInteractable, Is.True);
                Assert.That(
                    _deathView.CurrentMessageLocalizationKey,
                    Is.EqualTo(DeathView.MessageLocalizationKey));
                Assert.That(_cultivation.CurrentXp, Is.EqualTo(190f).Within(0.001f));
                Assert.That(penalties, Has.Count.EqualTo(1));
                Assert.That(penalties[0].AmountLost, Is.EqualTo(10f).Within(0.001f));
                Assert.That(
                    penalties[0].Percent,
                    Is.EqualTo(FormulaLibrary.DeathXpPenaltyPercent));

                _stats.HandleDeath(default);
                Assert.That(_cultivation.CurrentXp, Is.EqualTo(190f).Within(0.001f));
                Assert.That(penalties, Has.Count.EqualTo(1));

                IPlayerRespawnService respawnService =
                    ServiceLocator.Get<IPlayerRespawnService>();
                Assert.That(
                    respawnService.NearestRespawnPointId,
                    Is.EqualTo("teleport_test_near"));
                Assert.That(_deathView.TryRespawn(), Is.True);

                Assert.That(_stats.IsDead, Is.False);
                Assert.That(_stats.CurrentHp, Is.EqualTo(_stats.MaxHp));
                Assert.That(_stats.CurrentMana, Is.EqualTo(_stats.MaxMana));
                Assert.That(_player.State, Is.EqualTo(PlayerState.Idle));
                Assert.That(_gameManager.State, Is.EqualTo(GameState.Playing));
                Assert.That(_deathView.IsOpen, Is.False);
                Assert.That(
                    Vector3.Distance(
                        _player.transform.position,
                        new Vector3(12f, 0f, 0f)),
                    Is.LessThan(0.001f));
                Assert.That(respawns, Has.Count.EqualTo(1));
                Assert.That(
                    respawns[0].RespawnPointId,
                    Is.EqualTo("teleport_test_near"));

                _saveManager.Profile.CultivationXp = 1f;
                Assert.That(_saveManager.LoadGame(0), Is.True);
                Assert.That(
                    _saveManager.Profile.CultivationXp,
                    Is.EqualTo(190f).Within(0.001f));
            }
            finally
            {
                EventBus.Unsubscribe(
                    CultivationEvents.DeathXpPenaltyApplied,
                    penaltyHandler);
                EventBus.Unsubscribe(PlayerEvents.Respawned, respawnHandler);
            }
        }

        [Test]
        public void RecoveryStartsOnlyAfterFiveSecondsAtTwoAndThreePercentPerSecond()
        {
            _stats.SetMana(0f);
            _stats.ApplyDamage(
                new DamageInfo
                {
                    Source = Own("Recovery Enemy"),
                    Amount = 20f,
                    Type = DamageType.Physical
                });

            Assert.That(_stats.CurrentHp, Is.EqualTo(80f));
            Assert.That(_gameManager.IsInCombat, Is.True);

            _stats.TickRecovery(4.999f);
            Assert.That(_gameManager.IsInCombat, Is.True);
            Assert.That(_stats.CurrentHp, Is.EqualTo(80f));
            Assert.That(_stats.CurrentMana, Is.Zero);

            _stats.TickRecovery(0.001f);
            Assert.That(_gameManager.IsInCombat, Is.False);
            Assert.That(_stats.CurrentHp, Is.EqualTo(80f));
            Assert.That(_stats.CurrentMana, Is.Zero);

            HealInfo observedHeal = default;
            Action<HealInfo> healHandler = info => observedHeal = info;
            EventBus.Subscribe(CombatEvents.PlayerHealed, healHandler);
            try
            {
                _stats.TickRecovery(1f);
            }
            finally
            {
                EventBus.Unsubscribe(CombatEvents.PlayerHealed, healHandler);
            }

            Assert.That(_stats.CurrentHp, Is.EqualTo(82f).Within(0.001f));
            Assert.That(_stats.CurrentMana, Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(
                observedHeal.SourceId,
                Is.EqualTo(PlayerRecoveryContentIds.OutOfCombatRegeneration));
        }

        [Test]
        public void DamageDealtResetsClockAndNonPlayingStatesDoNotRecover()
        {
            _stats.SetHp(50f);
            _stats.SetMana(0f);
            EventBus.Publish(
                CombatEvents.DamageApplied,
                new DamageInfo
                {
                    Source = _stats.gameObject,
                    Target = Own("Damage Target"),
                    Amount = 10f
                });

            Assert.That(_gameManager.IsInCombat, Is.True);
            Assert.That(_stats.OutOfCombatElapsed, Is.Zero);
            Assert.That(_gameManager.TrySetState(GameState.Paused), Is.True);
            _stats.TickRecovery(10f);
            Assert.That(_stats.CurrentHp, Is.EqualTo(50f));
            Assert.That(_stats.CurrentMana, Is.Zero);
            Assert.That(_gameManager.IsInCombat, Is.True);

            Assert.That(_gameManager.TrySetState(GameState.Playing), Is.True);
            _stats.TickRecovery(5f);
            Assert.That(_gameManager.IsInCombat, Is.False);
            Assert.That(_stats.CurrentHp, Is.EqualTo(50f));
            Assert.That(_stats.CurrentMana, Is.Zero);
        }

        private GameObject Own(string name)
        {
            var value = new GameObject(name);
            _ownedObjects.Add(value);
            return value;
        }

        private RespawnPoint CreateRespawnPoint(
            string name,
            string id,
            Vector3 position)
        {
            GameObject value = Own(name);
            value.transform.position = position;
            RespawnPoint point = value.AddComponent<RespawnPoint>();
            point.Configure(id);
            return point;
        }

        private static void EnterPlayingState(GameManager gameManager)
        {
            Assert.That(gameManager.TrySetState(GameState.MainMenu), Is.True);
            Assert.That(gameManager.TrySetState(GameState.Loading), Is.True);
            Assert.That(gameManager.TrySetState(GameState.Playing), Is.True);
        }

        private static void DestroyRuntimeObjects()
        {
            DestroyAll<DeathView>();
            DestroyAll<RespawnPoint>();
            DestroyAll<PlayerController>();
            DestroyAll<CombatSystem>();
            DestroyAll<CultivationManager>();
            DestroyAll<ConfigDatabase>();
            DestroyAll<SaveManager>();
            DestroyAll<GameManager>();
            DestroyAll<EventSystem>();
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
}
