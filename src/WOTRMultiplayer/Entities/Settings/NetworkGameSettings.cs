namespace WOTRMultiplayer.Entities.Settings
{
    public class NetworkGameSettings
    {
        public NetworkTurnBasedSettngs TurnBased { get; set; }

        public NetworkGameMainSettings Main { get; set; }

        public NetworkAutopauseSettings Autopause { get; set; }

        public NetworkMultiplayerSettings Multiplayer { get; set; }
    }
}
