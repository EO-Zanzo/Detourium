using System;
using System.Collections.Generic;

namespace Detourium.Runtime.Logging
{
    public class ConsoleLoggerLevelConfig
    {
        public bool ShowException { get; set; }
        public bool ShowStackTrace { get; set; }
        public ConsoleColor? ForegroundColor { get; set; }
        public ConsoleColor? BackgroundColor { get; set; }

        public ConsoleLoggerLevelConfig()
        {
            ShowException = false;
            ShowStackTrace = false;
            ForegroundColor = null;
            BackgroundColor = null;
        }
    }

    public class ConsoleLogger : Logger
    {
        public string Format { get; set; }

        public Dictionary<LogLevel, ConsoleLoggerLevelConfig> LevelConfig { get; private set; }

        public ConsoleLogger()
        {
            this.LevelConfig = new Dictionary<LogLevel, ConsoleLoggerLevelConfig>();

            foreach (LogLevel level in Enum.GetValues(typeof(LogLevel))) {
                this.LevelConfig[level] = new ConsoleLoggerLevelConfig();
            }

            this.LevelConfig[LogLevel.Warning].ForegroundColor = ConsoleColor.Yellow;
            this.LevelConfig[LogLevel.Warning].BackgroundColor = ConsoleColor.Black;

            this.LevelConfig[LogLevel.Debug].ForegroundColor = ConsoleColor.White;
            this.LevelConfig[LogLevel.Debug].BackgroundColor = ConsoleColor.Black;

            this.LevelConfig[LogLevel.Info].ForegroundColor = ConsoleColor.White;
            this.LevelConfig[LogLevel.Info].BackgroundColor = ConsoleColor.Black;

            this.LevelConfig[LogLevel.Error].ShowException = true;
            this.LevelConfig[LogLevel.Error].ShowStackTrace = true;
            this.LevelConfig[LogLevel.Error].ForegroundColor = ConsoleColor.Red;
            this.LevelConfig[LogLevel.Error].BackgroundColor = ConsoleColor.Black;

            this.LevelConfig[LogLevel.Fatal].ShowException = true;
            this.LevelConfig[LogLevel.Fatal].ShowStackTrace = true;
            this.LevelConfig[LogLevel.Fatal].ForegroundColor = ConsoleColor.DarkRed;
            this.LevelConfig[LogLevel.Fatal].BackgroundColor = ConsoleColor.Black;
        }

        public void Log(string message)
        {
            Console.WriteLine(message);
        }

        public override void Log(LogLevel logLevel, string message, Exception ex)
        {
            var levelConfig = this.LevelConfig[logLevel];

            if (levelConfig.ForegroundColor.HasValue)
                Console.ForegroundColor = levelConfig.ForegroundColor.Value;

            if (levelConfig.BackgroundColor.HasValue)
                Console.BackgroundColor = levelConfig.BackgroundColor.Value;

            var abbrev =
                    logLevel == LogLevel.Debug      ? "DBG" :
                    logLevel == LogLevel.Warning    ? "WRN" :
                    logLevel == LogLevel.Error      ? "ERR" :
                    logLevel == LogLevel.Info       ? "   " :
                    logLevel == LogLevel.Fatal      ? "ERR" : "   ";

            Console.WriteLine("[{0}] {1}", abbrev, message);

            if (ex != null && levelConfig.ShowException) {
                if (levelConfig.ShowStackTrace)
                    Console.WriteLine(ex);
                else
                    Console.WriteLine("{0}: {1}", ex.GetType().Name, ex.Message);
            }

            Console.ResetColor();
        }
    }
}
