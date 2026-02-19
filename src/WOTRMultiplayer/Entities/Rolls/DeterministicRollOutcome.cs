using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Rolls
{
    public class DeterministicRollOutcome
    {
        public int Result { get; set; }

        public List<int> History { get; set; } = [];

        public int RollId { get; set; }

        public string Identifier { get; set; }
    }
}
