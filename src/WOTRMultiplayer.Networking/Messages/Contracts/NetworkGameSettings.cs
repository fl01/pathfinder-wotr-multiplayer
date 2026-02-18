using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkGameSettings
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkTurnBasedSettngs TurnBased { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public NetworkGameMainSettings Main { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public NetworkAutopauseSettings Autopause { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public NetworkMultiplayerSettings Multiplayer { get; set; }
    }
}
