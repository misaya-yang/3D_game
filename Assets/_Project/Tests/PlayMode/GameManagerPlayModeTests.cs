using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wendao.Core;

namespace Wendao.Tests.PlayMode
{
    public sealed class GameManagerPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EventBus.Clear();
            DestroyAllGameManagers();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            EventBus.Clear();
            DestroyAllGameManagers();
            yield return null;
        }

        [Test]
        public void NewManagerStartsInBootOutsideCombat()
        {
            var manager = CreateManager();

            Assert.That(manager.State, Is.EqualTo(GameState.Boot));
            Assert.That(manager.IsInCombat, Is.False);
        }

        [Test]
        public void MainProgressionTransitionsAreLegal()
        {
            var manager = CreateManager();

            Assert.That(manager.TrySetState(GameState.MainMenu), Is.True);
            Assert.That(manager.TrySetState(GameState.Loading), Is.True);
            Assert.That(manager.TrySetState(GameState.Playing), Is.True);
            Assert.That(manager.State, Is.EqualTo(GameState.Playing));
        }

        [Test]
        public void PlayingSubstatesReturnToPlaying()
        {
            var manager = CreateManagerInPlayingState();

            Assert.That(manager.TrySetState(GameState.Dialogue), Is.True);
            Assert.That(manager.TrySetState(GameState.Playing), Is.True);
            Assert.That(manager.TrySetState(GameState.Cutscene), Is.True);
            Assert.That(manager.TrySetState(GameState.Playing), Is.True);
            Assert.That(manager.TrySetState(GameState.Paused), Is.True);
            Assert.That(manager.TrySetState(GameState.Playing), Is.True);
            Assert.That(manager.TrySetState(GameState.Dead), Is.True);
            Assert.That(manager.TrySetState(GameState.Playing), Is.True);
        }

        [Test]
        public void PlayingCanEnterLoadingForMapTravel()
        {
            var manager = CreateManagerInPlayingState();

            Assert.That(manager.TrySetState(GameState.Loading), Is.True);
            Assert.That(manager.TrySetState(GameState.Playing), Is.True);
        }

        [Test]
        public void ActiveStatesCanReturnToMainMenu()
        {
            var manager = CreateManager();

            Assert.That(manager.TrySetState(GameState.MainMenu), Is.True);
            Assert.That(manager.TrySetState(GameState.Loading), Is.True);
            Assert.That(manager.TrySetState(GameState.MainMenu), Is.True);

            Assert.That(manager.TrySetState(GameState.Loading), Is.True);
            Assert.That(manager.TrySetState(GameState.Playing), Is.True);
            Assert.That(manager.TrySetState(GameState.Paused), Is.True);
            Assert.That(manager.TrySetState(GameState.MainMenu), Is.True);

            Assert.That(manager.TrySetState(GameState.Loading), Is.True);
            Assert.That(manager.TrySetState(GameState.Playing), Is.True);
            Assert.That(manager.TrySetState(GameState.Dialogue), Is.True);
            Assert.That(manager.TrySetState(GameState.MainMenu), Is.True);

            Assert.That(manager.TrySetState(GameState.Loading), Is.True);
            Assert.That(manager.TrySetState(GameState.Playing), Is.True);
            Assert.That(manager.TrySetState(GameState.Cutscene), Is.True);
            Assert.That(manager.TrySetState(GameState.MainMenu), Is.True);

            Assert.That(manager.TrySetState(GameState.Loading), Is.True);
            Assert.That(manager.TrySetState(GameState.Playing), Is.True);
            Assert.That(manager.TrySetState(GameState.Dead), Is.True);
            Assert.That(manager.TrySetState(GameState.MainMenu), Is.True);
        }

        [Test]
        public void IllegalTransitionReturnsFalseWithoutChangingStateOrPublishing()
        {
            var manager = CreateManager();
            var eventCalls = 0;
            EventBus.Subscribe<GameStateInfo>(
                GameManager.GameStateChangedEvent,
                _ => eventCalls++);

            var changed = manager.TrySetState(GameState.Playing);

            Assert.That(changed, Is.False);
            Assert.That(manager.State, Is.EqualTo(GameState.Boot));
            Assert.That(eventCalls, Is.Zero);
        }

        [Test]
        public void SameAndUnknownStatesAreRejected()
        {
            var manager = CreateManager();

            Assert.That(manager.TrySetState(GameState.Boot), Is.False);
            Assert.That(manager.TrySetState((GameState)999), Is.False);
            Assert.That(manager.State, Is.EqualTo(GameState.Boot));
        }

        [Test]
        public void SuccessfulTransitionPublishesPreviousAndNextState()
        {
            var manager = CreateManager();
            var received = false;
            var observed = default(GameStateInfo);
            EventBus.Subscribe<GameStateInfo>(
                GameManager.GameStateChangedEvent,
                info =>
                {
                    received = true;
                    observed = info;
                });

            var changed = manager.TrySetState(GameState.MainMenu);

            Assert.That(changed, Is.True);
            Assert.That(received, Is.True);
            Assert.That(observed.Prev, Is.EqualTo(GameState.Boot));
            Assert.That(observed.Next, Is.EqualTo(GameState.MainMenu));
        }

        [Test]
        public void CombatFlagChangesWithoutChangingStateOrPublishingStateEvent()
        {
            var manager = CreateManagerInPlayingState();
            var stateBefore = manager.State;
            var eventCalls = 0;
            EventBus.Clear();
            EventBus.Subscribe<GameStateInfo>(
                GameManager.GameStateChangedEvent,
                _ => eventCalls++);

            manager.SetCombatFlag(true);

            Assert.That(manager.IsInCombat, Is.True);
            Assert.That(manager.State, Is.EqualTo(stateBefore));
            Assert.That(eventCalls, Is.Zero);

            manager.SetCombatFlag(false);

            Assert.That(manager.IsInCombat, Is.False);
            Assert.That(manager.State, Is.EqualTo(stateBefore));
            Assert.That(eventCalls, Is.Zero);
        }

        private static GameManager CreateManager()
        {
            return new GameObject("Game Manager Test").AddComponent<GameManager>();
        }

        private static GameManager CreateManagerInPlayingState()
        {
            var manager = CreateManager();
            Assert.That(manager.TrySetState(GameState.MainMenu), Is.True);
            Assert.That(manager.TrySetState(GameState.Loading), Is.True);
            Assert.That(manager.TrySetState(GameState.Playing), Is.True);
            return manager;
        }

        private static void DestroyAllGameManagers()
        {
            var managers = Object.FindObjectsByType<GameManager>(FindObjectsInactive.Include);

            foreach (var manager in managers)
            {
                if (manager != null)
                {
                    Object.Destroy(manager.gameObject);
                }
            }
        }
    }
}
