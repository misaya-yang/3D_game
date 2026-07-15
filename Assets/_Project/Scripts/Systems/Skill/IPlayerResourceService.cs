namespace Wendao.Systems.Skill
{
    public interface IPlayerResourceService
    {
        float CurrentMana { get; }
        float MaxMana { get; }

        void SetMana(float value);
        float ApplyManaDelta(float delta);
        bool TrySpendMana(float amount);
    }
}
