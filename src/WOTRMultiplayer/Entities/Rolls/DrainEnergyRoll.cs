using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Rolls
{
    public class DrainEnergyRoll : NetworkDiceRollBase
    {
        public int DiceRolls { get; set; }

        public string DiceFormulaType { get; set; }

        public string CriticalModifierName { get; set; }

        public bool TargetIsImmune { get; set; }

        public int DrainValue { get; set; }

        public bool Empower { get; set; }

        public DrainEnergyRoll(string initiatorId, string ruleName, NetworkDiceRollType networkDiceRollType, int totalModifierBonus)
            : base(initiatorId, ruleName, networkDiceRollType, totalModifierBonus)
        {
        }

        public override IEnumerable<string> GetUniquinessIdentifiers()
        {
            return [DiceRolls.ToString(), DiceFormulaType, CriticalModifierName, TargetIsImmune.ToString(), DrainValue.ToString(), Empower.ToString()];
        }
    }
}
