using WOTRMultiplayer.MP;

namespace WOTRMultiplayer.Abstractions.MP
{
    public interface IMultiplayerHost
    {
        void Start(MultiplayerSettings multiplayerSettings);
    }
}
