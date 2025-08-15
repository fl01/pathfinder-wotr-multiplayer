using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType(1046)]
    public class NotifySpellForgotten
    {
        [ProtoMember(1)]
        public NetworkSpellSlot Slot { get; set; }
    }
}
