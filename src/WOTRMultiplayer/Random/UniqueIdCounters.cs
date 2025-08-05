using System.Collections.Concurrent;

namespace WOTRMultiplayer.Random
{
    public class UniqueIdCounters
    {
        public ConcurrentDictionary<string, uint> NameIdentifiers { get; } = new();
    }
}
