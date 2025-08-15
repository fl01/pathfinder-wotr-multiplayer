using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType(1045)]
    public class NotifySpellMemorized
    {
        [ProtoMember(1)]
        public NetworkSpellSlot Slot { get; set; }
    }
}
