using System;
using System.Net;

namespace WOTRMultiplayer.Networking.Abstractions
{
    public interface IIPEndPointParser
    {
        bool TryParse(string value, out IPEndPoint result);

        bool TryParse(ReadOnlySpan<char> value, out IPEndPoint result);
    }
}
