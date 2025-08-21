using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyContainerLooted)]
    public class NotifyContainerLooted
    {
        [ProtoMember(1)]
        public NetworkLootContainer Container { get; set; }
    }
}
