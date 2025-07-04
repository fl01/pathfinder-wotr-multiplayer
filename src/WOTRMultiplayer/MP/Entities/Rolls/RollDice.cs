using System.Collections.Generic;
using Kingmaker.RuleSystem;

namespace WOTRMultiplayer.MP.Entities.Rolls
{
    public class RollDice
    {
        public int Result { get; set; }

        public List<int> RollHistory { get; set; } = [];

        public string InitiatorId { get; set; }

        public DiceType DiceType { get; set; }

        public int? ResultOverride { get; set; }

        public string RuleRollType { get; set; }

        public string RuleRollName { get; set; }

        public virtual string GetIdString()
        {
            return InitiatorId + DiceType.ToString() + RuleRollType + RuleRollName;
        }
    }
}
