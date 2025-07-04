using Kingmaker.Blueprints.Area;
using Kingmaker.PubSubSystem;
using Kingmaker.UI;
using Kingmaker.View.MapObjects;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.PubSub;

namespace WOTRMultiplayer.PubSub
{
    public class GlobalSubscriber : IGlobalMultiplayerSubscriber, ISubscriber, IGlobalSubscriber, IWarningNotificationUIHandler, IPartyLeaveAreaHandler
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
