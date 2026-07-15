namespace Wendao.Systems.Combat
{
    public interface ICombatFeelService
    {
        bool IsHitstopActive { get; }
        float HitstopRemaining { get; }

        void PlayHitstop(float seconds);
        void CancelHitstop();
    }
}
