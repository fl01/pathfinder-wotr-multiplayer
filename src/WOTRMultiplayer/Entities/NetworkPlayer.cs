namespace WOTRMultiplayer.Entities
{
    public class NetworkPlayer
    {
        public long Id { get; private set; }

        public string Name { get; set; }

        public bool IsReady { get; set; }

        public NetworkPlayer(long id)
        {
            Id = id;
        }
    }
}
