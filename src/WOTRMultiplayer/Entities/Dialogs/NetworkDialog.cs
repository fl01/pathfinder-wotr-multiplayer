namespace WOTRMultiplayer.Entities.Dialogs
{
    public class NetworkDialog
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string TargetUnitId { get; set; }

        public string InitiatorUnitId { get; set; }

        public string MapObjectId { get; set; }

        public string SpeakerKey { get; set; }

        public bool IsScripted { get; set; }
    }
}
