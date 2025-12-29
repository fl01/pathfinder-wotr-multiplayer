using System.Collections.Concurrent;

namespace WOTRMultiplayer.Services.Random
{
    public class UniqueIdCounters
    {
        public ConcurrentDictionary<string, uint> NameIdentifiers { get; } = new();
    }
}
