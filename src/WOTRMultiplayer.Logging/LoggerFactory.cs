using System.IO;
using Serilog;
using WOTRMultiplayer.Logging.Enrichers;
using WOTRMultiplayer.Logging.Sinks;

namespace WOTRMultiplayer.Logging
{
    public static class LoggerFactory
    {
        private static TextWriter _debugConsole;
        private readonly static object _consoleSinkRoot = new();

        public static ILogger Create(bool addConsoleSink, Serilog.Events.LogEventLevel minimumLevel)
        {
            var template = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] {SourceContext}: {Message:lj}{NewLine}{Exception}";
            var logConfig = new LoggerConfiguration();

            if (addConsoleSink)
            {
                ConfigureConsoleSink(logConfig, template);
            }

            logConfig
                .WriteTo.Console(outputTemplate: template, restrictedToMinimumLevel: minimumLevel)
                .WriteTo.File("./Mods/WOTRMultiplayer/logs/wotr-multiplayer.log", outputTemplate: template, rollingInterval: RollingInterval.Day)
                .Enrich.FromLogContext()
                .Enrich.With(new ClassNameEnricher())
                ;

            return logConfig.CreateLogger();
        }

        private static void ConfigureConsoleSink(LoggerConfiguration logConfig, string template)
        {
            _debugConsole = WinApi.SpawnConsole();
            logConfig.WriteTo.DebugConsole(_debugConsole, outputTemplate: template, syncRoot: _consoleSinkRoot);
        }
    }
}
