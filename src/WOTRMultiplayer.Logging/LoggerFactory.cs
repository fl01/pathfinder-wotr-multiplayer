using System.IO;
using Serilog;
using WOTRMultiplayer.Logging.Sinks;

namespace WOTRMultiplayer.Logging
{
    public static class LoggerFactory
    {
        private static TextWriter _debugConsole;
        private static object _consoleSinkRoot = new();

        public static ILogger Create(bool addConsoleSink)
        {
            var logConfig = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File("./Mods/WOTRMultiplayer/logs/wotr-multiplayer.log", rollingInterval: RollingInterval.Day)
                .Enrich.FromLogContext()
                ;

            if (addConsoleSink)
            {
                ConfigureConsoleSink(logConfig);
            }

            return logConfig.CreateLogger();
        }

        private static void ConfigureConsoleSink(LoggerConfiguration logConfig)
        {
            _debugConsole = WinApi.SpawnConsole();
            logConfig.WriteTo.DebugConsole(_debugConsole, syncRoot: _consoleSinkRoot);
        }
    }
}
