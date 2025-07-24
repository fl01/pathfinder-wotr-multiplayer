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
        ITurnBasedModeHandler,
        IUnitCombatHandler
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
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return;
            }

            Logger.LogInformation("HandleAddCompanion");
            multiplayerActor.PartyChanged();
        }

        public void HandleCapitalModeChanged()
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return;
            }

            Logger.LogInformation("HandleCapitalModeChanged");
            multiplayerActor.PartyChanged();
        }

        public void HandleCompanionActivated(UnitEntityData unit)
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return;
            }

            Logger.LogInformation("HandleCompanionActivated");
            multiplayerActor.PartyChanged();
        }

        public void HandleCompanionRemoved(UnitEntityData unit, bool stayInGame)
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return;
            }

            Logger.LogInformation("HandleCompanionRemoved");
            multiplayerActor.PartyChanged();
        }

        public void HandlePartyChanged()
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return;
            }

            Logger.LogInformation("HandlePartyChanged");
            multiplayerActor.PartyChanged();
        }

        public void HandlePartyCombatStateChanged(bool inCombat)
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return;
            }

            Logger.LogInformation("Combat state changed. InCombat={inCombat}", inCombat);
            if (inCombat)
            {
                multiplayerActor.CombatStarted();
                return;
            }

            multiplayerActor.CombatEnded();
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
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return;
            }

            multiplayerActor.CombatRoundStarted(round);
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

        public void HandleUnitJoinCombat(UnitEntityData unit)
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null || !multiplayerActor.IsInCombat)
            {
                return;
            }

            // TODO
            Logger.LogError("NOT SYNCED: Unit joined mid combat. UnitId={unitId}, UnitName={unitName}", unit.UniqueId, unit.CharacterName);
        }

        public void HandleUnitLeaveCombat(UnitEntityData unit)
        {
        }

        public void HandleUnitNotSurprised(UnitEntityData unit, RuleSkillCheck perceptionCheck)
        {
        }

        public void HandleWarning(WarningNotificationType warningType, bool addToLog = true)
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return;
            }

            // looks dumb af, but seems like it's the only way to know game is loaded
            if (warningType != WarningNotificationType.GameLoaded)
            {
                return;
            }

            multiplayerActor.GameLoaded();
        }

        public void HandleWarning(string text, bool addToLog = true)
        {
        }

        public void OnAreaLoadingComplete()
        {
        }

        public void OnAreaScenesLoaded()
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return;
            }

            Logger.LogInformation("OnAreaScenesLoaded");
            multiplayerActor.PartyChanged();
        }
    }
}
