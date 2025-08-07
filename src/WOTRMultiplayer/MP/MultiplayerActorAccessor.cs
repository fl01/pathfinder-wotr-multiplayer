using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.MP.Actors;

namespace WOTRMultiplayer.MP
{
    public class MultiplayerActorAccessor : IMultiplayerActorAccessor
    {
        private readonly IMultiplayerHost _multiplayerHost;
        private readonly IMultiplayerClient _multiplayerClient;

        public MultiplayerActorAccessor(
            IMultiplayerHost multiplayerHost,
            IMultiplayerClient multiplayerClient)
        {
            _multiplayerHost = multiplayerHost;
            _multiplayerClient = multiplayerClient;
        }

        public IMultiplayerHost Host => _multiplayerHost;

        public IMultiplayerClient Client => _multiplayerClient;

        public IMultiplayerActor Current => _multiplayerHost.IsActive ? _multiplayerHost
            : _multiplayerClient.IsActive ?
            _multiplayerClient : null;
    }
}
