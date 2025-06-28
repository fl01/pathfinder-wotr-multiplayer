namespace WOTRMultiplayer.MP.Entities
{
    public class NetworkPlayer
    {
        public long Id { get; private set; }

        public string Name { get; set; }

        public bool IsReady { get; set; }

        public bool IsSyncedToStartGame { get; set; }

        public bool IsLoading { get; set; }

        public NetworkPlayer(long id)
        {
            Id = id;
        }
    }
}
