using System;

namespace Wendao.Systems.World
{
    public static class BlackwindDungeonEvents
    {
        public const string RunStarted = "OnBlackwindRunStarted";
        public const string FloorEntered = "OnBlackwindFloorEntered";
        public const string FloorCompleted = "OnBlackwindFloorCompleted";
        public const string RunReset = "OnBlackwindRunReset";
        public const string RunCompleted = "OnBlackwindRunCompleted";
    }

    [Serializable]
    public struct BlackwindFloorInfo
    {
        public int Floor;
        public int Checkpoint;
    }

    [Serializable]
    public struct BlackwindRunInfo
    {
        public int StartFloor;
        public int Checkpoint;
        public bool WasFailure;
    }
}
