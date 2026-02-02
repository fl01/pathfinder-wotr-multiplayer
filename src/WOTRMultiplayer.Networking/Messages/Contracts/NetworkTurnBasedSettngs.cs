using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkTurnBasedSettngs
    {
        [ProtoMember(1)]
        public bool IsTurnBasedModeEnabled { get; set; }

        [ProtoMember(2)]
        public float TimeScaleInNonPlayerTurn { get; set; }

        [ProtoMember(3)]
        public float TimeScaleInPlayerTurn { get; set; }

        [ProtoMember(4)]
        public bool AutoEndTurn { get; set; }

        [ProtoMember(5)]
        public bool AutoStopAfterFirstMoveAction { get; set; }
    }
}
