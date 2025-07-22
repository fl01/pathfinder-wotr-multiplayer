using Kingmaker.EntitySystem.Stats;

namespace WOTRMultiplayer.MP.Entities.Rolls
{
    public class PartyStatCheckRoll : NetworkDiceRoll
    {
        public int DifficultyClass { get; set; }

        public StatType StatType { get; set; }

        public override string GetIdString()
        {
            return string.Join(IdSeparator, base.GetIdString(), DifficultyClass, StatType.ToString());
        }
    }
}
