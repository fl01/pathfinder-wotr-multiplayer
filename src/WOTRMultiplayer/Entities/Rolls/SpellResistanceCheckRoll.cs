using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Rolls
{
    public class SpellResistanceCheckRoll : NetworkDiceRollBase
    {
        public SpellResistanceCheckRoll(string initiatorId, string ruleName, NetworkDiceRollType networkDiceRollType, int totalModifierBonus)
            : base(initiatorId, ruleName, networkDiceRollType, totalModifierBonus)
        {
        }

        public int SpellPenetration { get; set; }

        public int SpellResistance { get; set; }

        public string SchoolType { get; set; }

        public string AbilityType { get; set; }

        public string TargetId { get; set; }

        public string AbilityName { get; set; }

        public string ActionType { get; set; }

        public override IEnumerable<string> GetUniquinessIdentifiers()
        {
            return [SpellPenetration.ToString(), SpellResistance.ToString(), SchoolType, AbilityType, TargetId, AbilityType, ActionType];
        }
    }
}
