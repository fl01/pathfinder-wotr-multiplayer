using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Rolls
{
    public class CastSpellRoll : NetworkDiceRollBase
    {
        public int ArcaneSpellFailureChance { get; set; }

        public int SpellFailureChance { get; set; }

        public bool IsSpellFailure { get; set; }

        public CastSpellRoll(string initiatorId, string ruleName, NetworkDiceRollType networkDiceRollType, int totalModifierBonus)
            : base(initiatorId, ruleName, networkDiceRollType, totalModifierBonus)
        {
        }

        public override IEnumerable<string> GetUniquinessIdentifiers()
        {
            return [ArcaneSpellFailureChance.ToString(), SpellFailureChance.ToString(), IsSpellFailure.ToString()];
        }
    }
}
