using System;
using System.Globalization;
using System.Net;
using WOTRMultiplayer.Networking.Abstractions;

namespace WOTRMultiplayer.Networking
{
    public class IPEndPointParser : IIPEndPointParser
    {
        public IPEndPoint Parse(string rawValue)
        {
            var value = rawValue.AsSpan();
            int addressLength = value.Length;  // If there's no port then send the entire string to the address parser
            int lastColonPos = value.LastIndexOf(':');

            if (lastColonPos > 0 && (value[lastColonPos - 1] == ']' || value.Slice(0, lastColonPos).LastIndexOf(':') == -1))
            {
                addressLength = lastColonPos;
            }

            if (IPAddress.TryParse(value.Slice(0, addressLength).ToString(), out IPAddress address))
            {
                uint port = 0;
                if (addressLength == value.Length ||
                    uint.TryParse(value.Slice(addressLength + 1).ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out port) && port <= IPEndPoint.MaxPort)

                {
                    var result = new IPEndPoint(address, (int)port);
                    return result;
                }
            }

            return null;
        }
    }
}
