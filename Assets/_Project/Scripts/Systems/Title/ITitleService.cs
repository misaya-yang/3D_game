using System.Collections.Generic;
using Wendao.Data;

namespace Wendao.Systems.Title
{
    public interface ITitleService
    {
        string ActiveTitleId { get; }
        IReadOnlyList<string> UnlockedTitleIds { get; }
        float ActiveMaxHpPercent { get; }

        bool IsUnlocked(string titleId);
        bool Unlock(string titleId);
        bool Equip(string titleId);
        void Unequip();
        StatBlock GetActiveBonus();
        TitleData GetActiveTitle();
    }
}
