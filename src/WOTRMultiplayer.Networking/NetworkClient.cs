namespace WOTRMultiplayer.Networking
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
