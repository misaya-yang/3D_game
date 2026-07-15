namespace Wendao.Systems.Combat
{
    /// <summary>
    /// Actor-local hit reaction hook. This is intentionally not a poise system.
    /// </summary>
    public interface IHitstunReceiver
    {
        float HitstunMultiplier { get; }
        float HitstunRemaining { get; }

        void ApplyHitstun(float seconds);
    }
}
