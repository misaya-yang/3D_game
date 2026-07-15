using System.Collections.Generic;

namespace Wendao.Core
{
    public sealed class GameManager : Singleton<GameManager>
    {
        public const string GameStateChangedEvent = "OnGameStateChanged";

        private static readonly IReadOnlyDictionary<GameState, HashSet<GameState>> AllowedTransitions =
            new Dictionary<GameState, HashSet<GameState>>
            {
                [GameState.Boot] = new HashSet<GameState>
                {
                    GameState.MainMenu
                },
                [GameState.MainMenu] = new HashSet<GameState>
                {
                    GameState.Loading
                },
                [GameState.Loading] = new HashSet<GameState>
                {
                    GameState.Playing,
                    GameState.MainMenu
                },
                [GameState.Playing] = new HashSet<GameState>
                {
                    GameState.Loading,
                    GameState.Paused,
                    GameState.Dialogue,
                    GameState.Cutscene,
                    GameState.Dead,
                    GameState.MainMenu
                },
                [GameState.Paused] = new HashSet<GameState>
                {
                    GameState.Playing,
                    GameState.MainMenu
                },
                [GameState.Dialogue] = new HashSet<GameState>
                {
                    GameState.Playing,
                    GameState.MainMenu
                },
                [GameState.Cutscene] = new HashSet<GameState>
                {
                    GameState.Playing,
                    GameState.MainMenu
                },
                [GameState.Dead] = new HashSet<GameState>
                {
                    GameState.Playing,
                    GameState.MainMenu
                }
            };

        public GameState State { get; private set; }

        public bool IsInCombat { get; private set; }

        public bool TrySetState(GameState next)
        {
            if (next == State
                || !AllowedTransitions.TryGetValue(State, out var allowedTargets)
                || !allowedTargets.Contains(next))
            {
                return false;
            }

            var previous = State;
            State = next;
            EventBus.Publish(
                GameStateChangedEvent,
                new GameStateInfo(previous, next));
            return true;
        }

        public void SetCombatFlag(bool inCombat)
        {
            IsInCombat = inCombat;
        }

        protected override void OnSingletonAwake()
        {
            State = GameState.Boot;
            IsInCombat = false;
        }
    }
}
