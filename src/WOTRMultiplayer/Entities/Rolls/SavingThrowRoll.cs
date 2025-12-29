using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Rolls
{
    public class SavingThrowRoll : NetworkDiceRollBase
    {
        public string StatType { get; set; }

        public string ReasonAbilityName { get; set; }

        public string ReasonCasterId { get; set; }

        public int DifficultyClass { get; set; }

        public SavingThrowRoll(string initiatorId, string ruleName, NetworkDiceRollType networkDiceRollType, int totalModifierBonus)
            : base(initiatorId, ruleName, networkDiceRollType, totalModifierBonus)
        {
        }

        public override IEnumerable<string> GetUniquinessIdentifiers()
        {
            return [StatType, ReasonAbilityName, ReasonCasterId, DifficultyClass.ToString()];
        }
    }
}
