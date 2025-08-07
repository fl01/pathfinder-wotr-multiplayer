using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.MP;

namespace WOTRMultiplayer.PubSub
{
    public abstract class GlobalMultiplayerSubscriberBase
    {
        protected ILogger Logger { get; private set; }

        protected IMultiplayerActorAccessor ActorAccessor { get; private set; }

        public GlobalMultiplayerSubscriberBase(ILogger logger, IMultiplayerActorAccessor multiplayerActorAccessor)
        {
            Logger = logger;
            ActorAccessor = multiplayerActorAccessor;
        }
    }
}
