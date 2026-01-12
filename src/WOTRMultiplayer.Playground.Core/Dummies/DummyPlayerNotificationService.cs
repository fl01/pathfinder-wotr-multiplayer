using Kingmaker.RuleSystem;
using WOTRMultiplayer.Abstractions.GameInteraction;

namespace WOTRMultiplayer.Playground.Core.Dummies
{
    public class DummyPlayerNotificationService : IPlayerNotificationService
    {
        public void AddCombatText(string messageKey, params object[] args)
        {
        }

        public void AddCombatText(RulebookEvent rulebookEvent)
        {
        }

        public void ShowModalMessage(string messageKey, params object[] args)
        {
        }

        public void ShowWarningNotification(string messageKey, bool addToLog = true, params object[] args)
        {
        }
    }
}
