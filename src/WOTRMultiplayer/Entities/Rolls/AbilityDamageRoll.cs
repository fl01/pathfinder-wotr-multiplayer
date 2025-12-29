using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Rolls
{
    public class AbilityDamageRoll : NetworkDiceRollBase
    {
        public string TargetId { get; set; }

        public string AbilityName { get; set; }

        public string AbilitySchoolId { get; set; }

        public AbilityDamageRoll(string initiatorId, string ruleName, NetworkDiceRollType networkDiceRollType, int totalModifierBonus)
            : base(initiatorId, ruleName, networkDiceRollType, totalModifierBonus)
        {
        }

        public override IEnumerable<string> GetUniquinessIdentifiers()
        {
            return [TargetId, AbilityName, AbilitySchoolId];
        }
    }
}
