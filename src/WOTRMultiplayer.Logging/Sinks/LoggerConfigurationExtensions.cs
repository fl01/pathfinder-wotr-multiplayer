using System;
using System.IO;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Sinks.SystemConsole.Themes;

namespace WOTRMultiplayer.Logging.Sinks
{
    public static class LoggerConfigurationExtensions
    {
        public static LoggerConfiguration DebugConsole(
            this LoggerSinkConfiguration sinkConfiguration,
            TextWriter output,
            object syncRoot,
            LogEventLevel restrictedToMinimumLevel = LogEventLevel.Verbose,
            string outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
            IFormatProvider formatProvider = null,
            LoggingLevelSwitch levelSwitch = null,
            LogEventLevel? standardErrorFromLevel = null,
            ConsoleTheme theme = null,
            bool applyThemeToRedirectedOutput = false)
        {
            var consoleTheme = SystemConsoleTheme.Colored;
            var args = new object[] { consoleTheme, outputTemplate, formatProvider };
            // creating internal type
            var type = typeof(ConsoleLoggerConfigurationExtensions).Assembly.GetType("Serilog.Sinks.SystemConsole.Output.OutputTemplateRenderer");
            var formatter = Activator.CreateInstance(type, args) as ITextFormatter;
            return sinkConfiguration.Sink(new DebugConsoleSink(output, consoleTheme, formatter, standardErrorFromLevel, syncRoot), restrictedToMinimumLevel, levelSwitch);
        }
    }
}
