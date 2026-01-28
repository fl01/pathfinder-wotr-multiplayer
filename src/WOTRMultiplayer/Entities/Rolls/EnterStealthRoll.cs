using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Rolls
{
    public class EnterStealthRoll : NetworkDiceRollBase
    {
        public bool IsFullSpeed { get; set; }

        public int? ResultOverride { get; set; }

        public EnterStealthRoll(string initiatorId, string ruleName, NetworkDiceRollType networkDiceRollType, int totalModifierBonus)
            : base(initiatorId, ruleName, networkDiceRollType, totalModifierBonus)
        {
        }

        public override IEnumerable<string> GetUniquinessIdentifiers()
        {
            return [IsFullSpeed.ToString(), ResultOverride?.ToString()];
        }
    }
}
