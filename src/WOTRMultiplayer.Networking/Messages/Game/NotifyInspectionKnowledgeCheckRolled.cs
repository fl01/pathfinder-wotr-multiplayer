using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyInspectionKnowledgeCheckRolled)]
    public class NotifyInspectionKnowledgeCheckRolled
    {
        [ProtoMember(1)]
        public NetworkInspectionKnowledgeCheck Check { get; set; }
    }
}
