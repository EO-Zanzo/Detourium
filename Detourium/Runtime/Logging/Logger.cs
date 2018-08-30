using System;

namespace Detourium.Runtime.Logging
{
    public enum LogLevel
    {
        Fatal = 0,
        Error = 1,
        Warning = 2,
        Info = 3,
        Debug = 4
    };

    public abstract class Logger
    {
        public LogLevel LogLevel { get; set; }

        public Logger()
        {
            LogLevel = LogLevel.Info;
        }

        public abstract void Log(LogLevel logLevel, string message, Exception ex = null);

        public void LogDebug(string message, Exception ex = null) => Log(LogLevel.Debug, message, ex);

        public void LogInfo(string message, Exception ex = null) => Log(LogLevel.Info, message, ex);

        public void LogWarning(string message, Exception ex = null) => Log(LogLevel.Warning, message, ex);

        public void LogError(string message, Exception ex = null) => Log(LogLevel.Error, message, ex);

        public void LogFatal(string message, Exception ex = null) => Log(LogLevel.Fatal, message, ex);
    }
}
