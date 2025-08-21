using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifySpellForgotten)]
    public class NotifySpellForgotten
    {
        [ProtoMember(1)]
        public NetworkSpellSlot Slot { get; set; }
    }
}
