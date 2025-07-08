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
            IMultiplayerHost multiplayerHost,
            IMultiplayerClient multiplayerClient)
            : base(logger, multiplayerHost, multiplayerClient)
        {
        }

        public void HandleAddCompanion(UnitEntityData unit)
        {
            var multiplayerParticipant = GetMultiplayerParticipant();
            if (multiplayerParticipant == null)
            {
                return;
            }

            Logger.LogInformation("HandleAddCompanion");
            multiplayerParticipant.PartyChanged();
        }

        public void HandleCapitalModeChanged()
        {
            var multiplayerParticipant = GetMultiplayerParticipant();
            if (multiplayerParticipant == null)
            {
                return;
            }

            Logger.LogInformation("HandleCapitalModeChanged");
            multiplayerParticipant.PartyChanged();
        }

        public void HandleCompanionActivated(UnitEntityData unit)
        {
            var multiplayerParticipant = GetMultiplayerParticipant();
            if (multiplayerParticipant == null)
            {
                return;
            }

            Logger.LogInformation("HandleCompanionActivated");
            multiplayerParticipant.PartyChanged();
        }

        public void HandleCompanionRemoved(UnitEntityData unit, bool stayInGame)
        {
            var multiplayerParticipant = GetMultiplayerParticipant();
            if (multiplayerParticipant == null)
            {
                return;
            }

            Logger.LogInformation("HandleCompanionRemoved");
            multiplayerParticipant.PartyChanged();
        }

        public void HandlePartyChanged()
        {
            var multiplayerParticipant = GetMultiplayerParticipant();
            if (multiplayerParticipant == null)
            {
                return;
            }

            Logger.LogInformation("HandlePartyChanged");
            multiplayerParticipant.PartyChanged();
        }

        public void HandlePartyCombatStateChanged(bool inCombat)
        {
            var multiplayerParticipant = GetMultiplayerParticipant();
            if (multiplayerParticipant == null)
            {
                return;
            }

            Logger.LogInformation("Combat state changed. InCombat={inCombat}", inCombat);
            if (inCombat)
            {
                multiplayerParticipant.CombatStarted();
                return;
            }

            multiplayerParticipant.CombatEnded();
        }

        public void HandlePartyLeaveArea(BlueprintArea currentArea, BlueprintAreaEnterPoint targetArea, AreaTransitionPart areaTransition)
        {
            if (!Host.IsActive)
            {
                return;
            }

            var areaExitId = areaTransition.View?.UniqueId;
            if (string.IsNullOrEmpty(areaExitId))
            {
                Logger.LogError("Missing area transition unique id");
                return;
            }

            Host.LeaveArea(areaExitId);
        }

        public void HandleRoundStarted(int round)
        {
            var multiplayerParticipant = GetMultiplayerParticipant();
            if (!multiplayerParticipant?.IsActive ?? false)
            {
                return;
            }

            multiplayerParticipant.CombatRoundStarted(round);
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
            var multiplayerParticipant = GetMultiplayerParticipant();
            if (!multiplayerParticipant?.IsActive ?? false)
            {
                return;
            }

            // looks dumb af, but seems like it's the only way to know game is loaded
            if (warningType != WarningNotificationType.GameLoaded)
            {
                return;
            }

            multiplayerParticipant.GameLoaded();
        }

        public void HandleWarning(string text, bool addToLog = true)
        {
        }

        public void OnAreaLoadingComplete()
        {
        }

        public void OnAreaScenesLoaded()
        {
            var multiplayerParticipant = GetMultiplayerParticipant();
            if (multiplayerParticipant == null)
            {
                return;
            }

            Logger.LogInformation("OnAreaScenesLoaded");
            multiplayerParticipant.PartyChanged();
        }
    }
}
