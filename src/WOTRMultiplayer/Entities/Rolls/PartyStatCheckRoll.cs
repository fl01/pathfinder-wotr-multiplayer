using System.Collections.Generic;
using Kingmaker.EntitySystem.Stats;

namespace WOTRMultiplayer.Entities.Rolls
{
    public class PartyStatCheckRoll : NetworkDiceRollBase
    {
        public int DifficultyClass { get; set; }

        public string StatType { get; set; }

        public PartyStatCheckRoll(string initiatorId, string ruleName, NetworkDiceRollType networkDiceRollType, int totalModifierBonus)
            : base(initiatorId, ruleName, networkDiceRollType, totalModifierBonus)
        {
        }

        public override IEnumerable<string> GetUniquinessIdentifiers()
        {
            return [DifficultyClass.ToString(), StatType];
        }
    }
}
