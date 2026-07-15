using System;

namespace Wendao.Core
{
    [Serializable]
    public struct GameStateInfo
    {
        public GameStateInfo(GameState prev, GameState next)
        {
            Prev = prev;
            Next = next;
        }

        public GameState Prev;
        public GameState Next;
    }
}

