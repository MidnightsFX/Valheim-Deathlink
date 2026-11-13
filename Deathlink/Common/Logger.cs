using BepInEx.Logging;
using Deathlink.Common;

namespace Deathlink
{
    internal class Logger
    {
        public static LogLevel Level = LogLevel.Info;

        // For hot paths: check this before building a debug message, which otherwise costs even when nothing is logged.
        public static bool IsDebug => Level >= LogLevel.Debug;

        public static void UpdateLogLevel()
        {
            Level = ValConfig.EnableDebugMode.Value ? LogLevel.Debug : LogLevel.Info;
        }

        public static void LogDebug(string message)
        {
            if (Level >= LogLevel.Debug)
            {
                Deathlink.Log.LogInfo(message);
            }
        }
        public static void LogInfo(string message)
        {
            if (Level >= LogLevel.Info)
            {
                Deathlink.Log.LogInfo(message);
            }
        }

        public static void LogWarning(string message)
        {
            if (Level >= LogLevel.Warning)
            {
                Deathlink.Log.LogWarning(message);
            }
        }

        public static void LogError(string message)
        {
            if (Level >= LogLevel.Error)
            {
                Deathlink.Log.LogError(message);
            }
        }
    }
}
