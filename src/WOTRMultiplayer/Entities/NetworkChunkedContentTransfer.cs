using System.Collections.Concurrent;

namespace WOTRMultiplayer.Entities
{
    public class NetworkChunkedContentTransfer
    {
        public byte[] Content { get; set; }

        public int TotalChunks { get; set; }

        public int BatchSize { get; set; }

        public ConcurrentDictionary<long, NetworkChunkedContentTransferData> Data { get; set; } = [];
    }
}
