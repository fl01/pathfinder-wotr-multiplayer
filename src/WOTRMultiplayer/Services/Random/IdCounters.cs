using System.Collections.Concurrent;

namespace WOTRMultiplayer.Services.Random
{
    public class IdCounters
    {
        public ConcurrentDictionary<string, uint> NameIdentifiers { get; } = new();
    }
}
