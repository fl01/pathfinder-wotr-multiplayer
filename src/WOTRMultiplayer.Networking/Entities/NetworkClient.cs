namespace WOTRMultiplayer.Networking.Entities
{
    public class NetworkClient
    {
        public long Id { get; private set; }

        public string Name { get; set; }

        public NetworkClient(long id)
        {
            Id = id;
        }
    }
}
