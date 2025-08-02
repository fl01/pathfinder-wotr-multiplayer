using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType(1026)]
    public class NotifyContainerLooted
    {
        [ProtoMember(1)]
        public NetworkLootContainer Container { get; set; }
    }
}
