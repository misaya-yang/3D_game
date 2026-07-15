using Wendao.Data;

namespace Wendao.Systems.Player
{
    public interface IPlayerTitleStatsSink
    {
        void ApplyTitleBonus(StatBlock bonus, float maxHpPercent);
    }
}
