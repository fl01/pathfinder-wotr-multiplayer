using System.Linq;
using Serilog.Core;
using Serilog.Events;

namespace WOTRMultiplayer.Logging.Enrichers
{
    public class ClassNameEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            if (logEvent.Properties.TryGetValue("SourceContext", out var property))
            {
                var scalarValue = property as ScalarValue;
                var value = scalarValue?.Value as string;

                if (value?.StartsWith("WOTRMultiplayer.") ?? false)
                {
                    var lastElement = value.Split('.').LastOrDefault();
                    if (!string.IsNullOrWhiteSpace(lastElement))
                    {
                        logEvent.AddOrUpdateProperty(new LogEventProperty("SourceContext", new ScalarValue(lastElement)));
                    }
                }

            }
        }
    }
}
