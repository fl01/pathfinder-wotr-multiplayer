using System.Collections.Generic;
using System.Linq;
using WOTRMultiplayer.Entities.Rolls.Claiming.Values;

namespace WOTRMultiplayer.Entities.Rolls.Claiming
{
    public class ClaimableDiceRollEntry
    {
        public List<ClaimableDiceRollValue<RollValueBase>> Rolls { get; set; } = [];

        public bool IsClaimed => Rolls.All(r => r.IsClaimed);
    }
}
