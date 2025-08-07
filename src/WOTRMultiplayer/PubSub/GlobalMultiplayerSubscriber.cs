using Kingmaker.Blueprints.Area;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UI;
using Kingmaker.View.MapObjects;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.PubSub;

namespace WOTRMultiplayer.PubSub
{
    public class GlobalMultiplayerSubscriber : GlobalMultiplayerSubscriberBase,
        IGlobalMultiplayerSubscriber,
        ISubscriber,
        IGlobalSubscriber,
        IWarningNotificationUIHandler,
        IPartyLeaveAreaHandler,
        IPartyChangedUIHandler,
        IPartyHandler,
        IAreaLoadingStagesHandler,
        IPartyCombatHandler,
        ITurnBasedModeHandler
    {
        public GlobalMultiplayerSubscriber(
            ILogger<GlobalMultiplayerSubscriber> logger,
            IMultiplayerActorAccessor multiplayerActorAccessor)
            : base(logger, multiplayerActorAccessor)
        {
        }

        public void HandleAddCompanion(UnitEntityData unit)
        {
            if (ActorAccessor.Current == null)
            {
                return;
            }

            Logger.LogInformation("HandleAddCompanion");
            ActorAccessor.Current.PartyChanged();
        }

        public void HandleCapitalModeChanged()
        {
            if (ActorAccessor.Current == null)
            {
                return;
            }

            Logger.LogInformation("HandleCapitalModeChanged");
            ActorAccessor.Current.PartyChanged();
        }

        public void HandleCompanionActivated(UnitEntityData unit)
        {
            if (ActorAccessor.Current == null)
            {
                return;
            }

            Logger.LogInformation("HandleCompanionActivated");
            ActorAccessor.Current.PartyChanged();
        }

        public void HandleCompanionRemoved(UnitEntityData unit, bool stayInGame)
        {
            if (ActorAccessor.Current == null)
            {
                return;
            }

            Logger.LogInformation("HandleCompanionRemoved");
            ActorAccessor.Current.PartyChanged();
        }

        public void HandlePartyChanged()
        {
            if (ActorAccessor.Current == null)
            {
                return;
            }

            Logger.LogInformation("HandlePartyChanged");
            ActorAccessor.Current.PartyChanged();
        }

        public void HandlePartyCombatStateChanged(bool inCombat)
        {
            if (ActorAccessor.Current == null)
            {
                return;
            }

            Logger.LogInformation("Combat state changed. InCombat={inCombat}", inCombat);
            if (inCombat)
            {
                ActorAccessor.Current.CombatStarted();
                return;
            }

            ActorAccessor.Current.CombatEnded();
        }

        public void HandlePartyLeaveArea(BlueprintArea currentArea, BlueprintAreaEnterPoint targetArea, AreaTransitionPart areaTransition)
        {
            if (!ActorAccessor.Host.IsActive)
            {
                return;
            }

            var areaExitId = areaTransition.View?.UniqueId;
            if (string.IsNullOrEmpty(areaExitId))
            {
                Logger.LogError("Missing area transition unique id");
                return;
            }

            ActorAccessor.Host.LeaveArea(areaExitId);
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

        public void HandleTurnStarted(UnitEntityData unit)
        {
        }

        public void HandleUnitControlChanged(UnitEntityData unit)
        {
        }

        public void HandleUnitNotSurprised(UnitEntityData unit, RuleSkillCheck perceptionCheck)
        {
        }

        public void HandleWarning(WarningNotificationType warningType, bool addToLog = true)
        {
            if (ActorAccessor.Current == null)
            {
                return;
            }

            // looks dumb af, but seems like it's the only way to know game is loaded
            if (warningType != WarningNotificationType.GameLoaded)
            {
                return;
            }

            ActorAccessor.Current.GameLoaded();
        }

        public void HandleWarning(string text, bool addToLog = true)
        {
        }

        public void OnAreaLoadingComplete()
        {
        }

        public void OnAreaScenesLoaded()
        {
            if (ActorAccessor.Current == null)
            {
                return;
            }

            Logger.LogInformation("OnAreaScenesLoaded");
            ActorAccessor.Current.OnAreaScenesLoaded();
        }
    }
}
