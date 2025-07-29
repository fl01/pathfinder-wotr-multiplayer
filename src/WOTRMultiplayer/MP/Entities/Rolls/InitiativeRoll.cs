using System.Collections.Generic;

namespace WOTRMultiplayer.MP.Entities.Rolls
{
    public class InitiativeRoll : NetworkDiceRollBase
    {
        public InitiativeRoll(string initiatorId, string ruleName, NetworkDiceRollType networkDiceRollType, int totalModifierBonus)
            : base(initiatorId, ruleName, networkDiceRollType, totalModifierBonus)
        {
        }

        public override IEnumerable<string> GetUniquinessIdentifiers()
        {
            return [];
        }
    }
}
