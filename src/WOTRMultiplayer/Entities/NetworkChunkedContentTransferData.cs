namespace WOTRMultiplayer.Entities
{
    public class NetworkChunkedContentTransferData
    {
        public int CurrentOffset { get; set; }

        public int MaxOffset { get; set; }

        public int ConfirmedChunk { get; set; }

        public int SentChunk { get; set; }
    }
}
