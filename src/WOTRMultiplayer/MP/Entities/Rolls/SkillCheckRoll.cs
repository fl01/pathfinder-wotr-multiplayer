using System.Collections.Generic;

namespace WOTRMultiplayer.MP.Entities.Rolls
{
    public class SkillCheckRoll : NetworkDiceRollBase
    {
        public bool? EnsureSuccess { get; set; }

        public int DifficultyCheck { get; set; }

        public bool RequireSuccessBonus { get; set; }

        public bool Take10ForSuccess { get; set; }

        public string StatType { get; set; }

        public SkillCheckRoll(string initiatorId, string ruleName, NetworkDiceRollType networkDiceRollType, int totalModifierBonus)
            : base(initiatorId, ruleName, networkDiceRollType, totalModifierBonus)
        {
        }

        public override IEnumerable<string> GetUniquinessIdentifiers()
        {
            return [EnsureSuccess?.ToString(), DifficultyCheck.ToString(), RequireSuccessBonus.ToString(), Take10ForSuccess.ToString(), StatType];
        }
    }
}
