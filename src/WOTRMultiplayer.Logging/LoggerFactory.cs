using Serilog;
using WOTRMultiplayer.Logging.Enrichers;
using WOTRMultiplayer.Logging.Sinks;

namespace WOTRMultiplayer.Logging
{
    public static class LoggerFactory
    {
        private readonly static object _consoleSinkRoot = new();

        public static ILogger Create(bool addConsoleSink, string baseFolder, Serilog.Events.LogEventLevel consoleMinLevel, Serilog.Events.LogEventLevel fileMinLevel)
        {
            var template = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] {SourceContext}: {Message:lj}{NewLine}{Exception}";
            var logConfig = new LoggerConfiguration();

            if (addConsoleSink)
            {
                ConfigureConsoleSink(logConfig, template);
            }

            logConfig
                .WriteTo.Console(outputTemplate: template, restrictedToMinimumLevel: consoleMinLevel)
                .WriteTo.File($"{baseFolder}/logs/wotr-multiplayer.log", restrictedToMinimumLevel: fileMinLevel, outputTemplate: template, rollingInterval: RollingInterval.Day)
                .Enrich.FromLogContext()
                .Enrich.With(new ClassNameEnricher())
                ;

            return logConfig.CreateLogger();
        }

        private static void ConfigureConsoleSink(LoggerConfiguration logConfig, string template)
        {
            var debugConsole = WinApi.SpawnConsole();
            logConfig.WriteTo.DebugConsole(debugConsole, outputTemplate: template, syncRoot: _consoleSinkRoot);
        }
    }
}
