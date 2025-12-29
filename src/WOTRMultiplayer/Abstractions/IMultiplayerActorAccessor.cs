using Kingmaker.GameModes;

namespace WOTRMultiplayer.Abstractions
{
    public interface IMultiplayerActorAccessor
    {
        IMultiplayerHost Host { get; }

        IMultiplayerClient Client { get; }

        IMultiplayerActor Current { get; }
    }
}
