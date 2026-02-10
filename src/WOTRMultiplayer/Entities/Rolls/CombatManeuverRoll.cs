using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Rolls
{
    public class CombatManeuverRoll : NetworkDiceRollBase
    {
        public bool IncreasedDuration { get; set; }

        public int TargetCMD { get; set; }

        public string Type { get; set; }

        public string WeaponName { get; set; }

        public string TargetUnitId { get; set; }

        public CombatManeuverRoll(string initiatorId, string ruleName, NetworkDiceRollType networkDiceRollType, int totalModifierBonus)
            : base(initiatorId, ruleName, networkDiceRollType, totalModifierBonus)
        {
        }

        public override IEnumerable<string> GetUniquinessIdentifiers()
        {
            return [IncreasedDuration.ToString(), TargetCMD.ToString(), Type, WeaponName, TargetUnitId];
        }
    }
}
