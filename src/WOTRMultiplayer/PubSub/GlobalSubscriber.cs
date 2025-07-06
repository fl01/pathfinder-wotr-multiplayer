using Kingmaker.Blueprints.Area;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.PubSubSystem;
using Kingmaker.UI;
using Kingmaker.View.MapObjects;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.PubSub;

namespace WOTRMultiplayer.PubSub
{
    public class GlobalSubscriber : IGlobalMultiplayerSubscriber, ISubscriber, IGlobalSubscriber,
        IWarningNotificationUIHandler,
        IPartyLeaveAreaHandler,
        IPartyChangedUIHandler,
        IPartyHandler
    {
        private readonly ILogger<GlobalSubscriber> _logger;
        private readonly IMultiplayerHost _multiplayerHost;
        private readonly IMultiplayerClient _multiplayerClient;

        public GlobalSubscriber(
            ILogger<GlobalSubscriber> logger,
            IMultiplayerHost multiplayerHost,
            IMultiplayerClient multiplayerClient)
        {
            _logger = logger;
            _multiplayerHost = multiplayerHost;
            _multiplayerClient = multiplayerClient;
        }

        public void HandleAddCompanion(UnitEntityData unit)
        {
            var multiplayerParticipant = GetMultiplayerParticipant();
            if (!multiplayerParticipant?.IsActive ?? false)
            {
                return;
            }

            _logger.LogInformation("HandleAddCompanion");
            multiplayerParticipant.PartyChanged();
        }

        public void HandleCapitalModeChanged()
        {
            var multiplayerParticipant = GetMultiplayerParticipant();
            if (!multiplayerParticipant?.IsActive ?? false)
            {
                return;
            }

            _logger.LogInformation("HandleCapitalModeChanged");
            multiplayerParticipant.PartyChanged();
        }

        public void HandleCompanionActivated(UnitEntityData unit)
        {
            var multiplayerParticipant = GetMultiplayerParticipant();
            if (!multiplayerParticipant?.IsActive ?? false)
            {
                return;
            }

            _logger.LogInformation("HandleCompanionActivated");
            multiplayerParticipant.PartyChanged();
        }

        public void HandleCompanionRemoved(UnitEntityData unit, bool stayInGame)
        {
            var multiplayerParticipant = GetMultiplayerParticipant();
            if (!multiplayerParticipant?.IsActive ?? false)
            {
                return;
            }

            _logger.LogInformation("HandleCompanionRemoved");
            multiplayerParticipant.PartyChanged();
        }

        public void HandlePartyChanged()
        {
            var multiplayerParticipant = GetMultiplayerParticipant();
            if (!multiplayerParticipant?.IsActive ?? false)
            {
                return;
            }

            _logger.LogInformation("HandlePartyChanged");
            multiplayerParticipant.PartyChanged();
        }

        public void HandlePartyLeaveArea(BlueprintArea currentArea, BlueprintAreaEnterPoint targetArea, AreaTransitionPart areaTransition)
        {
            if (!_multiplayerHost.IsActive)
            {
                return;
            }

            var areaExitId = areaTransition.View?.UniqueId;
            if (string.IsNullOrEmpty(areaExitId))
            {
                _logger.LogError("Missing area transition unique id");
                return;
            }

            _multiplayerHost.LeaveArea(areaExitId);
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

            // TODO: update party members since saveinfo doesn't contain exact character names
            multiplayerParticipant.GameLoaded();
        }

        public void HandleWarning(string text, bool addToLog = true)
        {
        }

        private IMultiplayerParticipant GetMultiplayerParticipant()
        {
            return _multiplayerHost.IsActive ?
                _multiplayerHost
                : _multiplayerClient.IsActive ?
                    _multiplayerClient : null;
        }
    }
}
