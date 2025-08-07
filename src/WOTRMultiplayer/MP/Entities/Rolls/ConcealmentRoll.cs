using System.Collections.Generic;

namespace WOTRMultiplayer.MP.Entities.Rolls
{
    public class ConcealmentRoll : NetworkDiceRollBase
    {
        public string Concealment { get; set; }

        public int MissChance { get; set; }

        public int ConcealmentValue { get; set; }
        public string TargetId { get; set; }
        public bool IsAttack { get; set; }

        public ConcealmentRoll(string initiatorId, string ruleName, NetworkDiceRollType networkDiceRollType, int totalModifierBonus)
            : base(initiatorId, ruleName, networkDiceRollType, totalModifierBonus)
        {
        }

        public override IEnumerable<string> GetUniquinessIdentifiers()
        {
            return [Concealment, MissChance.ToString(), ConcealmentValue.ToString(), TargetId, IsAttack.ToString()];
        }
    }
}
