namespace Wendao.Systems.Inventory
{
    public interface IPlayerHealthService
    {
        float CurrentHp { get; }
        float MaxHp { get; }
        bool IsDead { get; }

        void ApplyHeal(float amount, string sourceId);
    }
}
