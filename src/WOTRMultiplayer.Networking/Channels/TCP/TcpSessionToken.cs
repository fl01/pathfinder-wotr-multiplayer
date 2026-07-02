using BeetleX;

namespace WOTRMultiplayer.Networking.Channels.TCP
{
    public class TcpSessionToken : ISessionToken
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
