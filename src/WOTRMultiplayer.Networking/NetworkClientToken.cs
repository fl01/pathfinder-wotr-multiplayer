using BeetleX;

namespace WOTRMultiplayer.Networking
{
    public class NetworkClientToken : ISessionToken
    {
        public void Dispose()
        {
        }

        public void Init(IServer server, ISession session)
        {
            session.Send(new Messages.System.NetworkClientNameRequest());
        }
    }
}
