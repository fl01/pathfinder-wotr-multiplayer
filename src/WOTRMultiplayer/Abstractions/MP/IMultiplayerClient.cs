using WOTRMultiplayer.MP;

namespace WOTRMultiplayer.Abstractions.MP
{
    public interface IMultiplayerClient
    {
        void Join(string address, MultiplayerSettings settings);

        void ReadyChanged(bool isReady);
    }
}
