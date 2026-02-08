using System;

namespace WOTRMultiplayer.Extensions
{
    public static class MathOperationsExtensions
    {
        public static TimeSpan Add(this TimeSpan timeSpan, TimeSpan other)
        {
            try
            {
                checked
                {
                    return timeSpan + other;
                }
            }
            catch (OverflowException)
            {
                return TimeSpan.MaxValue;
            }
        }
    }
}
