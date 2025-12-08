using WOTRMultiplayer.MP.Entities.Content;

namespace WOTRMultiplayer.MP.Entities
{
    public class NetworkPlayer
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public bool IsReady { get; set; }

        public NetworkContentState ContentState { get; set; }

        public NetworkPlayerSaveGameSyncStatus SaveGameSyncStatus { get; set; }

        public NetworkPlayer(long id)
        {
            Id = id;
        }

        public NetworkPlayer()
        {
        }
    }
}
