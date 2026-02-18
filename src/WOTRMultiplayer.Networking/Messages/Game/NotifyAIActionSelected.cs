using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyAIActionSelected)]
    public class NotifyAIActionSelected
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkAIAction Action { get; set; }
    }
}
