using System;
using System.IO;
using System.Text;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Sinks.SystemConsole.Themes;

namespace WOTRMultiplayer.Logging.Sinks
{
    public class DebugConsoleSink : ILogEventSink
    {
        private readonly TextWriter _output;
        private readonly LogEventLevel? _standardErrorFromLevel;
        private readonly ConsoleTheme _theme;
        private readonly ITextFormatter _formatter;
        private readonly object _syncRoot;
        private const int DefaultWriteBufferCapacity = 256;

        public DebugConsoleSink(TextWriter output, ConsoleTheme theme, ITextFormatter formatter, LogEventLevel? standardErrorFromLevel, object syncRoot)
        {
            _output = output;
            _standardErrorFromLevel = standardErrorFromLevel;
            _theme = theme ?? throw new ArgumentNullException("theme");
            _formatter = formatter;
            _syncRoot = syncRoot ?? throw new ArgumentNullException("syncRoot");
        }

        public void Emit(LogEvent logEvent)
        {
            if (_theme.CanBuffer)
            {
                StringWriter stringWriter = new StringWriter(new StringBuilder(256));
                _formatter.Format(logEvent, stringWriter);
                string value = stringWriter.ToString();
                lock (_syncRoot)
                {
                    _output.Write(value);
                    _output.Flush();
                    return;
                }
            }

            lock (_syncRoot)
            {
                _formatter.Format(logEvent, _output);
                _output.Flush();
            }
        }
    }
}
