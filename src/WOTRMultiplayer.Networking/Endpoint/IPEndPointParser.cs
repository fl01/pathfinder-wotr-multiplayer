using System;
using System.Globalization;
using System.Net;
using WOTRMultiplayer.Networking.Abstractions;

namespace WOTRMultiplayer.Networking.Endpoint
{
    public class IPEndPointParser : IIPEndPointParser
    {
        public bool TryParse(string value, out IPEndPoint result)
        {
            return TryParse(value.AsSpan(), out result);
        }

        public bool TryParse(ReadOnlySpan<char> value, out IPEndPoint result)
        {
            int addressLength = value.Length;  // If there's no port then send the entire string to the address parser
            int lastColonPos = value.LastIndexOf(':');

            // Look to see if this is an IPv6 address with a port.
            if (lastColonPos > 0)
            {
                if (value[lastColonPos - 1] == ']')
                {
                    addressLength = lastColonPos;
                }
                // Look to see if this is IPv4 with a port (IPv6 will have another colon)
                else if (value.Slice(0, lastColonPos).LastIndexOf(':') == -1)
                {
                    addressLength = lastColonPos;
                }
            }

            if (IPAddress.TryParse(value.Slice(0, addressLength).ToString(), out IPAddress address))
            {
                uint port = 0;
                if (addressLength == value.Length ||
                    uint.TryParse(value.Slice(addressLength + 1).ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out port) && port <= IPEndPoint.MaxPort)

                {
                    result = new IPEndPoint(address, (int)port);
                    return true;
                }
            }

            result = null;
            return false;
        }
    }
}
