using System.Collections.Generic;

namespace Wendao.Systems.Mount
{
    public interface IMountService
    {
        bool IsMounted { get; }
        bool IsFlying { get; }
        string ActiveMountId { get; }
        string SelectedMountId { get; }
        bool IsFlightAllowedInCurrentMap { get; }
        IReadOnlyList<string> UnlockedMountIds { get; }

        bool IsUnlocked(string mountId);
        bool TryMount(string mountId);
        void Dismount();
        bool TryTakeOff();
        void Land();
        void SetNoFlyZoneActive(string zoneId, bool active);
    }
}
