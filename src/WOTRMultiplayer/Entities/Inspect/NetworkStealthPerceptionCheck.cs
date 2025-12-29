namespace WOTRMultiplayer.Entities.Inspect
{
    public class NetworkStealthPerceptionCheck
    {
        public string InitiatorId { get; set; }

        public string StealthedUnitId { get; set; }

        public int Roll { get; set; }

        public bool IsSuccess { get; set; }

        public int DC { get; set; }

        public bool IsTargetInvisible { get; set; }

        public bool IgnoreDifficultyBonusToDC { get; set; }
    }
}
