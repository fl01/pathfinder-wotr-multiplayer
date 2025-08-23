using BeetleX;

namespace WOTRMultiplayer.Networking
{
    public class NetworkConnectionToken : ISessionToken
    {
        public long Id { get; set; }

        public void Dispose()
        {
        }

        public void Init(IServer server, ISession session)
        {
            Id = session.ID;
        }
    }
}
