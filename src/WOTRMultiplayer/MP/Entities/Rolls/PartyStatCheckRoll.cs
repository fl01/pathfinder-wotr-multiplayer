using Kingmaker.EntitySystem.Stats;

namespace WOTRMultiplayer.MP.Entities.Rolls
{
    public class PartyStatCheckRoll : RollDice
    {
        public int DifficultyClass { get; set; }

        public StatType StatType { get; set; }

        public override string GetIdString()
        {
            return base.GetIdString() + DifficultyClass + StatType.ToString();
        }
    }
}
