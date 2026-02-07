using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyInventoryItemCopied)]
    public class NotifyInventoryItemCopied
    {
        [ProtoMember(1)]
        public NetworkItemCopy Copy { get; set; }
    }
}
