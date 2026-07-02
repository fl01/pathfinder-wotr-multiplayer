using System.Net.Sockets;

namespace WOTRMultiplayer.Networking
{
    public class NetworkError
    {
        public NetworkErrorType Type { get; private set; }

        public SocketError? SocketError { get; set; }

        public NetworkError(NetworkErrorType type)
        {
            Type = type;
        }
    }
}
