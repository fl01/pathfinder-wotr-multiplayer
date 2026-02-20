using Kingmaker.RuleSystem;

namespace WOTRMultiplayer.Entities.Rolls
{
    public class WellKnownDiceFormula
    {
        public DiceFormula Formula { get; set; }

        public int Rerolls { get; set; }

        public WellKnownDiceFormula()
        {
        }

        public WellKnownDiceFormula(DiceFormula diceFormula, int rerolls)
        {
            Formula = diceFormula;
            Rerolls = rerolls;
        }
    }
}
