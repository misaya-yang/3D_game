#if DEVELOPMENT_BUILD || UNITY_EDITOR
namespace Wendao.Systems.Debugging
{
    public interface IDebugConsoleService
    {
        DebugCommandResult Execute(string commandLine);
    }
}
#endif
