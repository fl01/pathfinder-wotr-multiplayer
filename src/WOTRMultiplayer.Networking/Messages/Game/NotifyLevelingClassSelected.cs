using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingClassSelected)]
    public class NotifyLevelingClassSelected
    {
        [ProtoMember(1)]
        public string ClassId { get; set; }
    }
}
