using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.MP;

namespace WOTRMultiplayer.PubSub
{
    public abstract class GlobalMultiplayerSubscriberBase
    {
        protected ILogger Logger { get; private set; }

        protected IMultiplayerHost Host { get; private set; }

        protected IMultiplayerClient Client { get; private set; }

        public GlobalMultiplayerSubscriberBase(ILogger logger, IMultiplayerHost multiplayerHost, IMultiplayerClient client)
        {
            Logger = logger;
            Host = multiplayerHost;
            Client = client;
        }

        protected IMultiplayerParticipant GetMultiplayerParticipant()
        {
            return Host.IsActive ?
                Host
                : Client.IsActive ?
                    Client : null;
        }
    }
}
