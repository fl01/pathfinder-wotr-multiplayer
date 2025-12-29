using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Rolls
{
    public class DispelMagicRoll : NetworkDiceRollBase
    {
        public string CheckType { get; set; }

        public int DC { get; set; }

        public int CasterLevel { get; set; }

        public string BuffName { get; set; }

        public string Skill { get; set; }

        public string AreaEffectName { get; internal set; }

        public DispelMagicRoll(string initiatorId, string ruleName, NetworkDiceRollType networkDiceRollType, int totalModifierBonus)
            : base(initiatorId, ruleName, networkDiceRollType, totalModifierBonus)
        {
        }

        public override IEnumerable<string> GetUniquinessIdentifiers()
        {
            return [CheckType, DC.ToString(), CasterLevel.ToString(), BuffName, Skill, BuffName, AreaEffectName];
        }
    }
}
