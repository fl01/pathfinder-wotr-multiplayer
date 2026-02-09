using Kingmaker.EntitySystem.Entities;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem.Rules;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions;
using WOTRMultiplayer.Abstractions.PubSub;

namespace WOTRMultiplayer.Services.PubSub
{
    public class MultiplayerCombatSubscriber : MultiplayerSubscriberBase,
        IMultiplayerGlobalSubscriber,
        IPartyCombatHandler,
        ITurnBasedModeHandler
    {
        public MultiplayerCombatSubscriber(
            ILogger<MultiplayerCombatSubscriber> logger,
            IMultiplayerActorAccessor multiplayerActorAccessor)
            : base(logger, multiplayerActorAccessor)
        {
        }

        public void HandleTurnStarted(UnitEntityData unit)
        {
        }

        public void HandleUnitControlChanged(UnitEntityData unit)
        {
        }

        public void HandleUnitNotSurprised(UnitEntityData unit, RuleSkillCheck perceptionCheck)
        {
        }

        public void HandleRoundStarted(int round)
        {
            if (ActorAccessor.Current == null)
            {
                return;
            }

            ActorAccessor.Current.CombatRoundStarted(round);
        }

        public void HandleSurpriseRoundStarted()
        {
        }

        public void HandlePartyCombatStateChanged(bool inCombat)
        {
            if (ActorAccessor.Current == null)
            {
                return;
            }

            Logger.LogInformation("Combat state changed. InCombat={InCombat}", inCombat);
            if (inCombat)
            {
                ActorAccessor.Current.CombatStarted();
                return;
            }

            ActorAccessor.Current.CombatEnded();
        }
    }
}
