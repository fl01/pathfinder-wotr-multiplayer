using System.Collections.Generic;
using System.Linq;

namespace WOTRMultiplayer.Entities.Rolls.Claiming.Values
{
    public class NetworkNamedIntRollValue : TypedRollValueBase<Dictionary<string, int>>
    {
        public override string ToString()
        {
            return string.Join(", ", Value?.Select(x => $"{{{x.Key}, {x.Value}}}"));
        }
    }
}
