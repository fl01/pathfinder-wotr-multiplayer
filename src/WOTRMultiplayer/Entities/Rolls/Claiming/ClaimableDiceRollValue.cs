using System.Collections.Concurrent;

namespace WOTRMultiplayer.Entities.Rolls.Claiming
{
    public class ClaimableDiceRollValue<TValue>
    {
        public TValue Roll { get; set; }

        public ConcurrentDictionary<long, bool> ClaimingList { get; set; } = new();

        public int OrderId { get; set; }

        public bool IsClaimed => ClaimingList.Count == 0;
    }
}
