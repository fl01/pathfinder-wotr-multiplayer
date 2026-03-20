using System;
using System.Collections.Generic;
using Serilog;
using WOTRMultiplayer.Logging.Enrichers;
using WOTRMultiplayer.Logging.Object;
using WOTRMultiplayer.Logging.Sinks;

namespace WOTRMultiplayer.Logging
{
    public static class LoggerFactory
    {
        private readonly static object _consoleSinkRoot = new();

        public static ILogger Create(bool addDebugConsoleSink, string baseFolder, Serilog.Events.LogEventLevel consoleMinLevel, Serilog.Events.LogEventLevel fileMinLevel, IEnumerable<Type> loggableObjects)
        {
            ObjectLoggingMetadata.Initialize(loggableObjects);

            var template = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] {SourceContext}: {Message:lj}{NewLine}{Exception}";
            var logConfig = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.With(new ClassNameEnricher());

            if (addDebugConsoleSink)
            {
                ConfigureConsoleSink(logConfig, template, consoleMinLevel);
            }
            else
            {
                logConfig.WriteTo.Console(outputTemplate: template, restrictedToMinimumLevel: consoleMinLevel);
            }

            logConfig.WriteTo.File($"{baseFolder}/logs/wotr-multiplayer-.log", restrictedToMinimumLevel: fileMinLevel, outputTemplate: template, rollingInterval: RollingInterval.Day);

            return logConfig.CreateLogger();
        }

        private static void ConfigureConsoleSink(LoggerConfiguration logConfig, string template, Serilog.Events.LogEventLevel restrictedToMinimumLevel)
        {
            var debugConsole = WinApi.SpawnConsole();
            logConfig.WriteTo.DebugConsole(debugConsole, outputTemplate: template, syncRoot: _consoleSinkRoot, restrictedToMinimumLevel: restrictedToMinimumLevel);
        }
    }
}
