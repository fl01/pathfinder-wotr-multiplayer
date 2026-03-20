using System.Linq;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Logging.Object;

namespace WOTRMultiplayer.Logging.Extensions
{
    public static class LoggerExtensions
    {
        public static void LogObject(this ILogger logger, LogLevel logLevel, string template, object obj, params object[] args)
        {
            var loggingInfo = ObjectLoggingMetadata.GetLoggingInfo(obj);
            if (loggingInfo == null)
            {
                return;
            }

            var fullArgs = new object[] { obj?.GetType().Name }.Concat(args).Concat(loggingInfo.Values).ToArray();
            var properties = string.Join(", ", loggingInfo.Keys.Select(x => $"{x}={{{x}}}"));
            var fullTemplate = template + (template.EndsWith(".") ? ' ' : ", ") + properties;
            logger.Log(logLevel, fullTemplate, fullArgs);
        }
    }
}
