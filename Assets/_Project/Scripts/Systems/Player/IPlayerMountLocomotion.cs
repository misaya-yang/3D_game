using UnityEngine;

namespace Wendao.Systems.Player
{
    public interface IPlayerMountLocomotion
    {
        GameObject Actor { get; }
        bool IsGrounded { get; }
        bool CanChangeMountState { get; }

        void SetMountedState(bool mounted, float speedMultiplier);
        bool SetFlyingState(bool flying, float maximumHeight);
    }
}
