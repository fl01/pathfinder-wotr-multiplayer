using System.Collections.Generic;

namespace WOTRMultiplayer.MP.Entities.Rolls
{
    public class ParryRoll : NetworkDiceRollBase
    {
        public string TargetId { get; set; }

        public string WeaponId { get; set; }

        public ParryRoll(string initiatorId, string ruleName, NetworkDiceRollType networkDiceRollType, int totalModifierBonus)
            : base(initiatorId, ruleName, networkDiceRollType, totalModifierBonus)
        {
        }

        public override IEnumerable<string> GetUniquinessIdentifiers()
        {
            return [TargetId.ToString(), WeaponId];
        }
    }
}
