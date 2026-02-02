namespace WOTRMultiplayer.Entities.Settings
{
    public class NetworkTurnBasedSettngs
    {
        public bool IsTurnBasedModeEnabled { get; set; }

        public float TimeScaleInNonPlayerTurn { get; set; }

        public float TimeScaleInPlayerTurn { get; set; }

        public bool AutoEndTurn { get; set; }

        public bool AutoStopAfterFirstMoveAction { get; set; }
    }
}
