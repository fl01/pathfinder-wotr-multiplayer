using Kingmaker.Blueprints.Area;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.PubSubSystem;
using Kingmaker.View.MapObjects;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions;
using WOTRMultiplayer.Abstractions.Pubsub;

namespace WOTRMultiplayer.PubSub
{
    public class MultiplayerSubscriber : MultiplayerSubscriberBase,
        IMultiplayerGlobalSubscriber,
        IPartyLeaveAreaHandler,
        IPartyChangedUIHandler,
        IPartyHandler,
        IAreaLoadingStagesHandler
    {
        public MultiplayerSubscriber(
            ILogger<MultiplayerSubscriber> logger,
            IMultiplayerActorAccessor multiplayerActorAccessor)
            : base(logger, multiplayerActorAccessor)
        {
        }

        public void HandleAddCompanion(UnitEntityData unit)
        {
            Logger.LogInformation("HandleAddCompanion");
            OnPartyChanged();
        }

        public void HandleCapitalModeChanged()
        {
            Logger.LogInformation("HandleCapitalModeChanged");
            OnPartyChanged();
        }

        public void HandleCompanionActivated(UnitEntityData unit)
        {
            Logger.LogInformation("HandleCompanionActivated");
            OnPartyChanged();
        }

        public void HandleCompanionRemoved(UnitEntityData unit, bool stayInGame)
        {
            Logger.LogInformation("HandleCompanionRemoved");
            OnPartyChanged();
        }

        public void HandlePartyChanged()
        {
            Logger.LogInformation("HandlePartyChanged");
            OnPartyChanged();
        }

        public void HandlePartyLeaveArea(BlueprintArea currentArea, BlueprintAreaEnterPoint targetArea, AreaTransitionPart areaTransition)
        {
            if (ActorAccessor.Current == null || !ActorAccessor.Host.IsActive)
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

        public void OnAreaLoadingComplete()
        {
            if (ActorAccessor.Current == null)
            {
                return;
            }

            Logger.LogInformation("OnAreaLoadingComplete");
            ActorAccessor.Current.OnAreaLoadingComplete();
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

        private void OnPartyChanged()
        {
            if (ActorAccessor.Current == null)
            {
                return;
            }

            ActorAccessor.Current.UpdateCharactersOwnership();
        }
    }
}
