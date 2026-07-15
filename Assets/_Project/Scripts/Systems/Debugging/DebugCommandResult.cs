#if DEVELOPMENT_BUILD || UNITY_EDITOR
namespace Wendao.Systems.Debugging
{
    public readonly struct DebugCommandResult
    {
        public DebugCommandResult(
            bool succeeded,
            string localizationKey,
            string defaultValue)
        {
            Succeeded = succeeded;
            LocalizationKey = localizationKey ?? string.Empty;
            DefaultValue = defaultValue ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string LocalizationKey { get; }
        public string DefaultValue { get; }

        public static DebugCommandResult Success(
            string localizationKey,
            string defaultValue)
        {
            return new DebugCommandResult(true, localizationKey, defaultValue);
        }

        public static DebugCommandResult Failure(
            string localizationKey,
            string defaultValue)
        {
            return new DebugCommandResult(false, localizationKey, defaultValue);
        }
    }
}
#endif
