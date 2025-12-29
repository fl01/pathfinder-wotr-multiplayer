using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Rolls
{
    public class CastingDefensivelyRoll : NetworkDiceRollBase
    {
        public int DC { get; set; }

        public int Concentration { get; set; }

        public string AbilityName { get; set; }

        public CastingDefensivelyRoll(string initiatorId, string ruleName, NetworkDiceRollType networkDiceRollType, int totalModifierBonus)
            : base(initiatorId, ruleName, networkDiceRollType, totalModifierBonus)
        {
        }

        public override IEnumerable<string> GetUniquinessIdentifiers()
        {
            return [DC.ToString(), Concentration.ToString(), AbilityName];
        }
    }
}
