using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyAIActionSelected)]
    public class NotifyAIActionSelected
    {
        [ProtoMember(1)]
        public NetworkAIAction Action { get; set; }
    }
}
