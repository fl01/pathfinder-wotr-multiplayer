namespace WOTRMultiplayer.Entities.Content
{
    public class NetworkDiscrepantMod
    {
        public NetworkMod Mod { get; set; }

        public NetworkDiscrepancyReason Reason { get; set; }

        public NetworkDiscrepantMod(NetworkMod mod, NetworkDiscrepancyReason reason)
        {
            Mod = mod;
            Reason = reason;
        }

        public NetworkDiscrepantMod()
        {
        }
    }
}
