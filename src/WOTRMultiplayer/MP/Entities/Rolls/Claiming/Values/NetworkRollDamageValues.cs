using System.Collections.Generic;
using System.Linq;

namespace WOTRMultiplayer.MP.Entities.Rolls.Claiming.Values
{
    public class NetworkRollDamageValues : TypedRollValueBase<List<NetworkRollDamageRoll>>
    {
        public override string ToString()
        {
            return string.Join(",", Value.Select(x => $"[{x.ValueWithoutReduction}]"));
        }
    }
}
