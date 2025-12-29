using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions;

namespace WOTRMultiplayer.PubSub
{
    public abstract class MultiplayerSubscriberBase
    {
        protected ILogger Logger { get; private set; }

        protected IMultiplayerActorAccessor ActorAccessor { get; private set; }

        public MultiplayerSubscriberBase(ILogger logger, IMultiplayerActorAccessor multiplayerActorAccessor)
        {
            Logger = logger;
            ActorAccessor = multiplayerActorAccessor;
        }
    }
}
