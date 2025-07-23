using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType(1020)]
    public class NotifyAbilityClicked
    {
        [ProtoMember(1)]
        public NetworkClick Click { get; set; }
    }
}
