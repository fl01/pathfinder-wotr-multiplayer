using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Rolls
{
    public class HealDamageRoll : NetworkDiceRollBase
    {
        public string TargetId { get; set; }

        public string AbilityName { get; set; }

        public string AbilitySchoolId { get; set; }

        public int UnitsCount { get; set; }

        public float EmpowerModifier { get; set; }

        public bool IsTacticalCombat { get; set; }

        public int AdditionalBonus { get; set; }

        public float HealResistance { get; set; }

        public HealDamageRoll(string initiatorId, string ruleName, NetworkDiceRollType networkDiceRollType, int totalModifierBonus)
            : base(initiatorId, ruleName, networkDiceRollType, totalModifierBonus)
        {
        }

        public override IEnumerable<string> GetUniquinessIdentifiers()
        {
            return [TargetId, AbilityName, AbilitySchoolId, UnitsCount.ToString(), EmpowerModifier.ToString(), IsTacticalCombat.ToString(), AdditionalBonus.ToString(), HealResistance.ToString()];
        }
    }
}
