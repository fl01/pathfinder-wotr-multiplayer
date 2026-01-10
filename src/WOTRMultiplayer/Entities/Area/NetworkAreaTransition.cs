namespace WOTRMultiplayer.Entities.Area
{
    public class NetworkAreaTransition
    {
        public string AreaExitId { get; set; }

        public bool IsActionsTransition { get; set; }

        public NetworkArea From { get; set; }

        public NetworkArea To { get; set; }
    }
}
