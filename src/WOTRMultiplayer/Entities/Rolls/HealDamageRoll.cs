using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Rolls
{
    public class HealDamageRoll : NetworkDiceRollBase
    {
        public string TargetId { get; set; }

        public string AbilityName { get; set; }

        public string AbilitySchoolId { get; set; }

        public float EmpowerModifier { get; set; }

        public bool IsTacticalCombat { get; set; }

        public int AdditionalBonus { get; set; }

        public float HealResistance { get; set; }

        public HealDamageRoll(string initiatorId, string ruleName, NetworkDiceRollType networkDiceRollType, int totalModifierBonus)
            : base(initiatorId, ruleName, networkDiceRollType, totalModifierBonus)
        {
        }

        protected override IEnumerable<string> GetRollIdentifier()
        {
            return [TargetId, AbilityName, AbilitySchoolId, EmpowerModifier.ToString(), IsTacticalCombat.ToString(), AdditionalBonus.ToString(), HealResistance.ToString()];
        }
    }
}
