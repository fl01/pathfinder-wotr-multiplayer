using Kingmaker.PubSubSystem;
using Kingmaker.UI;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.PubSub;

namespace WOTRMultiplayer.PubSub
{
    public class GlobalSubscriber : IGlobalMultiplayerSubscriber, ISubscriber, IGlobalSubscriber, IWarningNotificationUIHandler
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

        public void HandleWarning(WarningNotificationType warningType, bool addToLog = true)
        {
            // looks dumb af, but seems like it's the only way to know game is loaded
            if (warningType != WarningNotificationType.GameLoaded)
            {
                return;
            }

            _logger.LogInformation("Game loaded");
            var multiplayerParticipant = GetMultiplayerParticipant();
            multiplayerParticipant.GameLoaded();
        }

        public void HandleWarning(string text, bool addToLog = true)
        {
        }

        private IMultiplayerParticipant GetMultiplayerParticipant()
        {
            return _multiplayerHost.IsActive ? _multiplayerHost : _multiplayerClient;
        }
    }
}
