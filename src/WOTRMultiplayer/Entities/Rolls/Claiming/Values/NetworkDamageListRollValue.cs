using System.Collections.Generic;
using System.Linq;

namespace WOTRMultiplayer.Entities.Rolls.Claiming.Values
{
    public class NetworkDamageListRollValue : TypedRollValueBase<List<NetworkDamageRollValue>>
    {
        public override string ToString()
        {
            return string.Join(",", Value.Select(x => $"[{x.ValueWithoutReduction}]"));
        }
    }
}
