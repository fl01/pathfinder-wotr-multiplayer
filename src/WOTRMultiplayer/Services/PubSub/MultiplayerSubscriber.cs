using System;
using AutoMapper;
using Kingmaker.Blueprints.Area;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.PubSubSystem;
using Kingmaker.UI;
using Kingmaker.View.MapObjects;
using Kingmaker.View.MapObjects.Traps;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions;
using WOTRMultiplayer.Abstractions.PubSub;
using WOTRMultiplayer.Entities.Area;
using WOTRMultiplayer.Entities.MapObjects;

namespace WOTRMultiplayer.Services.PubSub
{
    public class MultiplayerSubscriber : MultiplayerSubscriberBase,
        IMultiplayerGlobalSubscriber,
        IPartyLeaveAreaHandler,
        IPartyChangedUIHandler,
        IPartyHandler,
        IAreaLoadingStagesHandler,
        IWarningNotificationUIHandler,
        ITrapActivationHandler
    {
        public MultiplayerSubscriber(
            ILogger<MultiplayerSubscriber> logger,
            IMultiplayerActorAccessor multiplayerActorAccessor,
            IMapper mapper)
            : base(logger, multiplayerActorAccessor, mapper)
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
            try
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

                var transition = new NetworkAreaTransition
                {
                    AreaExitId = areaExitId,
                    IsActionsTransition = false, // HandlePartyLeaveArea is never called on actions transition
                    From = new NetworkArea { Id = currentArea.AssetGuid.ToString(), Name = currentArea.name },
                    To = new NetworkArea { Id = targetArea.AssetGuid.ToString(), Name = targetArea.name }
                };

                ActorAccessor.Host.OnAreaTransition(transition);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to handle party leave area event");
                throw;
            }
        }

        public void HandleTrapActivation(UnitEntityData unit, TrapObjectView trap)
        {
            try
            {
                if (ActorAccessor.Current == null)
                {
                    return;
                }

                var trapObject = Main.Mapper.Map<NetworkMapObject>(trap.Data);
                ActorAccessor.Current.OnTrapActivation(unit.UniqueId, trapObject);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to handle OnGameLoaded event");
                throw;
            }
        }

        public void HandleWarning(WarningNotificationType warningType, bool addToLog = true)
        {
            try
            {
                if (ActorAccessor.Current == null || warningType != WarningNotificationType.GameLoaded)
                {
                    return;
                }

                ActorAccessor.Current.OnGameLoaded();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to handle OnGameLoaded event");
                throw;
            }
        }

        public void HandleWarning(string text, bool addToLog = true)
        {
        }

        public void OnAreaLoadingComplete()
        {
            try
            {
                if (ActorAccessor.Current == null)
                {
                    return;
                }

                Logger.LogInformation("OnAreaLoadingComplete");
                ActorAccessor.Current.OnAreaLoadingComplete();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to handle OnAreaLoadingComplete event");
                throw;
            }
        }

        public void OnAreaScenesLoaded()
        {
            Logger.LogInformation("OnAreaScenesLoaded");
        }

        private void OnPartyChanged()
        {
            try
            {
                if (ActorAccessor.Current == null)
                {
                    return;
                }

                ActorAccessor.Current.UpdateCharactersOwnership();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to handle OnPartyChanged event");
                throw;
            }
        }
    }
}
