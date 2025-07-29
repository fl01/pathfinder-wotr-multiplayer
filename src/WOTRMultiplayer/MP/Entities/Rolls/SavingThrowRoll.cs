using System.Collections.Generic;
using Kingmaker.EntitySystem.Stats;

namespace WOTRMultiplayer.MP.Entities.Rolls
{
    public class SavingThrowRoll : NetworkDiceRollBase
    {
        public StatType StatType { get; set; }

        public string ReasonAbilityName { get; set; }

        public string ReasonCasterId { get; set; }

        public int DifficultyClass { get; set; }

        public SavingThrowRoll(string initiatorId, string ruleName, NetworkDiceRollType networkDiceRollType, int totalModifierBonus)
            : base(initiatorId, ruleName, networkDiceRollType, totalModifierBonus)
        {
        }

        public override IEnumerable<string> GetUniquinessIdentifiers()
        {
            return [StatType.ToString(), ReasonAbilityName, ReasonCasterId, DifficultyClass.ToString()];
        }
    }
}
