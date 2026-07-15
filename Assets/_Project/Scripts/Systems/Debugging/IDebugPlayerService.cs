namespace Wendao.Systems.Debugging
{
    public interface IDebugPlayerService
    {
        bool GodModeEnabled { get; }
        bool SetGodMode(bool enabled);
    }
}
