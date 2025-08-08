using Kingmaker.GameModes;
using WOTRMultiplayer.Abstractions.MP.Actors;

namespace WOTRMultiplayer.Abstractions.MP
{
    public interface IMultiplayerActorAccessor
    {
        IMultiplayerHost Host { get; }

        IMultiplayerClient Client { get; }

        IMultiplayerActor Current { get; }
    }
}
