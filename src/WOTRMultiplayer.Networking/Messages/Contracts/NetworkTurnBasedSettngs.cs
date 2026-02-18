using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkTurnBasedSettngs
    {
        [ProtoMember(1)]
        [LogMe]
        public bool IsTurnBasedModeEnabled { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public float? TimeScaleInNonPlayerTurn { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public float? TimeScaleInPlayerTurn { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public bool AutoEndTurn { get; set; }

        [ProtoMember(5)]
        [LogMe]
        public bool AutoStopAfterFirstMoveAction { get; set; }
    }
}
