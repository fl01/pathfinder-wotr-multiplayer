using WOTRMultiplayer.Entities.Content;

namespace WOTRMultiplayer.Entities
{
    public class NetworkPlayer
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public bool IsReady { get; set; }

        public bool IsHost { get; set; }

        public NetworkColor Color { get; set; }

        public NetworkContentState ContentState { get; set; } = new();

        public NetworkLobbySyncStatus LobbySyncStatus { get; set; }

        public NetworkPlayer(long id)
        {
            Id = id;
        }

        public NetworkPlayer()
        {
        }
    }
}
