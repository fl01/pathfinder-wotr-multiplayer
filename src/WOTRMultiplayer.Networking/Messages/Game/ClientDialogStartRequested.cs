using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.ClientDialogStartRequested)]
    public class ClientDialogStartRequested
    {
        [ProtoMember(1)]
        public string DialogName { get; set; }

        [ProtoMember(2)]
        public string TargetUnitId { get; set; }

        [ProtoMember(3)]
        public string InitiatorUnitId { get; set; }

        [ProtoMember(4)]
        public string MapObjectId { get; set; }

        [ProtoMember(5)]
        public string SpeakerKey { get; set; }
    }
}
