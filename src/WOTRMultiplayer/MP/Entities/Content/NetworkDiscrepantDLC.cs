namespace WOTRMultiplayer.MP.Entities.Content
{
    public class NetworkDiscrepantDLC
    {
        public NetworkDLC DLC { get; set; }

        public NetworkDiscrepancyReason Reason { get; set; }

        public NetworkDiscrepantDLC(NetworkDLC dlc, NetworkDiscrepancyReason reason)
        {
            DLC = dlc;
            Reason = reason;
        }

        public NetworkDiscrepantDLC()
        {
        }
    }
}
